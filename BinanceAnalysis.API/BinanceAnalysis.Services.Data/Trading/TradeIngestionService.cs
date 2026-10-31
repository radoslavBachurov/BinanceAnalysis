namespace BinanceAnalysis.Services.Data.Trading
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Channels;
    using System.Threading.Tasks;
    using BinanceAnalysis.API.Infrastructure.Binance;
    using BinanceAnalysis.API.Infrastructure.Binance.Models;
    using BinanceAnalysis.Data;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;

    public class TradeIngestionService : BackgroundService
    {
        private readonly BinanceWebSocketClient webSocketClient;
        private readonly IServiceScopeFactory scopeFactory;
        private readonly ILogger<TradeIngestionService> logger;
        private readonly TradeIngestionOptions options;
        private readonly Channel<BinanceTradeEvent> channel;

        public TradeIngestionService(
            BinanceWebSocketClient webSocketClient,
            IServiceScopeFactory scopeFactory,
            IOptions<TradeIngestionOptions> options,
            ILogger<TradeIngestionService> logger)
        {
            this.webSocketClient = webSocketClient;
            this.scopeFactory = scopeFactory;
            this.logger = logger;
            this.options = options.Value;

            if (this.options.SymbolRefreshIntervalSeconds <= 0 || 
                this.options.ChannelCapacity <= 0 ||
                this.options.BatchSize <= 0 ||
                this.options.FlushIntervalSeconds <= 0 ||
                this.options.InitialReconnectDelaySeconds <= 0 ||
                this.options.MaximumReconnectDelaySeconds <
                    this.options.InitialReconnectDelaySeconds)
            {
                throw new ArgumentException(
                    "Trade ingestion configuration contains invalid values.");
            }

            this.channel = Channel.CreateBounded<BinanceTradeEvent>(
                new BoundedChannelOptions(this.options.ChannelCapacity)
                {
                    FullMode = BoundedChannelFullMode.Wait,
                    SingleReader = true,
                    SingleWriter = true,
                    AllowSynchronousContinuations = false
                });
        }

        protected override async Task ExecuteAsync(
            CancellationToken stoppingToken)
        {
            using var linkedCts =
                CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);

            var consumerTask = this.PersistTradesAsync();

            try
            {
                var producerTask =
                    this.ReceiveTradesWithReconnectAsync(linkedCts.Token);

                var completedTask = await Task.WhenAny(
                    producerTask,
                    consumerTask);

                if (completedTask == consumerTask)
                {
                    linkedCts.Cancel();
                    await consumerTask;
                }

                await producerTask;
            }
            finally
            {
                this.channel.Writer.TryComplete();
                linkedCts.Cancel();
            }

            // Drain and persist any remaining trades before shutdown.
            await consumerTask;
        }

        private async Task ReceiveTradesWithReconnectAsync(
            CancellationToken cancellationToken)
        {
            var reconnectDelay =
                this.options.InitialReconnectDelaySeconds;

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var symbols = await this.GetActiveSymbolsAsync(
                        cancellationToken);

                    if (symbols.Length == 0)
                    {
                        this.logger.LogWarning(
                            "No active Binance symbols found. Retrying in 10 seconds.");

                        await Task.Delay(
                            TimeSpan.FromSeconds(10),
                            cancellationToken);

                        continue;
                    }

                    this.logger.LogInformation(
                        "Connecting to Binance for symbols: {Symbols}",
                        string.Join(", ", symbols));

                    await this.webSocketClient.ListenToTradesAsync(
                symbols,
                async (trade, token) =>
                {
                     await this.channel.Writer.WriteAsync(trade, token);
                },
                this.RunSubscriptionManagerAsync,
                cancellationToken);

                    reconnectDelay = this.options.InitialReconnectDelaySeconds;

                    this.logger.LogWarning(
                        "Binance WebSocket connection closed.");
                }
                catch (OperationCanceledException)
                    when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    this.logger.LogError(
                        exception,
                        "Binance WebSocket connection failed.");
                }

                this.logger.LogInformation(
                    "Reconnecting to Binance in {Delay} seconds.",
                    reconnectDelay);

                await Task.Delay(
                    TimeSpan.FromSeconds(reconnectDelay),
                    cancellationToken);

                reconnectDelay = Math.Min(
                    reconnectDelay * 2,
                    this.options.MaximumReconnectDelaySeconds);
            }
        }

        private async Task<string[]> GetActiveSymbolsAsync(
            CancellationToken cancellationToken)
        {
            await using var scope =
                this.scopeFactory.CreateAsyncScope();

            var dbContext = scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();

            return await dbContext.MonitoredSymbols
                .AsNoTracking()
                .Where(symbol => symbol.IsActive)
                .Select(symbol => symbol.Symbol)
                .ToArrayAsync(cancellationToken);
        }

        private async Task PersistTradesAsync()
        {
            var batch = new List<BinanceTradeEvent>(
                this.options.BatchSize);

            using var timer = new PeriodicTimer(
                TimeSpan.FromSeconds(
                    this.options.FlushIntervalSeconds));

            var tickTask = timer.WaitForNextTickAsync().AsTask();
            var waitToReadTask =
                this.channel.Reader.WaitToReadAsync().AsTask();

            while (true)
            {
                while (batch.Count < this.options.BatchSize &&
                       this.channel.Reader.TryRead(out var trade))
                {
                    batch.Add(trade);
                }

                if (batch.Count >= this.options.BatchSize)
                {
                    await this.FlushBatchAsync(batch);
                    batch.Clear();
                    continue;
                }

                var completedTask = await Task.WhenAny(
                    waitToReadTask,
                    tickTask);

                if (completedTask == tickTask)
                {
                    if (!await tickTask)
                    {
                        break;
                    }

                    if (batch.Count > 0)
                    {
                        await this.FlushBatchAsync(batch);
                        batch.Clear();
                    }

                    tickTask = timer.WaitForNextTickAsync().AsTask();
                }
                else
                {
                    if (!await waitToReadTask)
                    {
                        break;
                    }

                    waitToReadTask =
                        this.channel.Reader.WaitToReadAsync().AsTask();
                }
            }

            if (batch.Count > 0)
            {
                await this.FlushBatchAsync(batch);
            }
        }

        private async Task FlushBatchAsync(
            List<BinanceTradeEvent> batch)
        {
            await using var scope =
                this.scopeFactory.CreateAsyncScope();

            var persistenceService = scope.ServiceProvider
                .GetRequiredService<TradePersistenceService>();

            await persistenceService.PersistBatchAsync(
                batch,
                CancellationToken.None);
        }

        private async Task RunSubscriptionManagerAsync(
        Func<IReadOnlyCollection<string>, CancellationToken, Task> updateSubscriptions,
        CancellationToken cancellationToken)
        {
            using var timer = new PeriodicTimer(
                TimeSpan.FromSeconds(
                    this.options.SymbolRefreshIntervalSeconds));

            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                var activeSymbols = await this.GetActiveSymbolsAsync(
                    cancellationToken);

                this.logger.LogInformation(
                    "Refreshing Binance subscriptions. Active symbols: {Symbols}",
                    string.Join(", ", activeSymbols));

                await updateSubscriptions(
                    activeSymbols,
                    cancellationToken);
            }
        }
    }
}

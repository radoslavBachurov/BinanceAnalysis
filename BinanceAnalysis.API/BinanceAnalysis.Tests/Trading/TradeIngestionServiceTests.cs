namespace BinanceAnalysis.Tests.Trading
{
    using BinanceAnalysis.API.Infrastructure.Binance;
    using BinanceAnalysis.API.Infrastructure.Binance.Models;
    using BinanceAnalysis.Data;
    using BinanceAnalysis.Data.Models;
    using BinanceAnalysis.Services.Data.Trading;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;
    using Microsoft.Extensions.Options;
    using Microsoft.Extensions.Time.Testing;
    using Moq;
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Channels;
    using System.Threading.Tasks;
    using Xunit;
    using System.Linq;

    public class TradeIngestionServiceTests
    {
        [Fact]
        public async Task ReconnectAsync_ImmediateFailures_UsesExponentialBackoff()
        {
            var attempts = Channel.CreateUnbounded<int>();
            var attemptCount = 0;

            var client = CreateFailingClient(attempts, () => ++attemptCount);
            var harness = await CreateHarnessAsync(client);

            try
            {
                await harness.Service.StartAsync(CancellationToken.None);

                var expectedDelays = new[] { 1, 2, 4, 4 };

                for (var i = 0; i < expectedDelays.Length; i++)
                {
                    Assert.Equal(
                        i + 1,
                        await ReadAttemptAsync(attempts, harness.Service));

                    var actualDelay =
                        await harness.Clock.ReadScheduledDelayAsync();

                    Assert.Equal(
                        TimeSpan.FromSeconds(expectedDelays[i]),
                        actualDelay);

                    // Leave the final retry pending so shutdown can
                    // cancel it without starting another connection.
                    if (i < expectedDelays.Length - 1)
                    {
                        harness.Clock.Advance(actualDelay);
                    }
                }
            }
            finally
            {
                await harness.Service.StopAsync(CancellationToken.None);
                harness.Provider.Dispose();
            }
        }

        [Fact]
        public async Task ReconnectAsync_ShortLivedConnections_DoesNotResetBackoff()
        {
            var attempts = Channel.CreateUnbounded<int>();
            var attemptCount = 0;

            var client = new Mock<IBinanceWebSocketClient>();

            client.Setup(x => x.ListenToTradesAsync(
                    It.IsAny<string[]>(),
                    It.IsAny<Func<BinanceTradeEvent, CancellationToken, Task>>(),
                    It.IsAny<Func<
                        Func<IReadOnlyCollection<string>, CancellationToken, Task>,
                        CancellationToken,
                        Task>>(),
                    It.IsAny<CancellationToken>()))
                .Returns((
                    string[] symbols,
                    Func<BinanceTradeEvent, CancellationToken, Task> onTrade,
                    Func<Func<IReadOnlyCollection<string>, CancellationToken, Task>,
                        CancellationToken, Task> subscriptionManager,
                    CancellationToken token) =>
                {
                    attempts.Writer.TryWrite(++attemptCount);

                    // A normal return simulates a connection that
                    // closes immediately.
                    return Task.CompletedTask;
                });

            var harness = await CreateHarnessAsync(client);

            try
            {
                await harness.Service.StartAsync(CancellationToken.None);

                var expectedDelays = new[] { 1, 2, 4 };

                for (var i = 0; i < expectedDelays.Length; i++)
                {
                    Assert.Equal(
                        i + 1,
                        await ReadAttemptAsync(attempts, harness.Service));

                    var actualDelay =
                        await harness.Clock.ReadScheduledDelayAsync();

                    Assert.Equal(
                        TimeSpan.FromSeconds(expectedDelays[i]),
                        actualDelay);

                    if (i < expectedDelays.Length - 1)
                    {
                        harness.Clock.Advance(actualDelay);
                    }
                }
            }
            finally
            {
                await harness.Service.StopAsync(CancellationToken.None);
                harness.Provider.Dispose();
            }
        }

        [Fact]
        public async Task ReconnectAsync_StableConnection_ResetsBackoff()
        {
            var attempts = Channel.CreateUnbounded<int>();
            var attemptCount = 0;

            var connectionLifetime =
                new TaskCompletionSource(
                    TaskCreationOptions.RunContinuationsAsynchronously);

            var client = new Mock<IBinanceWebSocketClient>();

            client.Setup(x => x.ListenToTradesAsync(
                    It.IsAny<string[]>(),
                    It.IsAny<Func<BinanceTradeEvent, CancellationToken, Task>>(),
                    It.IsAny<Func<
                        Func<IReadOnlyCollection<string>, CancellationToken, Task>,
                        CancellationToken,
                        Task>>(),
                    It.IsAny<CancellationToken>()))
                .Returns((
                    string[] symbols,
                    Func<BinanceTradeEvent, CancellationToken, Task> onTrade,
                    Func<Func<IReadOnlyCollection<string>, CancellationToken, Task>,
                        CancellationToken, Task> subscriptionManager,
                    CancellationToken token) =>
                {
                    var attempt = ++attemptCount;
                    attempts.Writer.TryWrite(attempt);

                    if (attempt == 1)
                    {
                        // Keep the first connection open until the
                        // test advances the fake clock by 30 seconds.
                        return connectionLifetime.Task;
                    }

                    return Task.FromException(
                        new IOException("Simulated connection failure."));
                });

            var harness = await CreateHarnessAsync(client);

            try
            {
                await harness.Service.StartAsync(CancellationToken.None);

                Assert.Equal(1, await ReadAttemptAsync(attempts, harness.Service));

                // Simulate a connection that remains alive for
                // exactly the configured stability threshold.
                harness.Clock.Advance(TimeSpan.FromSeconds(30));
                connectionLifetime.SetResult();

                // Wait until the service schedules its next retry.
                var actualDelay =
                    await harness.Clock.ReadScheduledDelayAsync();

                // A stable connection must reset the delay to 1 second.
                Assert.Equal(TimeSpan.FromSeconds(1), actualDelay);

                // Advance the retry delay so the next connection can start.
                harness.Clock.Advance(actualDelay);

                Assert.Equal(2, await ReadAttemptAsync(attempts, harness.Service));
            }
            finally
            {
                // Ensure the connection task cannot hold shutdown open.
                connectionLifetime.TrySetResult();

                await harness.Service.StopAsync(CancellationToken.None);
                harness.Provider.Dispose();
            }
        }

        [Fact]
        public async Task ReconnectAsync_CancelledDuringRetryDelay_DoesNotReconnect()
        {
            var attempts = Channel.CreateUnbounded<int>();
            var attemptCount = 0;

            var client = CreateFailingClient(attempts, () => ++attemptCount);
            var harness = await CreateHarnessAsync(client);

            try
            {
                await harness.Service.StartAsync(CancellationToken.None);

                Assert.Equal(1, await ReadAttemptAsync(attempts, harness.Service));

                // Wait until the retry delay has actually been scheduled.
                var scheduledDelay =
                    await harness.Clock.ReadScheduledDelayAsync();

                Assert.Equal(TimeSpan.FromSeconds(1), scheduledDelay);

                // StopAsync cancels the pending retry delay.
                await harness.Service.StopAsync(CancellationToken.None);

                Assert.Equal(1, attemptCount);
                Assert.False(attempts.Reader.TryRead(out _));
            }
            finally
            {
                harness.Provider.Dispose();
            }
        }

        private static Mock<IBinanceWebSocketClient> CreateFailingClient(
            Channel<int> attempts,
            Func<int> getNextAttempt)
        {
            var client = new Mock<IBinanceWebSocketClient>();

            client.Setup(x => x.ListenToTradesAsync(
                    It.IsAny<string[]>(),
                    It.IsAny<Func<BinanceTradeEvent, CancellationToken, Task>>(),
                    It.IsAny<Func<
                        Func<IReadOnlyCollection<string>, CancellationToken, Task>,
                        CancellationToken,
                        Task>>(),
                    It.IsAny<CancellationToken>()))
                .Returns((
                    string[] symbols,
                    Func<BinanceTradeEvent, CancellationToken, Task> onTrade,
                    Func<Func<IReadOnlyCollection<string>, CancellationToken, Task>,
                        CancellationToken, Task> subscriptionManager,
                    CancellationToken token) =>
                {
                    attempts.Writer.TryWrite(getNextAttempt());

                    return Task.FromException(
                        new IOException("Simulated connection failure."));
                });

            return client;
        }

        private static async Task<int> ReadAttemptAsync(
    Channel<int> attempts,
    TradeIngestionService service)
        {
            var readTask = attempts.Reader.ReadAsync().AsTask();

            var serviceTask = service.ExecuteTask
                ?? throw new InvalidOperationException(
                    "The background service has not started.");

            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(10));

            var completedTask = await Task.WhenAny(
                readTask,
                serviceTask,
                timeoutTask);

            if (completedTask == readTask)
            {
                return await readTask;
            }

            if (completedTask == serviceTask)
            {
                // Surface the actual background-service exception if it faulted.
                await serviceTask;

                throw new InvalidOperationException(
                    "The background service stopped before recording a connection attempt.");
            }

            throw new TimeoutException(
                $"No connection attempt was recorded. Background service status: {serviceTask.Status}.");
        }

        private static async Task<TestHarness> CreateHarnessAsync(
            Mock<IBinanceWebSocketClient> client)
        {
            var services = new ServiceCollection();

            services.AddLogging();

            var databaseName = Guid.NewGuid().ToString();

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase(databaseName));

            services.AddScoped<TradePersistenceService>();

            var provider = services.BuildServiceProvider();
            await using (var scope = provider.CreateAsyncScope())
            {
                var dbContext = scope.ServiceProvider
                    .GetRequiredService<ApplicationDbContext>();

                dbContext.MonitoredSymbols.Add(new MonitoredSymbol
                {
                    Symbol = "BTCUSDT",
                    IsActive = true,
                    CreatedAtUtc = DateTime.UtcNow
                });

                await dbContext.SaveChangesAsync();

            }

            var clock = new SignallingFakeTimeProvider();

            var options = new TradeIngestionOptions
            {
                ChannelCapacity = 10,
                BatchSize = 10,
                FlushIntervalSeconds = 3600,
                InitialReconnectDelaySeconds = 1,
                MaximumReconnectDelaySeconds = 4,
                StableConnectionSeconds = 30,
                SymbolRefreshIntervalSeconds = 3600
            };

            var service = new TradeIngestionService(
                client.Object,
                provider.GetRequiredService<IServiceScopeFactory>(),
                Options.Create(options),
                LoggerFactory.Create(builder =>
                builder.AddConsole()).CreateLogger<TradeIngestionService>(),
                clock);

            return new TestHarness(service, provider, clock);
        }

        private sealed record TestHarness(
            TradeIngestionService Service,
            ServiceProvider Provider,
            SignallingFakeTimeProvider Clock);

        private sealed class SignallingFakeTimeProvider : FakeTimeProvider
        {
            private readonly Channel<TimeSpan> scheduledDelays =
                Channel.CreateUnbounded<TimeSpan>();

            public override ITimer CreateTimer(
                TimerCallback callback,
                object? state,
                TimeSpan dueTime,
                TimeSpan period)
            {
                // Task.Delay uses a one-shot timer. Periodic timers
                // have a finite period and are intentionally ignored.
                if (period == Timeout.InfiniteTimeSpan)
                {
                    this.scheduledDelays.Writer.TryWrite(dueTime);
                }

                return base.CreateTimer(
                    callback,
                    state,
                    dueTime,
                    period);
            }

            public async Task<TimeSpan> ReadScheduledDelayAsync()
            {
                using var timeout =
                    new CancellationTokenSource(TimeSpan.FromSeconds(10));

                return await this.scheduledDelays.Reader.ReadAsync(
                    timeout.Token);
            }
        }
    }
}

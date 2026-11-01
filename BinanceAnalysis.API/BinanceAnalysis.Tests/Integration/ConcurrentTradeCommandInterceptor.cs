namespace BinanceAnalysis.Tests.Integration
{
    using System.Data.Common;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.EntityFrameworkCore.Diagnostics;

    public sealed class ConcurrentTradeCommandInterceptor
        : DbCommandInterceptor
    {
        private readonly TaskCompletionSource<bool> bothQueriesCompleted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private int queryCount;

        public override async ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command,
            CommandExecutedEventData eventData,
            DbDataReader result,
            CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains(
                "MarketPrices",
                StringComparison.OrdinalIgnoreCase))
            {
                if (Interlocked.Increment(ref this.queryCount) == 2)
                {
                    this.bothQueriesCompleted.TrySetResult(true);
                }

                await this.bothQueriesCompleted.Task.WaitAsync(
                    cancellationToken);
            }

            return result;
        }
    }
}
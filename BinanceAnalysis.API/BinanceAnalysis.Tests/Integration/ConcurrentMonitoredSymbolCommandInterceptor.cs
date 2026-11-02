namespace BinanceAnalysis.Tests.Integration
{
    using System.Data.Common;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.EntityFrameworkCore.Diagnostics;

    public sealed class ConcurrentMonitoredSymbolCommandInterceptor
        : DbCommandInterceptor
    {
        private readonly TaskCompletionSource<bool> bothChecksReached =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private int checkCount;

        public override async ValueTask<InterceptionResult<DbDataReader>>
            ReaderExecutingAsync(
                DbCommand command,
                CommandEventData eventData,
                InterceptionResult<DbDataReader> result,
                CancellationToken cancellationToken = default)
        {
            var isMonitoredSymbolSelect =
                command.CommandText.TrimStart().StartsWith(
                    "SELECT",
                    StringComparison.OrdinalIgnoreCase) &&
                command.CommandText.Contains(
                    "MonitoredSymbols",
                    StringComparison.OrdinalIgnoreCase);

            if (isMonitoredSymbolSelect)
            {
                if (Interlocked.Increment(ref this.checkCount) == 2)
                {
                    this.bothChecksReached.TrySetResult(true);
                }

                await this.bothChecksReached.Task.WaitAsync(
                    cancellationToken);
            }

            return result;
        }
    }
}
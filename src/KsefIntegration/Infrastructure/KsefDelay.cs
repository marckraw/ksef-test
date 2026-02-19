using System;
using System.Threading;
using System.Threading.Tasks;

namespace KsefIntegration.Infrastructure
{
    internal static class KsefDelay
    {
        public static Task BackoffAsync(int attempt, int baseDelayMs, CancellationToken cancellationToken)
        {
            var multiplier = Math.Max(1, attempt);
            var delay = Math.Min(baseDelayMs * multiplier, 15000);
            return Task.Delay(delay, cancellationToken);
        }
    }
}

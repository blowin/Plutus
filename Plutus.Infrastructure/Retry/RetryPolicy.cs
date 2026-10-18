using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Plutus.Domain.Retry;

namespace Plutus.Infrastructure.Retry;

/// <summary>
/// Executes an async operation with bounded exponential backoff retries for transient failures.
/// </summary>
public class RetryPolicy(TimeoutOptions options)
{
    private readonly TimeoutOptions _options = options ?? throw new ArgumentNullException(nameof(options));

    /// <summary>
    /// Executes the operation, retrying on transient failures with exponential backoff.
    /// </summary>
    /// <typeparam name="T">The return type of the operation.</typeparam>
    /// <param name="operation">The async operation to execute.</param>
    /// <param name="cancellationToken">Token used to cancel the operation and all retries.</param>
    /// <returns>The result of the operation.</returns>
    /// <exception cref="T:Exception">The original exception if all retries are exhausted or the error is permanent.</exception>
    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        var attempt = 0;

        while (true)
        {
            try
            {
                return await operation(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Propagate cancellation immediately — never retry user-initiated cancellation
                throw;
            }
            catch (Exception ex)
            {
                // Check if this exception is retryable
                if (!RetryableExceptionFilter.IsRetryable(ex))
                {
                    throw; // Permanent error — fail promptly
                }

                // Check if we have retries left
                if (attempt >= _options.MaxRetries)
                {
                    throw; // Exhausted all retries
                }

                // Compute backoff delay with jitter
                var delay = ComputeBackoff(attempt);
                attempt++;

                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Computes the backoff delay for the given attempt number, with exponential growth and jitter.
    /// </summary>
    private TimeSpan ComputeBackoff(int attempt)
    {
        // Exponential: baseDelay * 2^attempt
        var exponentialDelay = _options.RetryBaseDelay * Math.Pow(2, attempt);

        // Add jitter: random value between 0 and baseDelay
        var jitter = TimeSpan.FromMilliseconds(Random.Shared.Next(0, (int)_options.RetryBaseDelay.TotalMilliseconds + 1));

        // Cap at max delay
        var total = exponentialDelay + jitter;
        if (total > _options.RetryMaxDelay)
        {
            total = _options.RetryMaxDelay;
        }

        return total;
    }
}

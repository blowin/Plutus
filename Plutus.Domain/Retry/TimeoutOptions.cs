using System;

namespace Plutus.Domain.Retry;

/// <summary>
/// Configuration for HTTP operation timeouts and retry behavior.
/// </summary>
public record TimeoutOptions
{
    /// <summary>
    /// Default timeout options used when no configuration is provided.
    /// </summary>
    public static readonly TimeoutOptions Default = new();

    /// <summary>
    /// Timeout for the entire HTTP operation. Default: 120 seconds.
    /// </summary>
    public TimeSpan OperationTimeout { get; init; } = TimeSpan.FromSeconds(120);

    /// <summary>
    /// Maximum number of retry attempts for transient failures. Default: 3.
    /// </summary>
    public int MaxRetries { get; init; } = 3;

    /// <summary>
    /// Base delay for exponential backoff. Default: 500 milliseconds.
    /// </summary>
    public TimeSpan RetryBaseDelay { get; init; } = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// Maximum delay between retries. Default: 30 seconds.
    /// </summary>
    public TimeSpan RetryMaxDelay { get; init; } = TimeSpan.FromSeconds(30);
}

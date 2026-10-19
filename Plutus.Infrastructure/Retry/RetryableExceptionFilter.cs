using System.Net;

namespace Plutus.Infrastructure.Retry;

/// <summary>
/// Classifies exceptions and HTTP responses as retryable or permanent.
/// Only transient network failures are considered retryable.
/// </summary>
public static class RetryableExceptionFilter
{
    private static readonly ISet<HttpStatusCode> RetryableStatusCodes = new HashSet<HttpStatusCode>
    {
        HttpStatusCode.RequestTimeout,      // 408
        HttpStatusCode.TooManyRequests,     // 429
        HttpStatusCode.InternalServerError, // 500
        HttpStatusCode.BadGateway,          // 502
        HttpStatusCode.ServiceUnavailable,  // 503
        HttpStatusCode.GatewayTimeout,      // 504
    };

    /// <summary>
    /// Determines whether the given exception represents a transient failure that may succeed on retry.
    /// </summary>
    /// <param name="exception">The exception to classify.</param>
    /// <returns>True if the error is retryable; false for permanent failures.</returns>
    public static bool IsRetryable(Exception exception)
    {
        // Check more specific types before base types
        if (exception is TaskCanceledException)
        {
            return true; // Usually a timeout on .NET
        }

        if (exception is OperationCanceledException)
        {
            return false; // User-initiated cancellation is not retryable
        }

        if (exception is HttpRequestException httpRequestException)
        {
            // HttpRequestException with a status code — check if it's retryable
            // HttpRequestException without status code (e.g., network failure, timeout)
            return httpRequestException.StatusCode is null || IsRetryableStatusCode(httpRequestException.StatusCode.Value);
        }

        // Check file system exceptions before IOException
        if (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return false; // Permanent — file/dir doesn't exist
        }

        if (exception is IOException)
        {
            return true; // I/O errors on network streams are often transient
        }

        if (exception is ObjectDisposedException)
        {
            return false; // Programming error, not transient
        }

        if (exception is ArgumentException or ArgumentNullException or InvalidOperationException or
            ArgumentOutOfRangeException or UnauthorizedAccessException or
            InvalidDataException)
        {
            return false; // Permanent configuration or data errors
        }

        // For any other exception type, treat as non-retryable to avoid infinite retry loops
        return false;
    }

    /// <summary>
    /// Determines whether the given HTTP status code is retryable.
    /// </summary>
    /// <param name="statusCode">The HTTP status code to check.</param>
    /// <returns>True if the status code indicates a transient failure.</returns>
    public static bool IsRetryableStatusCode(HttpStatusCode statusCode) => RetryableStatusCodes.Contains(statusCode);
}

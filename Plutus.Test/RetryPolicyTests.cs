using System.Net;
using FluentAssertions;
using Plutus.Domain.Retry;
using Plutus.Infrastructure.Retry;

namespace Plutus.Test;

public class RetryPolicyTests
{
    private static readonly TimeoutOptions DefaultOptions = new()
    {
        MaxRetries = 3,
        RetryBaseDelay = TimeSpan.FromMilliseconds(10),
        RetryMaxDelay = TimeSpan.FromSeconds(1),
        OperationTimeout = TimeSpan.FromSeconds(1),
    };

    [Fact]
    public async Task ExecuteAsync_WhenOperationSucceedsFirstTime_ShouldReturnResult()
    {
        // Arrange
        var policy = new RetryPolicy(DefaultOptions);
        var callCount = 0;

        // Act
        var result = await policy.ExecuteAsync<int>(_ =>
        {
            callCount++;
            return Task.FromResult(42);
        }, CancellationToken.None);

        // Assert
        result.Should().Be(42);
        callCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTransientFailureThenSucceed_ShouldRetryAndReturnResult()
    {
        // Arrange
        var policy = new RetryPolicy(DefaultOptions);
        var callCount = 0;

        // Act
        var result = await policy.ExecuteAsync<string>(_ =>
        {
            callCount++;
            if (callCount < 3)
            {
                throw new HttpRequestException("Connection reset", null, HttpStatusCode.ServiceUnavailable);
            }

            return Task.FromResult("success");
        }, CancellationToken.None);

        // Assert
        result.Should().Be("success");
        callCount.Should().Be(3);
    }

    [Fact]
    public async Task ExecuteAsync_WhenPermanentError_ShouldFailImmediately()
    {
        // Arrange
        var policy = new RetryPolicy(DefaultOptions);
        var callCount = 0;

        // Act
        Func<CancellationToken, Task<int>> operation = _ =>
        {
            callCount++;
            throw new ArgumentException("Invalid argument");
        };

        // Assert
        Func<Task> act = async () => await policy.ExecuteAsync(operation, CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentException>();
        callCount.Should().Be(1); // No retry for permanent errors
    }

    [Fact]
    public async Task ExecuteAsync_WhenAllRetriesExhausted_ShouldThrowOriginalException()
    {
        // Arrange
        var policy = new RetryPolicy(DefaultOptions);
        var callCount = 0;

        // Act
        Func<CancellationToken, Task<string>> operation = _ =>
        {
            callCount++;
            throw new HttpRequestException("Network error", null, HttpStatusCode.TooManyRequests);
        };

        // Assert
        Func<Task> act = async () => await policy.ExecuteAsync(operation, CancellationToken.None);
        await act.Should().ThrowAsync<HttpRequestException>();
        callCount.Should().Be(4); // 1 initial + 3 retries
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancelled_ShouldPropagateCancellation()
    {
        // Arrange
        var policy = new RetryPolicy(DefaultOptions);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        Func<CancellationToken, Task<string>> operation = ct =>
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult("should not reach");
        };

        // Assert
        Func<Task> act = async () => await policy.ExecuteAsync(operation, cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ExecuteAsync_When404_ShouldFailImmediately()
    {
        // Arrange
        var policy = new RetryPolicy(DefaultOptions);
        var callCount = 0;

        // Act
        Func<CancellationToken, Task<int>> operation = _ =>
        {
            callCount++;
            throw new HttpRequestException("Not found", null, HttpStatusCode.NotFound);
        };

        // Assert
        Func<Task> act = async () => await policy.ExecuteAsync(operation, CancellationToken.None);
        await act.Should().ThrowAsync<HttpRequestException>();
        callCount.Should().Be(1); // 404 is not retryable
    }

    [Fact]
    public async Task ExecuteAsync_When503_ShouldRetry()
    {
        // Arrange
        var policy = new RetryPolicy(DefaultOptions);
        var callCount = 0;

        // Act
        Func<CancellationToken, Task<string>> operation = _ =>
        {
            callCount++;
            if (callCount < 2)
            {
                throw new HttpRequestException("Service unavailable", null, HttpStatusCode.ServiceUnavailable);
            }

            return Task.FromResult("recovered");
        };

        var result = await policy.ExecuteAsync(operation, CancellationToken.None);

        // Assert
        result.Should().Be("recovered");
        callCount.Should().Be(2);
    }
}

using System.Net;
using System.Net.Http;
using FluentAssertions;
using Plutus.Domain.Retry;
using Plutus.Infrastructure.Retry;

namespace Plutus.Test;

public class RetryableExceptionFilterTests
{
    [Theory]
    [InlineData(typeof(OperationCanceledException), false, new object[] { })]
    [InlineData(typeof(TaskCanceledException), true, new object[] { })]
    [InlineData(typeof(HttpRequestException), true, new object[] { "test" })]
    [InlineData(typeof(IOException), true, new object[] { "test" })]
    [InlineData(typeof(ObjectDisposedException), false, new object[] { "test" })]
    [InlineData(typeof(ArgumentException), false, new object[] { "test" })]
    [InlineData(typeof(ArgumentNullException), false, new object[] { "param" })]
    [InlineData(typeof(InvalidOperationException), false, new object[] { "test" })]
    [InlineData(typeof(UnauthorizedAccessException), false, new object[] { "test" })]
    [InlineData(typeof(DirectoryNotFoundException), false, new object[] { "test" })]
    [InlineData(typeof(FileNotFoundException), false, new object[] { "test" })]
    [InlineData(typeof(InvalidDataException), false, new object[] { "test" })]
    [InlineData(typeof(FormatException), false, new object[] { "test" })]
    [InlineData(typeof(NotSupportedException), false, new object[] { "test" })]
    public void IsRetryable_ShouldClassifyCorrectly(Type exceptionType, bool expected, object[] constructorArgs)
    {
        // Arrange
        var exception = (Exception)Activator.CreateInstance(exceptionType, constructorArgs)!;

        // Act
        var result = RetryableExceptionFilter.IsRetryable(exception);

        // Assert
        result.Should().Be(expected, $"Expected {exceptionType.Name} to be {(expected ? "retryable" : "permanent")}");
    }

    [Theory]
    [InlineData(HttpStatusCode.RequestTimeout, true)]       // 408
    [InlineData(HttpStatusCode.TooManyRequests, true)]      // 429
    [InlineData(HttpStatusCode.InternalServerError, true)]  // 500
    [InlineData(HttpStatusCode.BadGateway, true)]           // 502
    [InlineData(HttpStatusCode.ServiceUnavailable, true)]   // 503
    [InlineData(HttpStatusCode.GatewayTimeout, true)]       // 504
    [InlineData(HttpStatusCode.NotFound, false)]            // 404
    [InlineData(HttpStatusCode.Forbidden, false)]           // 403
    [InlineData(HttpStatusCode.Unauthorized, false)]        // 401
    [InlineData(HttpStatusCode.OK, false)]                  // 200
    [InlineData(HttpStatusCode.Created, false)]             // 201
    public void IsRetryableStatusCode_ShouldClassifyCorrectly(HttpStatusCode statusCode, bool expected)
    {
        // Act
        var result = RetryableExceptionFilter.IsRetryableStatusCode(statusCode);

        // Assert
        result.Should().Be(expected, $"Expected {statusCode} to be {(expected ? "retryable" : "permanent")}");
    }

    [Fact]
    public void IsRetryable_HttpRequestExceptionWithRetryableStatusCode_ShouldReturnTrue()
    {
        // Arrange
        var exception = new HttpRequestException("Rate limited", null, HttpStatusCode.TooManyRequests);

        // Act
        var result = RetryableExceptionFilter.IsRetryable(exception);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsRetryable_HttpRequestExceptionWithPermanentStatusCode_ShouldReturnFalse()
    {
        // Arrange
        var exception = new HttpRequestException("Not found", null, HttpStatusCode.NotFound);

        // Act
        var result = RetryableExceptionFilter.IsRetryable(exception);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsRetryable_OperationCanceledException_ShouldReturnFalse()
    {
        // Arrange
        var exception = new OperationCanceledException("User cancelled");

        // Act
        var result = RetryableExceptionFilter.IsRetryable(exception);

        // Assert
        result.Should().BeFalse();
    }
}

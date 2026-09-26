namespace Atc.Rest.MinimalApi.Tests.Middleware;

/// <summary>
/// Custom mappings: the most-derived mapped type wins, whatever order the mappings were added in.
/// </summary>
public sealed class ExceptionMappingOrderTests
{
    [Fact]
    public async Task Invoke_WhenDerivedMappedBeforeBase_UsesTheNearestMappedBaseType()
    {
        // Arrange
        var options = new GlobalErrorHandlingOptions()
            .MapException<FileNotFoundException>(HttpStatusCode.NotFound)
            .MapException<IOException>(HttpStatusCode.ServiceUnavailable);

        // Act
        var statusCode = await InvokeAsync(options, new CustomFileNotFoundTestException("missing"));

        // Assert
        statusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Invoke_WhenBaseMappedBeforeDerived_UsesTheNearestMappedBaseType()
    {
        // Arrange
        var options = new GlobalErrorHandlingOptions()
            .MapException<IOException>(HttpStatusCode.ServiceUnavailable)
            .MapException<FileNotFoundException>(HttpStatusCode.NotFound);

        // Act
        var statusCode = await InvokeAsync(options, new CustomFileNotFoundTestException("missing"));

        // Assert
        statusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Invoke_WhenOnlyTheBaseIsMapped_ASiblingUsesTheBaseMapping()
    {
        // Arrange
        var options = new GlobalErrorHandlingOptions()
            .MapException<FileNotFoundException>(HttpStatusCode.NotFound)
            .MapException<IOException>(HttpStatusCode.ServiceUnavailable);

        // Act
        var statusCode = await InvokeAsync(options, new DirectoryNotFoundException("missing"));

        // Assert
        statusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
    }

    [Fact]
    public async Task Invoke_WhenACustomBaseIsMapped_ItWinsOverAMoreDerivedDefaultMapping()
    {
        // Arrange - documented contract: custom mappings take precedence over the built-in defaults
        var options = new GlobalErrorHandlingOptions()
            .MapException<ArgumentException>(HttpStatusCode.UnprocessableEntity);

        // Act
        var statusCode = await InvokeAsync(options, new CustomArgumentOutOfRangeTestException("Out of range."));

        // Assert
        statusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
    }

    [Fact]
    public async Task Invoke_WhenBadHttpRequestExceptionIsMapped_TheMappingWinsOverItsStatusCode()
    {
        // Arrange
        var options = new GlobalErrorHandlingOptions()
            .MapException<BadHttpRequestException>(HttpStatusCode.BadRequest);

        // Act
        var statusCode = await InvokeAsync(options, new BadHttpRequestException("Too large.", StatusCodes.Status413PayloadTooLarge));

        // Assert
        statusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    private static async Task<int> InvokeAsync(
        GlobalErrorHandlingOptions options,
        Exception exception)
    {
        var context = new DefaultHttpContext { Response = { Body = new MemoryStream() } };
        var middleware = new GlobalErrorHandlingMiddleware(_ => throw exception, options);

        await middleware.Invoke(context);

        return context.Response.StatusCode;
    }
}
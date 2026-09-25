namespace Demo.Api.Contracts.Tests.Endpoints;

public class TestEndpointDefinitionTests
{
    [Fact]
    public void SimulateServerError_ThrowsInvalidDataException()
        => Assert.Throws<InvalidDataException>(TestEndpointDefinition.SimulateServerError);

    [Fact]
    public void SimulateClientError_ThrowsArgumentException()
        => Assert.Throws<ArgumentException>(() => TestEndpointDefinition.SimulateClientError("bad-value"));

    [Fact]
    public async Task SimulateStreamThenThrow_WritesPartialBodyThenThrows()
    {
        // Arrange
        var body = new MemoryStream();
        var context = new DefaultHttpContext { Response = { Body = body } };

        // Act
        await Assert.ThrowsAsync<InvalidDataException>(() => TestEndpointDefinition.SimulateStreamThenThrow(context));

        // Assert - part of the body was written before the exception
        var written = System.Text.Encoding.UTF8.GetString(body.ToArray());
        Assert.StartsWith("chunk 1", written, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SimulateSlowStream_WhenClientDisconnects_ThrowsOperationCanceledException()
    {
        // Arrange
        using var aborted = new CancellationTokenSource();
        await aborted.CancelAsync();

        var context = new DefaultHttpContext
        {
            RequestAborted = aborted.Token,
            Response = { Body = new MemoryStream() },
        };

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => TestEndpointDefinition.SimulateSlowStream(context));
    }
}
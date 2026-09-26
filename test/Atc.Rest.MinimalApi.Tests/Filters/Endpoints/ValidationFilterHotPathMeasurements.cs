namespace Atc.Rest.MinimalApi.Tests.Filters.Endpoints;

/// <summary>
/// Measures the cost of the ValidationFilter on a valid request (the hot path: the generator adds the filter to every
/// endpoint with parameters). Explicit: run with <c>-- --explicit only --filter-class "*HotPathMeasurements"</c>.
/// </summary>
public sealed class ValidationFilterHotPathMeasurements
{
    private const int Iterations = 20_000;

    private static readonly SetParameters ValidParameters = new(
        Mode: "x",
        PageSize: 10,
        Request: new SetRequest(Reason: "ok", FirstName: "Ada", Lines: [new SetRequestLine("sku-1")]));

    [Fact(Explicit = true)]
    public async Task ValidRequest_AllocationsAndTimePerCall()
    {
        // Arrange - generator shape: a parameters record with a [FromBody] member that has its own validator
        var services = new ServiceCollection();
        services.AddSingleton<IValidator<SetRequest>, SetRequestValidator>();
        var httpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        var filter = new ValidationFilter<SetParameters>();
        EndpointFilterDelegate next = _ => ValueTask.FromResult<object?>("Success");

        for (var i = 0; i < 1_000; i++)
        {
            await filter.InvokeAsync(new DefaultEndpointFilterInvocationContext(httpContext, ValidParameters), next);
        }

        // Act
        var (bytesPerCall, microsecondsPerCall) = await MeasureAsync(async () =>
            await filter.InvokeAsync(new DefaultEndpointFilterInvocationContext(httpContext, ValidParameters), next));

        var (lookupBytes, lookupMicroseconds) = await MeasureAsync(() =>
        {
            _ = WireNameValidationKeyMapper.GetFromBodyProperties(typeof(SetParameters)).ToList();
            return Task.CompletedTask;
        });

        // Assert - report the numbers (also written next to the test assembly, because passed tests hide their output)
        var report = string.Join(
            Environment.NewLine,
            $"ValidationFilter (valid request): {bytesPerCall:N0} bytes/call, {microsecondsPerCall:N2} µs/call",
            $"  of which [FromBody] lookup:     {lookupBytes:N0} bytes/call, {lookupMicroseconds:N2} µs/call");
        TestContext.Current.TestOutputHelper!.WriteLine(report);
        await File.WriteAllTextAsync(Path.Combine(AppContext.BaseDirectory, "validation-filter-hot-path.txt"), report, TestContext.Current.CancellationToken);
        bytesPerCall.Should().BePositive();
    }

    private static async Task<(long BytesPerCall, double MicrosecondsPerCall)> MeasureAsync(
        Func<Task> action)
    {
        var bytesBefore = GC.GetAllocatedBytesForCurrentThread();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        for (var i = 0; i < Iterations; i++)
        {
            await action();
        }

        stopwatch.Stop();
        var bytes = GC.GetAllocatedBytesForCurrentThread() - bytesBefore;

        return (bytes / Iterations, stopwatch.Elapsed.TotalMicroseconds / Iterations);
    }
}
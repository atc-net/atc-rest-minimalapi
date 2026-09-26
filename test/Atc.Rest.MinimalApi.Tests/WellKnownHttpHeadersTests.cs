extern alias MinimalApi;

namespace Atc.Rest.MinimalApi.Tests;

using WellKnownHttpHeaders = MinimalApi::Microsoft.AspNetCore.Http.WellKnownHttpHeaders;

public sealed class WellKnownHttpHeadersTests
{
    [Fact]
    public void StandardHeaders_HaveTheRfcNames()
    {
        using var scope = new AssertionScope();
        WellKnownHttpHeaders.IfMatch.Should().BeEquivalentTo(Microsoft.Net.Http.Headers.HeaderNames.IfMatch);
        WellKnownHttpHeaders.IfNoneMatch.Should().BeEquivalentTo(Microsoft.Net.Http.Headers.HeaderNames.IfNoneMatch);
        WellKnownHttpHeaders.ETag.Should().BeEquivalentTo(Microsoft.Net.Http.Headers.HeaderNames.ETag);
    }
}
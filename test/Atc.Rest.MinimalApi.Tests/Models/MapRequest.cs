namespace Atc.Rest.MinimalApi.Tests.Models;

public sealed record MapRequest(
    IDictionary<string, SetRequestLine> Map);
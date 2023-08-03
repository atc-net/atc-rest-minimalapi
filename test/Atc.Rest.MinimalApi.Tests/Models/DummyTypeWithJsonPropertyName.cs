namespace Atc.Rest.MinimalApi.Tests.Models;

public sealed record DummyTypeWithJsonPropertyName(
    [property: JsonPropertyName("record_property")]
    string RecordProperty);
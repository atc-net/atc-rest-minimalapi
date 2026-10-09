namespace Atc.Rest.MinimalApi.Tests.Filters.Endpoints;

/// <summary>
/// Edge cases of the key mapper that a request cannot reach easily (the validation behaviour suite covers the rest).
/// </summary>
public sealed class WireNameValidationKeyMapperTests
{
    [Fact]
    public void Map_EmptyKey_StaysEmpty()
    {
        // Act
        var result = Map<SetParameters>(isParametersWrapper: true, ("", "whole request"));

        // Assert
        result.Should().ContainKey(string.Empty).WhoseValue.Should().Equal("whole request");
    }

    [Fact]
    public void Map_WrapperKeyThatIsNoMember_IsKeptAsIs()
    {
        // Act - e.g. a FluentValidation OverridePropertyName on the parameters record
        var result = Map<SetParameters>(isParametersWrapper: true, ("Alias.Part", "m"));

        // Assert
        result.Keys.Should().Equal("Alias.Part");
    }

    [Theory]
    [InlineData("Route", "route-name")]
    [InlineData("Query", "query-name")]
    [InlineData("Header", "header-name")]
    [InlineData("Form", "form-name")]
    [InlineData("Unnamed", "Unnamed")]
    [InlineData("Plain", "Plain")]
    public void Map_WrapperMember_UsesItsBindingNameElseItsMemberName(
        string member,
        string expectedKey)
    {
        // Act
        var result = Map<BindingNamesParameters>(isParametersWrapper: true, (member, "m"));

        // Assert
        result.Keys.Should().Equal(expectedKey);
    }

    [Fact]
    public void Map_WithoutNamingPolicy_KeepsTheCSharpNames()
    {
        // Act - the app cleared the naming policy (PropertyNamingPolicy = null)
        var result = WireNameValidationKeyMapper.Map(
            typeof(SetRequest),
            isParametersWrapper: false,
            new Dictionary<string, string[]>(StringComparer.Ordinal) { ["FirstName"] = ["m"] },
            namingPolicy: null);

        // Assert
        result.Keys.Should().Equal("FirstName");
    }

    [Theory]
    [InlineData(typeof(OddBindAsyncParameters))]
    [InlineData(typeof(TaskBindAsyncParameters))]
    public void Map_MemberWhoseBindAsyncIsNotTheBindingContract_IsNotTheBody(
        Type parametersType)
    {
        // Act - BindAsync overloads with other parameters, or returning Task instead of ValueTask
        var result = WireNameValidationKeyMapper.Map(
            parametersType,
            isParametersWrapper: true,
            new Dictionary<string, string[]>(StringComparer.Ordinal) { ["Request.Name"] = ["m"] },
            JsonNamingPolicy.CamelCase);

        // Assert - the member segment stays
        result.Keys.Should().Equal("Request.name");
    }

    [Theory]
    [InlineData(typeof(BindAsyncBodyParameters), true)] // a self-binding body only
    [InlineData(typeof(QueryOnlyParameters), true)] // a binding attribute only
    [InlineData(typeof(SetRequest), false)] // a body type
    public void HasBindingSourceMembers(
        Type type,
        bool expected)
        => WireNameValidationKeyMapper.HasBindingSourceMembers(type).Should().Be(expected);

    private static Dictionary<string, string[]> Map<T>(
        bool isParametersWrapper,
        params (string Key, string Message)[] errors)
        => WireNameValidationKeyMapper.Map(
            typeof(T),
            isParametersWrapper,
            errors.ToDictionary(e => e.Key, e => new[] { e.Message }, StringComparer.Ordinal),
            JsonNamingPolicy.CamelCase);
}

public sealed record BindingNamesParameters(
    [property: FromRoute(Name = "route-name")] string? Route,
    [property: FromQuery(Name = "query-name")] string? Query,
    [property: FromHeader(Name = "header-name")] string? Header,
    [property: FromForm(Name = "form-name")] string? Form,
    [property: FromQuery] string? Unnamed,
    string? Plain);

public sealed record QueryOnlyParameters(
    [property: FromQuery(Name = "q")] string? Query);

public sealed record OddBindAsyncParameters(
    OddBindAsyncBody Request);

public sealed record TaskBindAsyncParameters(
    TaskBindAsyncBody Request);

public sealed record OddBindAsyncBody(
    string? Name)
{
    public static ValueTask<OddBindAsyncBody?> BindAsync(string value)
        => ValueTask.FromResult<OddBindAsyncBody?>(new OddBindAsyncBody(value));

    public static ValueTask<OddBindAsyncBody?> BindAsync(
        HttpContext context,
        string value)
        => ValueTask.FromResult<OddBindAsyncBody?>(new OddBindAsyncBody(value));

    public static ValueTask<OddBindAsyncBody?> BindAsync(
        string value,
        System.Reflection.ParameterInfo parameter)
        => ValueTask.FromResult<OddBindAsyncBody?>(new OddBindAsyncBody(value));

    public static ValueTask<OddBindAsyncBody?> BindAsync(
        HttpContext context,
        System.Reflection.ParameterInfo parameter,
        string value)
        => ValueTask.FromResult<OddBindAsyncBody?>(new OddBindAsyncBody(value));
}

public sealed record TaskBindAsyncBody(
    string? Name)
{
    public static Task<TaskBindAsyncBody?> BindAsync(HttpContext context)
        => Task.FromResult<TaskBindAsyncBody?>(null);
}
namespace Atc.Rest.MinimalApi.Tests;

/// <summary>
/// The APIs that use reflection by design carry the trimming attributes, so a trimmed or Native AOT app is warned at its
/// call site. Generated servers use none of them.
/// </summary>
public sealed class TrimAnnotationsTests
{
    [Theory]
    [InlineData(nameof(EndpointDefinitionExtensions.AddEndpointDefinitions))]
    [InlineData(nameof(EndpointDefinitionExtensions.AddEndpointAndServiceDefinitions))]
    public void AssemblyScanning_RequiresUnreferencedCode(string methodName)
        => typeof(EndpointDefinitionExtensions)
            .GetMethod(methodName)!
            .GetCustomAttributes(typeof(System.Diagnostics.CodeAnalysis.RequiresUnreferencedCodeAttribute), inherit: false)
            .Should()
            .ContainSingle();

    [Fact]
    public void UseEndpointDefinitions_HasNoTrimmingAttribute()
        => typeof(EndpointDefinitionExtensions)
            .GetMethod(nameof(EndpointDefinitionExtensions.UseEndpointDefinitions))!
            .GetCustomAttributes(typeof(System.Diagnostics.CodeAnalysis.RequiresUnreferencedCodeAttribute), inherit: false)
            .Should()
            .BeEmpty("it only reads the registered definitions from the services, without reflection");

    [Theory]
    [InlineData(typeof(SwaggerDefaultValues), true)]
    [InlineData(typeof(SwaggerEnumDescriptionsDocumentFilter), false)]
    public void SwaggerFilters_RequireUnreferencedCode(
        Type filterType,
        bool requiresDynamicCode)
    {
        // Assert
        filterType
            .GetCustomAttributes(typeof(System.Diagnostics.CodeAnalysis.RequiresUnreferencedCodeAttribute), inherit: false)
            .Should()
            .ContainSingle();
        filterType
            .GetCustomAttributes(typeof(System.Diagnostics.CodeAnalysis.RequiresDynamicCodeAttribute), inherit: false)
            .Should()
            .HaveCount(requiresDynamicCode ? 1 : 0);
    }
}
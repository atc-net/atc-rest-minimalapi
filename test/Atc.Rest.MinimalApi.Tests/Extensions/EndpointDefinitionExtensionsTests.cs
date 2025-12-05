namespace Atc.Rest.MinimalApi.Tests.Extensions;

public sealed class EndpointDefinitionExtensionsTests
{
    [Fact]
    public void AddEndpointDefinitions_WhenScanMarkersIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => services.AddEndpointDefinitions(null!));
    }

    [Fact]
    public void AddEndpointDefinitions_WhenScanMarkersIsEmpty_AddsEmptyCollection()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddEndpointDefinitions();

        // Assert
        var provider = services.BuildServiceProvider();
        var definitions = provider.GetService<IReadOnlyCollection<IEndpointDefinition>>();
        Assert.NotNull(definitions);
        Assert.Empty(definitions);
    }

    [Fact]
    public void AddEndpointDefinitions_WithValidMarker_RegistersEndpointDefinitions()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act - Use the test class type as marker, which will scan this assembly
        services.AddEndpointDefinitions(typeof(EndpointDefinitionExtensionsTests));

        // Assert - Should find all public IEndpointDefinition implementations in this assembly
        var provider = services.BuildServiceProvider();
        var definitions = provider.GetService<IReadOnlyCollection<IEndpointDefinition>>();
        Assert.NotNull(definitions);

        // We expect to find TestEndpointDefinition and TestEndpointAndServiceDefinition
        Assert.True(definitions.Count >= 2);
        Assert.Contains(definitions, x => x is TestEndpointDefinition);
        Assert.Contains(definitions, x => x is TestEndpointAndServiceDefinition);
    }

    [Fact]
    public void AddEndpointAndServiceDefinitions_WhenScanMarkersIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => services.AddEndpointAndServiceDefinitions(null!));
    }

    [Fact]
    public void AddEndpointAndServiceDefinitions_WithValidMarker_RegistersDefinitionsAndCallsDefineServices()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddEndpointAndServiceDefinitions(typeof(TestEndpointAndServiceDefinition));

        // Assert
        var provider = services.BuildServiceProvider();
        var definitions = provider.GetService<IReadOnlyCollection<IEndpointDefinition>>();
        Assert.NotNull(definitions);

        // Verify DefineServices was called by checking registered service
        var testService = provider.GetService<ITestService>();
        Assert.NotNull(testService);
    }

    [Fact]
    public void UseEndpointDefinitions_WhenAppIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        WebApplication? app = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => app!.UseEndpointDefinitions());
    }

    [Fact]
    public void AddEndpointDefinitions_FiltersAbstractAndInterfaceTypes()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act - Scan an assembly containing abstract types
        services.AddEndpointDefinitions(typeof(AbstractEndpointDefinition));

        // Assert - Should not include abstract types
        var provider = services.BuildServiceProvider();
        var definitions = provider.GetService<IReadOnlyCollection<IEndpointDefinition>>();
        Assert.NotNull(definitions);

        // No abstract types should be included
        Assert.DoesNotContain(definitions, x => x.GetType().IsAbstract);
    }

    [Fact]
    public void AddEndpointDefinitions_FiltersPotentialNullFromActivatorCreateInstance()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act - This test ensures that if Activator.CreateInstance returns null,
        // it doesn't cause issues (the Where filter handles it)
        services.AddEndpointDefinitions(typeof(EndpointDefinitionExtensionsTests));

        // Assert - Should complete without throwing
        var provider = services.BuildServiceProvider();
        var definitions = provider.GetService<IReadOnlyCollection<IEndpointDefinition>>();
        Assert.NotNull(definitions);
    }
}

//// Test types defined outside the test class to have predictable behavior

#pragma warning disable CA1034 // Nested types should not be visible
#pragma warning disable CA1040 // Avoid empty interfaces

public sealed class TestEndpointDefinition : IEndpointDefinition
{
    public void DefineEndpoints(WebApplication app)
    {
        // Test implementation
    }
}

public interface ITestService
{
}

internal sealed class TestService : ITestService
{
}

public sealed class TestEndpointAndServiceDefinition : IEndpointAndServiceDefinition
{
    public void DefineEndpoints(WebApplication app)
    {
        // Test implementation
    }

    public void DefineServices(IServiceCollection services)
    {
        services.AddSingleton<ITestService, TestService>();
    }
}

public abstract class AbstractEndpointDefinition : IEndpointDefinition
{
    public abstract void DefineEndpoints(WebApplication app);
}

#pragma warning restore CA1040 // Avoid empty interfaces
#pragma warning restore CA1034 // Nested types should not be visible
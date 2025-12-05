namespace Atc.Rest.MinimalApi.Tests.Filters.Swagger;

public sealed class SwaggerDefaultValuesTests
{
    [Fact]
    public void Apply_WhenOperationParametersIsNull_DoesNotThrow()
    {
        // Arrange
        var filter = new SwaggerDefaultValues();
        var operation = new OpenApiOperation
        {
            Parameters = null,
        };

        var apiDescription = CreateApiDescription();
        var context = CreateOperationFilterContext(apiDescription);

        // Act
        var exception = Record.Exception(() => filter.Apply(operation, context));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void Apply_WhenOperationResponsesIsNull_DoesNotThrow()
    {
        // Arrange
        var filter = new SwaggerDefaultValues();
        var operation = new OpenApiOperation
        {
            Responses = null,
        };

        var apiDescription = CreateApiDescription();
        var context = CreateOperationFilterContext(apiDescription);

        // Act
        var exception = Record.Exception(() => filter.Apply(operation, context));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void Apply_WhenResponseContentIsNull_DoesNotThrow()
    {
        // Arrange
        var filter = new SwaggerDefaultValues();
        var operation = new OpenApiOperation
        {
            Responses = new OpenApiResponses
            {
                ["200"] = new OpenApiResponse
                {
                    Content = null,
                },
            },
        };

        var apiDescription = CreateApiDescription();
        var context = CreateOperationFilterContext(apiDescription);

        // Act
        var exception = Record.Exception(() => filter.Apply(operation, context));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void Apply_SetsParameterRequiredFromDescription()
    {
        // Arrange
        var filter = new SwaggerDefaultValues();
        var operation = new OpenApiOperation
        {
            Parameters =
            [
                new OpenApiParameter
                {
                    Name = "testParam",
                    Required = false,
                },
            ],
        };

        var apiDescription = CreateApiDescription();
        apiDescription.ParameterDescriptions.Add(new ApiParameterDescription
        {
            Name = "testParam",
            IsRequired = true,
        });

        var context = CreateOperationFilterContext(apiDescription);

        // Act
        filter.Apply(operation, context);

        // Assert
        Assert.True(operation.Parameters[0].Required);
    }

    [Fact]
    public void Apply_WhenParameterDescriptionNotFound_DoesNotThrow()
    {
        // Arrange
        var filter = new SwaggerDefaultValues();
        var operation = new OpenApiOperation
        {
            Parameters =
            [
                new OpenApiParameter
                {
                    Name = "unknownParam",
                    Required = false,
                },
            ],
        };

        var apiDescription = CreateApiDescription();
        var context = CreateOperationFilterContext(apiDescription);

        // Act
        var exception = Record.Exception(() => filter.Apply(operation, context));

        // Assert
        Assert.Null(exception);
    }

    private static ApiDescription CreateApiDescription()
    {
        var apiDescription = new ApiDescription
        {
            ActionDescriptor = new ActionDescriptor
            {
                EndpointMetadata = [],
                RouteValues = new Dictionary<string, string?>(StringComparer.Ordinal),
            },
        };

        return apiDescription;
    }

    /// <summary>
    /// Creates a test OperationFilterContext with minimal required dependencies.
    /// The MethodInfo parameter is required by the constructor but not used in these tests.
    /// </summary>
    private static OperationFilterContext CreateOperationFilterContext(
        ApiDescription apiDescription)
        => new(
            apiDescription,
            Substitute.For<ISchemaGenerator>(),
            new SchemaRepository(),
            new OpenApiDocument(),
            typeof(SwaggerDefaultValuesTests).GetMethod(nameof(CreateApiDescription))!);
}
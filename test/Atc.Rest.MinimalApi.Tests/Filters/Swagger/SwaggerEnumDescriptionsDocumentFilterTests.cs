namespace Atc.Rest.MinimalApi.Tests.Filters.Swagger;

public sealed class SwaggerEnumDescriptionsDocumentFilterTests
{
    [Fact]
    public void Apply_WhenSwaggerDocIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        var filter = new SwaggerEnumDescriptionsDocumentFilter();
        var context = CreateDocumentFilterContext();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => filter.Apply(null!, context));
    }

    [Fact]
    public void Apply_WhenContextIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        var filter = new SwaggerEnumDescriptionsDocumentFilter();
        var swaggerDoc = new OpenApiDocument();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => filter.Apply(swaggerDoc, null!));
    }

    [Fact]
    public void Apply_WhenComponentsSchemasIsNull_DoesNotThrow()
    {
        // Arrange
        var filter = new SwaggerEnumDescriptionsDocumentFilter();
        var swaggerDoc = new OpenApiDocument
        {
            Components = null,
        };
        var context = CreateDocumentFilterContext();

        // Act
        var exception = Record.Exception(() => filter.Apply(swaggerDoc, context));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void Apply_WhenSchemasIsEmpty_DoesNotThrow()
    {
        // Arrange
        var filter = new SwaggerEnumDescriptionsDocumentFilter();
        var swaggerDoc = new OpenApiDocument
        {
            Components = new OpenApiComponents(),
        };
        var context = CreateDocumentFilterContext();

        // Act
        var exception = Record.Exception(() => filter.Apply(swaggerDoc, context));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void Apply_WithEnumSchema_DoesNotThrow()
    {
        // Arrange
        var filter = new SwaggerEnumDescriptionsDocumentFilter();
        var swaggerDoc = new OpenApiDocument
        {
            Components = new OpenApiComponents
            {
                Schemas = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal)
                {
                    ["TestEnum"] = new OpenApiSchema
                    {
                        Enum =
                        [
                            JsonValue.Create(0),
                            JsonValue.Create(1),
                        ],
                    },
                },
            },
            Paths = new OpenApiPaths(),
        };

        var context = CreateDocumentFilterContext();

        // Act
        var exception = Record.Exception(() => filter.Apply(swaggerDoc, context));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void Apply_WithEmptyPaths_DoesNotThrow()
    {
        // Arrange
        var filter = new SwaggerEnumDescriptionsDocumentFilter();
        var swaggerDoc = new OpenApiDocument
        {
            Components = new OpenApiComponents(),
            Paths = new OpenApiPaths(),
        };
        var context = CreateDocumentFilterContext();

        // Act
        var exception = Record.Exception(() => filter.Apply(swaggerDoc, context));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void Apply_WithPathOperationsHavingNullParameters_DoesNotThrow()
    {
        // Arrange
        var filter = new SwaggerEnumDescriptionsDocumentFilter();
        var swaggerDoc = new OpenApiDocument
        {
            Components = new OpenApiComponents(),
            Paths = new OpenApiPaths
            {
                ["/test"] = new OpenApiPathItem
                {
                    Operations = new Dictionary<HttpMethod, OpenApiOperation>
                    {
                        [HttpMethod.Get] = new OpenApiOperation
                        {
                            Parameters = null,
                        },
                    },
                },
            },
        };
        var context = CreateDocumentFilterContext();

        // Act
        var exception = Record.Exception(() => filter.Apply(swaggerDoc, context));

        // Assert
        Assert.Null(exception);
    }

    private static DocumentFilterContext CreateDocumentFilterContext()
        => new(
            [],
            Substitute.For<ISchemaGenerator>(),
            new SchemaRepository());
}
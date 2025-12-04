namespace Atc.Rest.MinimalApi.Filters.Swagger;

/// <summary>
/// Represents a document filter for Swagger/Swashbuckle that adds detailed descriptions for enumerations.
/// This class ensures that the generated Swagger documentation includes textual descriptions for enum values,
/// both for result models and input parameters, enhancing the readability and understanding of the API schema.
/// </summary>
public class SwaggerEnumDescriptionsDocumentFilter : IDocumentFilter
{
    /// <summary>
    /// Applies the filter to the specified Swagger document, enhancing enum descriptions in the schema components and operations.
    /// </summary>
    /// <param name="swaggerDoc">The Swagger document to modify.</param>
    /// <param name="context">The context containing information about the API, such as descriptions and metadata.</param>
    /// <remarks>
    /// This method iterates through the schema components and operations to identify enums, and appends their descriptions.
    /// </remarks>
    [SuppressMessage("Minor Code Smell", "S1643:Strings should not be concatenated using '+' in a loop", Justification = "OK. For now.")]
    public void Apply(
        OpenApiDocument swaggerDoc,
        DocumentFilterContext context)
    {
        ArgumentNullException.ThrowIfNull(swaggerDoc);
        ArgumentNullException.ThrowIfNull(context);

        // Add enum descriptions to result models
        if (swaggerDoc.Components?.Schemas is null)
        {
            return;
        }

        foreach (var item in swaggerDoc.Components.Schemas.Where(x => x.Value?.Enum?.Count > 0))
        {
            var propertyEnums = item.Value.Enum;
            if (propertyEnums is not null && propertyEnums.Count > 0)
            {
                item.Value.Description += DescribeEnum(propertyEnums, item.Key);
            }
        }

        // Add enum descriptions to input parameters
        foreach (var item in swaggerDoc.Paths)
        {
            DescribeEnumParameters(item.Value.Operations, swaggerDoc, context.ApiDescriptions, item.Key);
        }
    }

    /// <summary>
    /// Describes the enum parameters for a set of operations, appending descriptions to the parameters in the Swagger document.
    /// </summary>
    /// <param name="operations">The operations to inspect.</param>
    /// <param name="document">The Swagger document being modified.</param>
    /// <param name="apiDescriptions">The API descriptions to refer to.</param>
    /// <param name="path">The path of the operations.</param>
    [SuppressMessage("Minor Code Smell", "S1643:Strings should not be concatenated using '+' in a loop", Justification = "OK.")]
    private static void DescribeEnumParameters(
        IDictionary<HttpMethod, OpenApiOperation>? operations,
        OpenApiDocument document,
        IEnumerable<ApiDescription> apiDescriptions,
        string path)
    {
        if (operations is null)
        {
            return;
        }

        path = path.Trim('/');

        var pathDescriptions = apiDescriptions
            .Where(a => string.Equals(a.RelativePath, path, StringComparison.Ordinal))
            .ToList();

        foreach (var operation in operations)
        {
            if (operation.Value.Parameters is null)
            {
                continue;
            }

            var operationDescription = pathDescriptions.Find(a => a.HttpMethod is not null && a.HttpMethod.Equals(operation.Key.Method, StringComparison.OrdinalIgnoreCase));
            foreach (var param in operation.Value.Parameters)
            {
                var parameterDescription = operationDescription?.ParameterDescriptions.FirstOrDefault(a => string.Equals(a.Name, param.Name, StringComparison.Ordinal));

                if (parameterDescription?.Type is null)
                {
                    continue;
                }

                if (!parameterDescription.Type.TryGetEnumType(out var enumType))
                {
                    continue;
                }

                if (document.Components?.Schemas is null)
                {
                    continue;
                }

                var paramEnum = document.Components.Schemas.FirstOrDefault(x => string.Equals(x.Key, enumType.Name, StringComparison.Ordinal));
                if (paramEnum.Value?.Enum is not null)
                {
                    param.Description += DescribeEnum(paramEnum.Value.Enum, paramEnum.Key);
                }
            }
        }
    }

    /// <summary>
    /// Constructs a description for a given enum, enumerating its values and names.
    /// </summary>
    /// <param name="enums">The enum values.</param>
    /// <param name="propertyTypeName">The name of the property type containing the enum.</param>
    /// <returns>A formatted string describing the enum values and names.</returns>
    private static string DescribeEnum(
        IEnumerable<JsonNode> enums,
        string propertyTypeName)
    {
        var enumDescriptions = new List<string>();
        var enumType = GetEnumTypeByName(propertyTypeName);
        if (enumType is null)
        {
            return string.Empty;
        }

        foreach (var item in enums)
        {
            if (item is JsonValue jsonValue)
            {
                if (jsonValue.TryGetValue<int>(out var intValue))
                {
                    enumDescriptions.Add($"{intValue} = {Enum.GetName(enumType, intValue)}");
                }
                else if (jsonValue.TryGetValue<string>(out var stringValue))
                {
                    var enumInt = (int)Enum.Parse(enumType, stringValue);
                    enumDescriptions.Add($"{enumInt} = {stringValue}");
                }
            }
        }

        return string.Join("<br />", enumDescriptions.ToArray());
    }

    /// <summary>
    /// Retrieves the enum type by its name from the current application domain.
    /// </summary>
    /// <param name="enumTypeName">The name of the enum type to find.</param>
    /// <returns>The enum type, if found; otherwise, <c>null</c>.</returns>
    private static Type? GetEnumTypeByName(string enumTypeName)
        => AppDomain.CurrentDomain
            .GetAssemblies()
            .SelectMany(x => x
                .GetTypes()
                .Where(t => t.IsEnum))
            .Where(x => string.Equals(x.Name, enumTypeName, StringComparison.Ordinal))
            .ToArray()
            switch
            {
                { Length: 1 } a => a[0],
                _ => null,
            };
}
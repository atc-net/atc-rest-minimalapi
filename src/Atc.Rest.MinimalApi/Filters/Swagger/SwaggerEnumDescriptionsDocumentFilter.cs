namespace Atc.Rest.MinimalApi.Filters.Swagger;

/// <summary>
/// Represents a document filter for Swagger/Swashbuckle that adds detailed descriptions for enumerations.
/// This class ensures that the generated Swagger documentation includes textual descriptions for enum values,
/// both for result models and input parameters, enhancing the readability and understanding of the API schema.
/// </summary>
public class SwaggerEnumDescriptionsDocumentFilter : IDocumentFilter
{
    /// <summary>
    /// Thread-local mapping from schema ID to CLR Type, built from DocumentFilterContext.
    /// This provides accurate type resolution even when multiple assemblies define enums with the same name.
    /// </summary>
    [ThreadStatic]
    private static Dictionary<string, Type>? schemaIdToType;

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

        // Build schema-to-type mapping from DocumentFilterContext for accurate type resolution
        BuildSchemaToTypeMapping(context);

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
    /// Builds a mapping from schema IDs to CLR Types by analyzing the API descriptions.
    /// This approach ensures accurate type resolution by using the actual types Swashbuckle registered.
    /// </summary>
    /// <param name="context">The document filter context containing API descriptions and schema repository.</param>
    private static void BuildSchemaToTypeMapping(DocumentFilterContext context)
    {
        schemaIdToType = new Dictionary<string, Type>(StringComparer.Ordinal);
        var visitedTypes = new HashSet<Type>();

        foreach (var apiDescription in context.ApiDescriptions)
        {
            foreach (var responseType in apiDescription.SupportedResponseTypes)
            {
                if (responseType.Type is not null)
                {
                    RegisterTypeAndNestedTypes(responseType.Type, context, visitedTypes);
                }
            }

            foreach (var parameter in apiDescription.ParameterDescriptions)
            {
                if (parameter.Type is not null)
                {
                    RegisterTypeAndNestedTypes(parameter.Type, context, visitedTypes);
                }
            }
        }
    }

    /// <summary>
    /// Recursively registers a type and its nested types (properties, generic arguments) in the schema-to-type mapping.
    /// </summary>
    /// <param name="type">The type to register.</param>
    /// <param name="context">The document filter context.</param>
    /// <param name="visitedTypes">Set of already processed types to prevent infinite recursion.</param>
    private static void RegisterTypeAndNestedTypes(
        Type type,
        DocumentFilterContext context,
        HashSet<Type> visitedTypes)
    {
        // Handle nullable types
        var underlyingType = Nullable.GetUnderlyingType(type) ?? type;

        // Prevent infinite recursion for circular type references
        if (!visitedTypes.Add(underlyingType))
        {
            return;
        }

        // Handle generic types (List<T>, Dictionary<K,V>, etc.)
        if (underlyingType.IsGenericType)
        {
            foreach (var genericArg in underlyingType.GetGenericArguments())
            {
                RegisterTypeAndNestedTypes(genericArg, context, visitedTypes);
            }
        }

        // Handle array types
        if (underlyingType.IsArray && underlyingType.GetElementType() is { } elementType)
        {
            RegisterTypeAndNestedTypes(elementType, context, visitedTypes);
        }

        // Register enum types - use type name as schema ID (Swashbuckle's default behavior)
        // TryLookupByType may fail if the type was registered with a different instance,
        // so we fall back to using the simple type name as the schema ID
        if (underlyingType.IsEnum)
        {
            var schemaId = underlyingType.Name;
            if (context.SchemaRepository.TryLookupByType(underlyingType, out var reference) &&
                reference.Id is not null)
            {
                schemaId = reference.Id;
            }

            schemaIdToType!.TryAdd(schemaId, underlyingType);
        }

        // Register properties of complex types (for nested enums in DTOs)
        if (underlyingType.IsClass && underlyingType != typeof(string))
        {
            foreach (var property in underlyingType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                RegisterTypeAndNestedTypes(property.PropertyType, context, visitedTypes);
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
    /// Retrieves the enum type by its name (schema ID), using the schema-to-type mapping
    /// built from the DocumentFilterContext for accurate resolution.
    /// </summary>
    /// <param name="enumTypeName">The name of the enum type (schema ID) to find.</param>
    /// <returns>The enum type, if found; otherwise, <c>null</c>.</returns>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "GetTypes() can throw various exceptions for dynamic assemblies")]
    private static Type? GetEnumTypeByName(string enumTypeName)
    {
        // Primary: Use schema-to-type mapping (most accurate)
        if (schemaIdToType is not null && schemaIdToType.TryGetValue(enumTypeName, out var mappedType))
        {
            return mappedType;
        }

        // Fallback: AppDomain scan for edge cases (e.g., enums not in API descriptions)
        var enumTypes = AppDomain.CurrentDomain
            .GetAssemblies()
            .SelectMany(assembly =>
            {
                try
                {
                    return assembly
                        .GetTypes()
                        .Where(t => t.IsEnum);
                }
                catch (ReflectionTypeLoadException)
                {
                    return [];
                }
            })
            .Where(x => string.Equals(x.Name, enumTypeName, StringComparison.Ordinal))
            .ToArray();

        // Only return if exactly one match (to avoid ambiguity)
        return enumTypes.Length == 1 ? enumTypes[0] : null;
    }
}
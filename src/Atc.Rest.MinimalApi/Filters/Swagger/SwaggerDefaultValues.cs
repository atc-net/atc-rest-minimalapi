namespace Atc.Rest.MinimalApi.Filters.Swagger;

/// <summary>
/// Represents the Swagger/Swashbuckle operation filter used to document the implicit API version parameter.
/// This class is used to modify the Swagger documentation by defining defaults for various properties,
/// including operation descriptions, summaries, deprecation status, response types, and parameters.
/// </summary>
/// <remarks>
/// This <see cref="IOperationFilter"/> is only required due to bugs in the <see cref="SwaggerGenerator"/>.
/// Once they are fixed and published, this class can be removed.
/// </remarks>
public class SwaggerDefaultValues : IOperationFilter
{
    /// <inheritdoc />
    /// <summary>
    /// Applies the filter to the specified operation, modifying the properties based on the metadata and other characteristics of the API.
    /// </summary>
    /// <param name="operation">The operation to be modified.</param>
    /// <param name="context">The context containing information about the API, such as the descriptions, metadata, and supported response types.</param>
    /// <remarks>
    /// This method addresses several issues and shortcomings related to Swagger documentation generation,
    /// and it references specific issues and sections of code in the Swashbuckle library. The method achieves the following:
    /// - Populates the operation description and summary from the endpoint metadata if they are not set.
    /// - Marks the operation as deprecated if it is marked as such in the API description.
    /// - Modifies the response types, removing unnecessary content types.
    /// - Adjusts the operation parameters based on the API description, including setting default values, descriptions, and required flags.
    /// </remarks>
    public void Apply(
        OpenApiOperation operation,
        OperationFilterContext context)
    {
        var apiDescription = context.ApiDescription;

        var metadata = apiDescription.ActionDescriptor.EndpointMetadata;

        operation.Description ??= metadata
            .OfType<IEndpointDescriptionMetadata>()
            .FirstOrDefault()?.Description;
        operation.Summary ??= metadata
            .OfType<IEndpointSummaryMetadata>()
            .FirstOrDefault()?.Summary;

        // REF: https://github.com/domaindrivendev/Swashbuckle.AspNetCore/issues/1752#issue-663991077
        if (operation.Responses is not null)
        {
            foreach (var responseType in context.ApiDescription.SupportedResponseTypes)
            {
                // REF: https://github.com/domaindrivendev/Swashbuckle.AspNetCore/blob/b7cf75e7905050305b115dd96640ddd6e74c7ac9/src/Swashbuckle.AspNetCore.SwaggerGen/SwaggerGenerator/SwaggerGenerator.cs#L383-L387
                var responseKey = responseType.IsDefaultResponse
                    ? "default"
                    : responseType.StatusCode.ToString(GlobalizationConstants.EnglishCultureInfo);

                if (!operation.Responses.TryGetValue(responseKey, out var response) ||
                    response.Content is null)
                {
                    continue;
                }

                foreach (var contentType in response.Content.Keys)
                {
                    if (responseType.ApiResponseFormats.All(x => x.MediaType != contentType))
                    {
                        response.Content.Remove(contentType);
                    }
                }
            }
        }

        if (operation.Parameters == null)
        {
            return;
        }

        // REF: https://github.com/domaindrivendev/Swashbuckle.AspNetCore/issues/412
        // REF: https://github.com/domaindrivendev/Swashbuckle.AspNetCore/pull/413
        foreach (var parameter in operation.Parameters)
        {
            var description = apiDescription.ParameterDescriptions.FirstOrDefault(p => p.Name == parameter.Name);
            if (description is null)
            {
                continue;
            }

            parameter.Description ??= description.ModelMetadata?.Description;

            // Cast to concrete types to modify properties in Microsoft.OpenApi v2.x
            if (parameter.Schema is OpenApiSchema schema &&
                schema.Default == null &&
                description.DefaultValue != null &&
                description.DefaultValue is not DBNull &&
                description.ModelMetadata is { } modelMetadata)
            {
                // REF: https://github.com/Microsoft/aspnet-api-versioning/issues/429#issuecomment-605402330
                var json = JsonSerializer.Serialize(description.DefaultValue, modelMetadata.ModelType);
                var defaultValue = JsonNode.Parse(json);
                if (defaultValue is not null)
                {
                    schema.Default = defaultValue;
                }
            }

            if (parameter is OpenApiParameter concreteParam)
            {
                concreteParam.Required |= description.IsRequired;
            }
        }
    }
}
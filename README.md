[![NuGet Version](https://img.shields.io/nuget/v/atc.rest.minimalapi.svg?logo=nuget&style=for-the-badge)](https://www.nuget.org/packages/atc.rest.minimalapi)

# Atc.Rest.MinimalApi

Modern development demands efficiency, clarity, and flexibility, especially when it comes to building RESTful APIs. The Atc.Rest.MinimalApi library is crafted with these principles in mind, offering a comprehensive collection of components specifically designed to streamline API development.

From EndpointDefinitions that standardize and automate endpoint registration to intricate ValidationFilters that ensure data integrity, the components within this package encapsulate best practices in a way that's accessible and customizable. With additional features for error handling, versioning, and enhanced Swagger documentation, the library provides a robust foundation for creating scalable and maintainable APIs.

Whether you're building a brand-new project or seeking to enhance an existing one, Atc.Rest.MinimalApi can significantly simplify development, allowing you to focus on what truly matters: delivering value to your users.

# Table of Contents
- [Atc.Rest.MinimalApi](#atcrestminimalapi)
- [Table of Contents](#table-of-contents)
  - [Automatic endpoint discovery and registration](#automatic-endpoint-discovery-and-registration)
  - [SwaggerFilters](#swaggerfilters)
    - [`SwaggerDefaultValues`](#swaggerdefaultvalues)
    - [`SwaggerEnumDescriptionsDocumentFilter`](#swaggerenumdescriptionsdocumentfilter)
  - [Validation](#validation)
  - [Middleware](#middleware)
    - [GlobalErrorHandlingMiddleware](#globalerrorhandlingmiddleware)
- [Sample Project](#sample-project)
- [Requirements](#requirements)
- [How to contribute](#how-to-contribute)

## Automatic endpoint discovery and registration

In modern API development, maintaining consistency and automation in endpoint registration is paramount. Utilizing an interface like `IEndpointDefinition` can automate the process, seamlessly incorporating all endpoints within the API into the Dependency Container. By inheriting from this interface in your endpoints, you enable a systematic orchestration for automatic registration, as illustrated in the subsequent examples.

This approach simplifies configuration, ensures uniformity across endpoints, and fosters a more maintainable codebase.

```csharp
public interface IEndpointDefinition
{
    void DefineEndpoints(
        WebApplication app);
}
```

Upon defining all endpoints and ensuring they inherit from the specified interface, the process of automatic registration can be systematically orchestrated and configured in the subsequent manner.

```csharp
var builder = WebApplication.CreateBuilder(args);

/// Adds the endpoint definitions to the specified service collection by scanning the assemblies of the provided marker types.
/// In this example the empty assembly marker interface IApiContractAssemblyMarker defined in the project where EndpointDefinitions reside.
/// This method looks for types that implement the IEndpointDefinition interface and are neither abstract nor an interface,
/// and adds them to the service collection as a single instance of IReadOnlyCollection{IEndpointDefinition}.
builder.Services.AddEndpointDefinitions(typeof(IApiContractAssemblyMarker));

var app = builder.Build();

/// Applies the endpoint definitions to the specified web application.
/// This method retrieves the registered endpoint definitions from the application's services and invokes their "IEndpointDefinition.DefineEndpoints" method.
app.UseEndpointDefinitions();
```

An example of how to configure an endpoint upon inheriting from the specified interface.

```csharp
public sealed class UsersEndpointDefinition : IEndpointDefinition
{
    internal const string ApiRouteBase = "/api/users";

    public void DefineEndpoints(
        WebApplication app)
    {
        var users = app.NewVersionedApi("Users");

        var usersV1 = users
            .MapGroup(ApiRouteBase)
            .HasApiVersion(1.0);

        usersV1
            .MapGet("/", GetAllUsers)
            .WithName("GetAllUsers");
    }

    internal Task<Ok<IEnumerable<User>>> GetAllUsers(
        [FromServices] IGetUsersHandler handler,
        CancellationToken cancellationToken)
        => handler.ExecuteAsync(cancellationToken);
}
```

## SwaggerFilters

In the development of RESTful APIs, filters play an essential role in shaping the output and behavior of the system. Whether it's providing detailed descriptions for enumerations or handling default values and response formats, filters like those in the Atc.Rest.MinimalApi.Filters.Swagger namespace enhance the API's functionality and documentation, aiding in both development and integration.

| Filter Name                          | Summary                                                                                                   | Remarks                                                                                                                        |
|--------------------------------------|-----------------------------------------------------------------------------------------------------------|--------------------------------------------------------------------------------------------------------------------------------|
| SwaggerDefaultValues                 | This class is an operation filter for Swagger/Swashbuckle to document the implicit API version parameter. | This filter is only required due to specific bugs in the SwaggerGenerator. Once fixed and published, this class can be removed.|
| SwaggerEnumDescriptionsDocumentFilter| This class is a document filter to handle and describe enumerations within the Swagger documentation.     | This filter enhances the Swagger documentation by incorporating detailed descriptions for enumerated types in the SwaggerUI.   |

### `SwaggerDefaultValues`

| Feature                       | Description                                                                      |
|-------------------------------|----------------------------------------------------------------------------------|
| Description and Summary       | Applies the endpoint's description and summary metadata if available.            |
| Deprecation Handling          | Marks an operation as deprecated if applicable.                                  |
| Response Types Handling       | Adjusts response content types based on the API's supported response types.      |
| Parameter Handling            | Adjusts the description, default values, and required attributes for parameters. |

### `SwaggerEnumDescriptionsDocumentFilter`

| Feature                            | Description                                                                                            |
|------------------------------------|--------------------------------------------------------------------------------------------------------|
| Enum Descriptions in Result Models | Adds descriptions for enums within result models.                                                      |
| Enum Descriptions in Input Params  | Appends descriptions to input parameters that are of an enumeration type.                              |
| Enum Descriptions Construction     | Constructs a string description for enums, listing integer values along with their corresponding names.|

An example of how to configure the swagger filters when adding Swagger generation to the API.

```csharp
services.AddSwaggerGen(options =>
{
    options.OperationFilter<SwaggerDefaultValues>();
    options.DocumentFilter<SwaggerEnumDescriptionsDocumentFilter>();
});
```

## Validation

Enhance your Minimal API with powerful validation using the `ValidationFilter<T>` class. This filter integrates both DataAnnotations validation and FluentValidation, combining and deduplicating errors into a cohesive validation response.

Out of the box, the filter can be applied as shown below:

```csharp
usersV1
    .MapPost("/", CreateUser)
    .WithName(Names.UserDefinitionNames.CreateUser)
    .WithDescription("Create user.")
    .WithSummary("Create user.")
    .AddEndpointFilter<ValidationFilter<CreateUserParameters>>()
    .ProducesValidationProblem();
```

At times, you may find it necessary to manipulate the serialized type names for validation keys/values, or to dictate the naming conventions of these keys/values.

With the `ValidationFilterOptions` class, you can customize the filter's behavior to suit your needs, such as opting to skip the first level when resolving serialization type names for validation keys/values.

The filter can be configured in two distinct ways:

1. Locally injected ValidationFilterOptions

```csharp
usersV1
    .MapPost("/", CreateUser)
    .WithName(Names.UserDefinitionNames.CreateUser)
    .WithDescription("Create user.")
    .WithSummary("Create user.")
    .AddEndpointFilter(new ValidationFilter<UpdateUserByIdParameters>(new ValidationFilterOptions
    {
        SkipFirstLevelOnValidationKeys = true,
    }))
    .ProducesValidationProblem();
```

2. Alternatively, you can use the following method, where the `ValidationFilterOptions` have been registered in the dependency container earlier in the pipeline globally for all endpoints.

```csharp
builder.services.AddSingleton(_ => new ValidationFilterOptions
{
    SkipFirstLevelOnValidationKeys = true,
});

usersV1
    .MapPost("/", CreateUser)
    .WithName(Names.UserDefinitionNames.CreateUser)
    .WithDescription("Create user.")
    .WithSummary("Create user.")
    .AddEndpointFilter<ValidationFilter<CreateUserParameters>>()
    .ProducesValidationProblem();
```

## Middleware

### GlobalErrorHandlingMiddleware

The `GlobalErrorHandlingMiddleware` class provides a mechanism for handling uncaught exceptions globally across an ASP.NET Core application.

This middleware helps in capturing any unhandled exceptions that occur during the request processing pipeline. It translates different types of exceptions into the appropriate HTTP status codes and responds with a standardized error message.

An example of how to configure the middleware.

```csharp
var app = builder.Build();
app.UseMiddleware<GlobalErrorHandlingMiddleware>();
```

# Sample Project

The sample project `Demo.Api` located in the [sample](/sample/) folder within the repository illustrates a practical implementation of the Atc.Rest.MinimalApi package, showcasing all the features and best practices detailed in this documentation. It's a comprehensive starting point for those looking to get a hands-on understanding of how to effectively utilize the library in real-world applications.

The Demo.Api project also leverages the `Asp.Versioning.Http` Nuget package to establish a proper versioning scheme. It's an example implementation of how API versioning can be easily managed and maintained.

# Requirements

* [.NET 7 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/7.0)

# How to contribute

[Contribution Guidelines](https://atc-net.github.io/introduction/about-atc#how-to-contribute)

[Coding Guidelines](https://atc-net.github.io/introduction/about-atc#coding-guidelines)

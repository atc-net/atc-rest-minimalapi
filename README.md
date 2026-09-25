[![NuGet Version](https://img.shields.io/nuget/v/atc.rest.minimalapi.svg?logo=nuget&style=for-the-badge)](https://www.nuget.org/packages/atc.rest.minimalapi)

# 🚀 Atc.Rest.MinimalApi

Modern development demands efficiency, clarity, and flexibility, especially when it comes to building RESTful APIs. The Atc.Rest.MinimalApi library is crafted with these principles in mind, offering a comprehensive collection of components specifically designed to streamline API development.

From EndpointDefinitions that standardize and automate endpoint registration to intricate ValidationFilters that ensure data integrity, the components within this package encapsulate best practices in a way that's accessible and customizable. With additional features for error handling, versioning, and enhanced Swagger documentation, the library provides a robust foundation for creating scalable and maintainable APIs.

Whether you're building a brand-new project or seeking to enhance an existing one, Atc.Rest.MinimalApi can significantly simplify development, allowing you to focus on what truly matters: delivering value to your users.

# 📑 Table of Contents
- [🚀 Atc.Rest.MinimalApi](#-atcrestminimalapi)
- [📑 Table of Contents](#-table-of-contents)
- [🔌 Automatic endpoint discovery and registration](#-automatic-endpoint-discovery-and-registration)
- [🔌 Automatic endpoint discovery and registration with services](#-automatic-endpoint-discovery-and-registration-with-services)
- [📝 SwaggerFilters](#-swaggerfilters)
  - [`SwaggerDefaultValues`](#swaggerdefaultvalues)
  - [`SwaggerEnumDescriptionsDocumentFilter`](#swaggerenumdescriptionsdocumentfilter)
- [✅ Validation](#-validation)
  - [Validation Approaches](#validation-approaches)
  - [Nested [FromBody] Validator Support 🎯](#nested-frombody-validator-support-)
  - [When the argument to validate is missing](#when-the-argument-to-validate-is-missing)
- [📚 API Documentation](#-api-documentation)
  - [Swagger UI](#swagger-ui)
  - [Scalar](#scalar)
- [🛡️ Middleware](#️-middleware)
  - [GlobalErrorHandlingMiddleware](#globalerrorhandlingmiddleware)
- [💡 Sample Project](#-sample-project)
- [📋 Requirements](#-requirements)
- [🤝 How to contribute](#-how-to-contribute)

# 🔌 Automatic endpoint discovery and registration

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
/// This method retrieves the registered endpoint definitions from the application's services and invokes
/// their <see cref="IEndpointDefinition.DefineEndpoints"/> and/or <see cref="IEndpointAndServiceDefinition.DefineEndpoints"/> method.
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

# 🔌 Automatic endpoint discovery and registration with services

An alternative approach is using the interface `IEndpointAndServiceDefinition`.

```csharp
public interface IEndpointAndServiceDefinition : IEndpointDefinition
{
    void DefineServices(
        IServiceCollection services);
}
```

Upon defining all endpoints and ensuring they inherit from the specified interface, the process of automatic registration can be systematically orchestrated and configured in the subsequent manner.

```csharp
var builder = WebApplication.CreateBuilder(args);

/// Adds the endpoint definitions to the specified service collection by scanning the assemblies of the provided marker types.
/// This method looks for types that implement the <see cref="IEndpointDefinition"/> and <see cref="IEndpointAndServiceDefinition"/>
/// interface and are neither abstract nor an interface,
/// and adds them to the service collection as a single instance of <see cref="IReadOnlyCollection{IEndpointDefinition}"/>
/// and <see cref="IReadOnlyCollection{IEndpointAndServiceDefinition}"/>.
builder.Services.AddEndpointAndServiceDefinitions(typeof(IApiContractAssemblyMarker));

var app = builder.Build();

/// Applies the endpoint definitions to the specified web application.
/// This method retrieves the registered endpoint definitions from the application's services and invokes
/// their <see cref="IEndpointDefinition.DefineEndpoints"/> and/or <see cref="IEndpointAndServiceDefinition.DefineEndpoints"/> method.
app.UseEndpointDefinitions();
```

An example of how to configure an endpoint upon inheriting from the specified interface.

```csharp
public sealed class UsersEndpointDefinition : IEndpointAndServiceDefinition
{
    internal const string ApiRouteBase = "/api/users";

    public void DefineServices(
        IServiceCollection services)
    {
        services.AddScoped<IUserService, UserService>();
    }

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
        [FromServices] IUserService userService,
        CancellationToken cancellationToken)
        => userService.GetAllUsers(cancellationToken);
}
```

# 📝 SwaggerFilters

In the development of RESTful APIs, filters play an essential role in shaping the output and behavior of the system. Whether it's providing detailed descriptions for enumerations or handling default values and response formats, filters like those in the Atc.Rest.MinimalApi.Filters.Swagger namespace enhance the API's functionality and documentation, aiding in both development and integration.

| Filter Name                          | Summary                                                                                                   | Remarks                                                                                                                        |
|--------------------------------------|-----------------------------------------------------------------------------------------------------------|--------------------------------------------------------------------------------------------------------------------------------|
| SwaggerDefaultValues                 | This class is an operation filter for Swagger/Swashbuckle to document the implicit API version parameter. | This filter is only required due to specific bugs in the SwaggerGenerator. Once fixed and published, this class can be removed.|
| SwaggerEnumDescriptionsDocumentFilter| This class is a document filter to handle and describe enumerations within the Swagger documentation.     | This filter enhances the Swagger documentation by incorporating detailed descriptions for enumerated types in the SwaggerUI.   |

## `SwaggerDefaultValues`

| Feature                       | Description                                                                      |
|-------------------------------|----------------------------------------------------------------------------------|
| Description and Summary       | Applies the endpoint's description and summary metadata if available.            |
| Deprecation Handling          | Marks an operation as deprecated if applicable.                                  |
| Response Types Handling       | Adjusts response content types based on the API's supported response types.      |
| Parameter Handling            | Adjusts the description, default values, and required attributes for parameters. |

## `SwaggerEnumDescriptionsDocumentFilter`

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

# ✅ Validation

Enhance your Minimal API with powerful validation using the `ValidationFilter<T>` class. This filter integrates both DataAnnotations validation and FluentValidation, combining and deduplicating errors into a cohesive validation response.

## Validation Approaches

When building APIs with .NET 10, you have two options for validation:

### Option 1: Atc.Rest.MinimalApi ValidationFilter (Recommended) ⭐

This library provides a `ValidationFilter<T>` that works on .NET 10.

**✅ Pros:**
- **Unified error responses** - Merges DataAnnotations and FluentValidation errors into a single response
- **Smart error keys** - Respects `[JsonPropertyName]` attributes for serialization-aware error keys
- **Consistent behavior** - Reliable validation logic on .NET 10
- **No additional configuration** - Works out of the box without project file changes
- **Error deduplication** - Automatically removes duplicate validation errors
- **Custom validation attributes** - Full support for custom `ValidationAttribute` implementations
- **Nested validator discovery** - Automatically finds validators for `[FromBody]` properties

**Usage:**
```csharp
usersV1
    .MapPost("/", CreateUser)
    .WithName("CreateUser")
    .AddEndpointFilter<ValidationFilter<CreateUserParameters>>()
    .ProducesValidationProblem();
```

### Option 2: .NET 10 Native Validation

.NET 10 introduces built-in validation via `services.AddValidation()` with source generators.

**✅ Pros:**
- Built into the framework (no additional packages)
- Source generator approach (compile-time validation discovery)

**⚠️ Cons:**
- Requires `InterceptorsNamespaces` configuration in your project file
- Short-circuits on first validation failure (cannot merge with FluentValidation errors)
- Only available on .NET 10+
- Less control over error key formatting

**Configuration required in .csproj:**
```xml
<PropertyGroup>
  <InterceptorsNamespaces>$(InterceptorsNamespaces);Microsoft.AspNetCore.Http.Validation.Generated</InterceptorsNamespaces>
</PropertyGroup>
```

**Usage:**
```csharp
builder.Services.AddValidation();
```

### Custom Validation Attributes

Both approaches support custom validation attributes. Here's an example:

```csharp
public class EvenNumberAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is int number && number % 2 != 0)
        {
            return new ValidationResult($"The field {validationContext.DisplayName} must be an even number.");
        }
        return ValidationResult.Success;
    }
}

public class MyRequest
{
    [EvenNumber]
    public int Quantity { get; set; }
}
```

## Basic Usage

Out of the box, the filter can be applied as shown below:

```csharp
usersV1
    .MapPost("/", CreateUser)
    .WithName("CreateUser")
    .AddEndpointFilter<ValidationFilter<CreateUserParameters>>()
    .ProducesValidationProblem();
```

At times, you may find it necessary to manipulate the serialized type names for validation keys/values, or to dictate the naming conventions of these keys/values.

With the `ValidationFilterOptions` class, you can customize the filter's behavior to suit your needs, such as opting to skip the first level when resolving serialization type names for validation keys/values.

The filter can be configured in two distinct ways:

> Locally injected ValidationFilterOptions

```csharp
usersV1
    .MapPost("/", CreateUser)
    .WithName("CreateUser")
    .AddEndpointFilter(new ValidationFilter<UpdateUserByIdParameters>(new ValidationFilterOptions
    {
        SkipFirstLevelOnValidationKeys = true,
    }))
    .ProducesValidationProblem();
```

> Alternatively, you can use the following method, where the `ValidationFilterOptions` have been registered in the dependency container earlier in the pipeline globally for all endpoints.

```csharp
builder.services.AddSingleton(_ => new ValidationFilterOptions
{
    SkipFirstLevelOnValidationKeys = true,
});

usersV1
    .MapPost("/", CreateUser)
    .WithName("CreateUser")
    .AddEndpointFilter<ValidationFilter<CreateUserParameters>>()
    .ProducesValidationProblem();
```

## Nested [FromBody] Validator Support 🎯

When using the common pattern of wrapping request parameters, the `ValidationFilter<T>` automatically discovers validators for nested `[FromBody]` properties:

```csharp
// Your Parameters wrapper
public record CreateUserParameters([property: FromBody] CreateUserRequest Request);

// Your validator - registered for the nested type
public class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
    }
}

// The filter automatically finds CreateUserRequestValidator!
.AddEndpointFilter<ValidationFilter<CreateUserParameters>>()
```

This means you can:
- ✅ Keep validators focused on the actual request model (not the parameters wrapper)
- ✅ Reuse validators across different endpoints
- ✅ Register validators with `AddValidatorsFromAssemblyContaining<T>()` as usual
- ✅ Both `IValidator<CreateUserParameters>` and `IValidator<CreateUserRequest>` will run if both exist

## When the argument to validate is missing

`ValidationFilter<T>` looks for the endpoint argument of type `T` (a derived type also counts). If there is none:

| Situation | Result |
|-----------|--------|
| The handler declares a parameter of type `T`, but its value is `null` (for example an optional body the client did not send) | Validation is skipped and the handler is called. There is nothing to validate, and the endpoint allows the value to be missing. |
| The handler has no parameter of type `T` (the filter is attached to the wrong endpoint) | `500` ProblemDetails (`application/problem+json`) with the generic detail "The endpoint is misconfigured.", and one `Error` log (event id 10100) that names the missing type. |

The response never contains internal type names; they are only logged. The filter picks up the application's `ILoggerFactory` automatically when it is added with `AddEndpointFilter<ValidationFilter<T>>()`. When it is constructed manually, pass one as the last argument: `new ValidationFilter<T>(options, loggerFactory)`.

# 📚 API Documentation

This library includes support for both Swagger UI and Scalar for API documentation, with OpenAPI 3.1 support.

## Swagger UI

Configure Swagger with OpenAPI 3.1 and the provided filters:

```csharp
// In Program.cs or your service configuration
builder.Services.AddSwaggerGen(options =>
{
    options.OperationFilter<SwaggerDefaultValues>();
    options.DocumentFilter<SwaggerEnumDescriptionsDocumentFilter>();
});

// In your app configuration
app.UseSwagger(options =>
{
    options.OpenApiVersion = Microsoft.OpenApi.OpenApiSpecVersion.OpenApi3_1;
});

app.UseSwaggerUI(options =>
{
    options.EnableTryItOutByDefault();
});
```

## Scalar

[Scalar](https://github.com/scalar/scalar) provides a modern, beautiful API reference documentation UI. This library includes Scalar.AspNetCore for easy integration:

```csharp
using Scalar.AspNetCore;

// Configure Scalar with C# HttpClient code generation
app.MapScalarApiReference(options =>
{
    options.WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
});
```

### Available Endpoints

When configured, your API will have the following documentation endpoints:

| Endpoint | Description |
|----------|-------------|
| `/` | Redirects to Scalar API reference |
| `/swagger` | Swagger UI interface |
| `/swagger/v1/swagger.json` | OpenAPI spec for Swagger UI |
| `/scalar/v1` | Scalar API reference |
| `/openapi/v1.json` | OpenAPI spec for Scalar |

Example configuration with separate OpenAPI paths:

```csharp
// Configure Swagger with OpenAPI 3.1
app.UseSwagger(options =>
{
    options.OpenApiVersion = OpenApiSpecVersion.OpenApi3_1;
});

// Configure Swagger UI
app.UseSwaggerUI(options =>
{
    var descriptions = app.DescribeApiVersions();
    foreach (var description in descriptions)
    {
        var url = $"/swagger/{description.GroupName}/swagger.json";
        options.SwaggerEndpoint(url, $"MyApi {description.GroupName.ToUpperInvariant()}");
    }
});

// Configure Scalar with separate OpenAPI path
app.MapScalarApiReference(options =>
{
    options
        .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient)
        .WithOpenApiRoutePattern("/openapi/{documentName}.json");
});
```

# 🛡️ Middleware

## GlobalErrorHandlingMiddleware

The `GlobalErrorHandlingMiddleware` class provides a mechanism for handling uncaught exceptions globally across an ASP.NET Core application.

This middleware helps in capturing any unhandled exceptions that occur during the request processing pipeline. It translates different types of exceptions into the appropriate HTTP status codes and responds with a standardized error message.

### Basic Usage

An example of how to configure the middleware.

```csharp
var app = builder.Build();

app.UseMiddleware<GlobalErrorHandlingMiddleware>();
```

An example of how to configure the middleware with options.

```csharp
var app = builder.Build();

var options = new GlobalErrorHandlingOptions
{
    IncludeException = true,
    UseProblemDetailsAsResponseBody = false,
};

app.UseMiddleware<GlobalErrorHandlingMiddleware>(options);
```

An example of how to configure the middleware with options through a extension method `UseGlobalErrorHandler`.

```csharp
var app = builder.Build();

app.UseGlobalErrorHandler(options =>
{
    options.IncludeException = true;
    options.UseProblemDetailsAsResponseBody = false;
});
```

### Error response format

By default (`UseProblemDetailsAsResponseBody = true`) the middleware writes an RFC 9457 ProblemDetails body with `Content-Type: application/problem+json`. Its `type` and `title` are the same as ASP.NET Core's own `TypedResults.Problem(statusCode)` in the same app:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.6.5",
  "title": "Gateway Timeout",
  "status": 504,
  "detail": "…",
  "instance": "/orders/42",
  "traceId": "…"
}
```

- `type` is the RFC link for the status code. Statuses without an RFC link in ASP.NET Core (e.g. 418, 429) have no `type`, which RFC 9457 treats as `about:blank`.
- `title` is the RFC reason phrase (e.g. "Bad Request", "Gateway Timeout"). For 500 it is ASP.NET Core's "An error occurred while processing your request.".

With `UseProblemDetailsAsResponseBody = false`, the middleware writes a plain JSON object (`status`, `title`, `instance`, `detail` and the id fields) with `Content-Type: application/json`, because that body is not RFC 9457 ProblemDetails. It uses the same `title`, but has no `type`.

### Custom Exception Mapping

The middleware supports mapping custom exception types to specific HTTP status codes. This allows you to define domain-specific exceptions and have them automatically translated to appropriate HTTP responses.

```csharp
app.UseGlobalErrorHandler(options =>
{
    options.MapException<OrderNotFoundException>(HttpStatusCode.NotFound);
    options.MapException<DuplicateResourceException>(HttpStatusCode.Conflict);
    options.MapException<QuotaExceededException>(HttpStatusCode.TooManyRequests);

    // For status codes not in HttpStatusCode enum, cast from StatusCodes
    options.MapException<TeapotException>((HttpStatusCode)StatusCodes.Status418ImATeapot);
});
```

**Key features:**
- **Custom mappings take precedence** over built-in defaults
- **Inheritance support**: Derived exception types inherit mappings from base types
- **Method chaining**: Multiple mappings can be chained fluently
- **Type-safe**: Uses `HttpStatusCode` enum for compile-time safety

### Default Exception Mappings

The following exception types have built-in mappings:

| Exception Type | HTTP Status Code |
|---------------|------------------|
| `FluentValidation.ValidationException` | 400 Bad Request |
| `System.ComponentModel.DataAnnotations.ValidationException` | 400 Bad Request |
| `BadHttpRequestException` | 400 Bad Request |
| `ArgumentException` | 400 Bad Request |
| `UnauthorizedAccessException` | 401 Unauthorized |
| `InvalidOperationException` | 409 Conflict |
| `NotImplementedException` | 501 Not Implemented |
| `TimeoutException` | 504 Gateway Timeout |
| All other exceptions | 500 Internal Server Error |

You can override any of these defaults by mapping the same exception type to a different status code.

### Exceptions after the response has started

If an exception is thrown after the response has started (for example while streaming a large body), the status code and headers have already been sent, so no error response can be written. The middleware then logs a warning and rethrows the exception, so the server aborts the response (HTTP/1.1 closes the connection, HTTP/2 resets the stream). The client gets a transport error instead of a silently truncated response that looks successful. The server (Kestrel) logs the rethrown exception at `Error` with the stack trace, and the request is recorded as failed (`error.type`) in the `http.server.request.duration` metric.

### Logging

The middleware logs the exceptions it handles. Both `UseGlobalErrorHandler` and `UseMiddleware<GlobalErrorHandlingMiddleware>()` pick up the application's `ILoggerFactory` automatically, so no extra configuration is needed.

| Situation | Log level | Event id | Exception (stack trace) included |
|-----------|-----------|----------|----------------------------------|
| Exception resolved to a server error (5xx) | `Error` | 10000 | ✅ |
| Exception resolved to a client error (4xx) | `Debug` | 10001 | ❌ (type name and message only) |
| Request aborted by the client (`OperationCanceledException` + `RequestAborted`) | `Debug` | 10002 | ❌ |
| Exception after the response has started (the exception is rethrown) | `Warning` | 10003 | ❌ (the server logs the rethrown exception at `Error`, so each failure is logged at `Error` once) |

The log category is `Atc.Rest.MinimalApi.Middleware.GlobalErrorHandlingMiddleware`. The client-supplied ids are added to the log scope, so log entries can be matched with the `correlationId` and `requestId` fields in the error response:

| Request header | Log scope key |
|----------------|---------------|
| `x-correlation-id` | `CorrelationId` |
| `x-request-id` | `ClientRequestId` |

`x-request-id` is logged as `ClientRequestId` because ASP.NET Core's hosting scope already uses `RequestId` for `HttpContext.TraceIdentifier` (the `traceId` in the response). The hosting scope also provides `TraceId`, `SpanId` and `RequestPath`.

With the usual production log level (`Information`), only server errors are written. Client errors and aborted requests are expected behavior and are logged at `Debug`, so they cost nothing until you need them. Use standard log filtering to change this. For example, to see client errors while troubleshooting (use `"None"` to turn the logging off completely), in `appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Atc.Rest.MinimalApi.Middleware.GlobalErrorHandlingMiddleware": "Debug"
    }
  }
}
```

When the middleware is constructed manually, pass an `ILoggerFactory` to enable logging; without one, nothing is logged:

```csharp
var middleware = new GlobalErrorHandlingMiddleware(next, options, loggerFactory);
```

### Empty error responses (404, 405, malformed JSON, …)

`GlobalErrorHandlingMiddleware` handles exceptions. Many framework errors are not exceptions, though: an unknown route (404), a wrong HTTP method (405), malformed JSON (400), and results without a body such as `TypedResults.NotFound()` or `TypedResults.Unauthorized()`. By default these have an **empty body**.

To give them a ProblemDetails body (`application/problem+json`), register **both** of these:

```csharp
builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseGlobalErrorHandler();
app.UseStatusCodePages();
```

Only the pair works:

| `AddProblemDetails()` | `UseStatusCodePages()` | Error without a body becomes |
|:---:|:---:|---|
| ❌ | ❌ | empty |
| ✅ | ❌ | still empty (`AddProblemDetails` alone does not fill them) |
| ❌ | ✅ | `text/plain`: "Status Code: 404; Not Found" |
| ✅ | ✅ | `application/problem+json` with `type`, `title`, `status` and `traceId` |

Responses that already have a body are not changed, so the bodies written by `GlobalErrorHandlingMiddleware` are kept as they are.

# 💡 Sample Project

The sample project `Demo.Api` located in the [sample](/sample/) folder within the repository illustrates a practical implementation of the Atc.Rest.MinimalApi package, showcasing all the features and best practices detailed in this documentation. It's a comprehensive starting point for those looking to get a hands-on understanding of how to effectively utilize the library in real-world applications.

The Demo.Api project also leverages the `Asp.Versioning.Http` Nuget package to establish a proper versioning scheme. It's an example implementation of how API versioning can be easily managed and maintained.

# 📋 Requirements

* [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)

# 🤝 How to contribute

[Contribution Guidelines](https://atc-net.github.io/introduction/about-atc#how-to-contribute)

[Coding Guidelines](https://atc-net.github.io/introduction/about-atc#coding-guidelines)

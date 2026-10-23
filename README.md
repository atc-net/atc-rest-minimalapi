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
  - [Validation error keys](#validation-error-keys)
  - [Nested [FromBody] Validator Support 🎯](#nested-frombody-validator-support-)
  - [When the argument to validate is missing](#when-the-argument-to-validate-is-missing)
  - [Validating a value the endpoint builds itself (form endpoints)](#validating-a-value-the-endpoint-builds-itself-form-endpoints)
- [📚 API Documentation](#-api-documentation)
  - [Swagger UI](#swagger-ui)
  - [Scalar](#scalar)
- [🛡️ Middleware](#️-middleware)
  - [GlobalErrorHandlingMiddleware](#globalerrorhandlingmiddleware)
- [✂️ Trimming and Native AOT](#️-trimming-and-native-aot)
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
- **Whole object graph** - DataAnnotations on nested objects, collections and dictionaries, type-level attributes and `IValidatableObject`, with a built-in validator inspired by Damian Edwards' [MiniValidation](https://github.com/DamianEdwards/MiniValidation) (3.0 no longer depends on the MiniValidation package). Every object is validated, also when two are equal records. As in `Microsoft.Extensions.Validation`, an object's type-level attributes and `IValidatableObject.Validate` run only when its members (nested objects included) passed. A request with validation rules nested deeper than `ValidationFilterOptions.MaxValidationDepth` (32 by default) is rejected as a whole: a 400 with "The request body is nested too deeply to validate (more than 32 levels)." under the empty key `""`

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

## Validation error keys

The keys of the `errors` object are the names **the client sent**, so a Blazor, React or other form can put each error on the matching field without any mapping:

| Parameters record member | Violation | Error key |
|---|---|---|
| `[FromBody] SetRequest Request` → `Reason` | `MaxLength(5)` | `reason` |
| `[FromBody] SetRequest Request` → `Lines[2].Sku` | `Required` | `lines[2].sku` |
| `[FromQuery(Name = "page-size")] int PageSize` | `Range(1, 100)` | `page-size` |
| `[FromRoute(Name = "mode")] string Mode` | `Required` | `mode` |
| `[FromBody, Required] SetRequest Request` | body missing or `null` | `""` - "A non-empty request body is required." |
| `[FromBody] IList<SetRequestLine> Request` (an array body) → `[2].Sku` | `Required` | `[2].sku` |

- **Body values:** the JSON path relative to the body (the body member itself is not part of the key). Each segment uses `[JsonPropertyName]` when present, otherwise the app's JSON naming policy (`Microsoft.AspNetCore.Http.Json.JsonOptions`, camelCase by default).
- **The body member** is the `[FromBody]` member, or a member with no binding-source attribute whose type binds itself with a static `BindAsync(HttpContext[, ParameterInfo])` (or `IBindableFromHttpContext<T>`). That is how a body that is read as JSON *or* XML by its `Content-Type` is bound, since `[FromBody]` makes Minimal APIs read JSON only: `record AddPetParameters([property: Required] Pet Request)` with `Pet.BindAsync` gives `name`, not `Request.name`.
- **XML requests** (`application/xml`, `text/xml`, `*+xml`): body segments use their `[XmlElement]` / `[XmlAttribute]` name when present, otherwise the JSON name, so the client gets back the names it sent (`[XmlElement("surname")] string LastName` → `surname`). The member's messages name it the same way: "The field surname must be ..." - the name the message was built with (its `[Display(Name)]`, its C# name, or FluentValidation's `'Last Name'`) is replaced as a whole word. A JSON request's messages are not changed.
- **The body as a whole** (a missing or `null` body, or a FluentValidation rule on the body itself, `RuleFor(x => x)`): the empty key `""`, as in ASP.NET Core MVC - a form shows it as a form-level error. A missing body gets MVC's message "A non-empty request body is required.".
- **Route, query and header values:** the binding `Name` (members without a `Name` keep their member name).
- **Collections:** indexers are kept, e.g. `lines[2].sku` - also when the body itself is an array (`[2].sku`, as in MVC). Libraries that expect dot notation (react-hook-form: `lines.2.sku`) can convert with `key.replace(/\[(\d+)\]/g, '.$1')`.
- **Same key twice** (e.g. route `id` and body `id`): the errors are merged under one key.
- **Messages** are not rewritten (except the missing-body one above: DataAnnotations' `[Required]` and FluentValidation's `NotNull` / `NotEmpty` on the body member, and a member's name in an XML request's messages); they keep the validators' own text (e.g. `'First Name' must not be empty.`).
- DataAnnotations and FluentValidation errors for the same field are merged under the same key.
- **Dictionaries:** the dictionary member is mapped, but from the entry on the key keeps the validator's path, e.g. `map[0].Value.Sku` (the DataAnnotations validator reports the entry by position, not by its key).

The `sample` folder proves this with two frontends that bind errors to fields by exact key: the Blazor app `Demo.Web` and the React app `Demo.React` (Vite + TypeScript).

### Keeping the 2.x keys (C# member paths)

Before 3.0, the keys were the C# member paths of the validated type, e.g. `Request.Reason` and `PageSize`. ASP.NET Core's own `AddValidation()` also uses that style. To keep it, set `KeyStyle = ValidationKeyStyle.MemberPath`:

```csharp
builder.Services.AddSingleton(new ValidationFilterOptions
{
    KeyStyle = ValidationKeyStyle.MemberPath,
    SkipFirstLevelOnValidationKeys = true, // optional, only applies to MemberPath: "Request.Reason" -> "Reason"
});
```

`SkipFirstLevelOnValidationKeys` has no effect with the default `WireNames` style (the keys are already the names the client sent). If it is set without `MemberPath`, the filter logs one `Warning` (event id 10101) when the first endpoint that uses it is built.

The options can also be passed to a single endpoint: `.AddEndpointFilter(new ValidationFilter<UpdateUserByIdParameters>(new ValidationFilterOptions { ... }))`.

For a generated API (atc-rest-api-source-generator), register the options in the host's service collection the same way; the generated `AddEndpointFilter<ValidationFilter<...>>()` picks them up from DI.

### Registering ValidationFilterOptions

Options registered in the dependency container apply to all endpoints that use `AddEndpointFilter<ValidationFilter<T>>()`:

```csharp
builder.Services.AddSingleton(new ValidationFilterOptions
{
    KeyStyle = ValidationKeyStyle.WireNames, // the default
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
- ✅ The same works for a body member bound by `BindAsync` (no `[FromBody]`, see [Validation error keys](#validation-error-keys))

### A body that is a list

For a body that is a list of a model (`[FromBody] IList<CreateUserRequest> Request`), each item is validated by the model's validator - the one you already have for a single model - as it is by the model's DataAnnotations. Each error is keyed by the item's index:

```csharp
public record ImportUsersParameters([property: FromBody] IList<CreateUserRequest> Request);

// No list validator needed: CreateUserRequestValidator runs for each user
.AddEndpointFilter<ValidationFilter<ImportUsersParameters>>()
```

```json
{ "errors": { "[1].firstName": ["FirstName has to start with an uppercase letter."], "[2].email": ["The Email field is required."] } }
```

A validator for the list itself runs instead of the item's, when one is registered - for the body's own type or for a base type (`AbstractValidator<List<CreateUserRequest>>` also serves a `List<T>` subclass that binds itself). A rule on the items, `RuleForEach(x => x).SetValidator(...)`, is keyed the same way (`[1].firstName`, not `x[1].FirstName`). The sample's `POST /api/users/import` shows it.

## When the argument to validate is missing

`ValidationFilter<T>` looks for the endpoint argument of type `T` (a derived type also counts). If there is none:

| Situation | Result |
|-----------|--------|
| The handler declares a parameter of type `T`, but its value is `null` (for example an optional body the client did not send) | Validation is skipped and the handler is called. There is nothing to validate, and the endpoint allows the value to be missing. |
| The handler has no parameter of type `T` (the filter is attached to the wrong endpoint) | `500` ProblemDetails (`application/problem+json`) with the generic detail "The endpoint is misconfigured.", and one `Error` log (event id 10100) that names the missing type. |

The response never contains internal type names; they are only logged. The filter picks up the application's `ILoggerFactory` automatically when it is added with `AddEndpointFilter<ValidationFilter<T>>()`. When it is constructed manually, pass one as the last argument: `new ValidationFilter<T>(options, loggerFactory)`.

## Validating a value the endpoint builds itself (form endpoints)

`ValidationFilter<T>` validates an endpoint argument. An endpoint that builds the value itself, e.g. a form bound field by field (Minimal APIs cannot bind a wrapped model from a form), calls `RequestValidation.ValidateAsync` instead. It runs the same DataAnnotations and FluentValidation as the filter, with the `ValidationFilterOptions` from DI, and returns the same 400 `ValidationProblem`, or `null` when the value is valid:

```csharp
internal static async Task<IResult> CreateShipment(
    [FromServices] ICreateShipmentHandler handler,
    HttpContext httpContext,
    [FromRoute(Name = "shipmentId")] string shipmentId,
    [FromForm(Name = "label")] string? label,
    CancellationToken cancellationToken)
{
    var parameters = new CreateShipmentParameters(shipmentId, new ShipmentForm(label));
    if (await RequestValidation.ValidateAsync(parameters, httpContext) is { } invalid)
    {
        return invalid;
    }

    return await handler.ExecuteAsync(parameters, cancellationToken);
}

public sealed record CreateShipmentParameters(
    [property: FromRoute(Name = "shipmentId")] string ShipmentId,
    [property: Required] ShipmentForm Request);
```

In the parameters record, the member without a binding attribute (`Request`) is the body, as a `[FromBody]` member is for the filter: a form field is keyed like the same property in a JSON body (`label`, `address.street`), and a registered `IValidator<ShipmentForm>` runs. Pass `isParametersWrapper: false` when the value is the form model itself.

# 📚 API Documentation

This library includes filters for Swagger UI (Swashbuckle) and works with Scalar for API documentation, with OpenAPI 3.1 support.

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

[Scalar](https://github.com/scalar/scalar) provides a modern, beautiful API reference documentation UI. This library does not depend on it (up to 3.0.1 it did, without using it); add the `Scalar.AspNetCore` package to the host:

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
  "instance": "/orders/42",
  "traceId": "…"
}
```

(A `detail` field is added for client errors, and for all errors when `IncludeException` is `true`; see below.)

- `type` is the RFC link for the status code. Statuses without an RFC link in ASP.NET Core (e.g. 418, 429) have no `type`, which RFC 9457 treats as `about:blank`.
- `title` is the RFC reason phrase (e.g. "Bad Request", "Gateway Timeout"). For 500 it is ASP.NET Core's "An error occurred while processing your request.".
- `instance` is the request path, including the path base (e.g. `/api/orders/42` behind `UsePathBase("/api")`).

Before writing the error, the middleware resets the response, the same as ASP.NET Core's own exception handler (`UseExceptionHandler`): headers the failed handler had set (e.g. `ETag`, `Location`, `Content-Length`) and a buffered body are dropped, and the error gets `Cache-Control: no-cache,no-store`, `Pragma: no-cache` and `Expires: -1`, so caches never store it. Only headers that middleware adds in `Response.OnStarting` (e.g. CORS, `api-supported-versions`) are kept. Headers that middleware sets directly are dropped, wherever that middleware is registered: for example `UseHsts()` gives no `Strict-Transport-Security` on error responses, exactly as with `UseExceptionHandler`. If a header must also be on error responses (e.g. security headers), add it in `Response.OnStarting`.

#### Integration with `AddProblemDetails()`

When the host registers ASP.NET Core's ProblemDetails service (`builder.Services.AddProblemDetails()`), the middleware writes its ProblemDetails body through `IProblemDetailsService`. So:

- `CustomizeProblemDetails` also applies to the middleware's errors (e.g. to add an `errorCode` or a tenant id);
- the app's JSON options are used for serialization;
- `traceId` is filled in by ASP.NET Core.

When the service is not registered, or it declines to write (e.g. for `Accept: text/html`), the middleware writes the same body itself. The plain dictionary body (`UseProblemDetailsAsResponseBody = false`) never goes through the service.

#### Correlating a response with the logs

Error responses contain up to three ids. Each one can be found in the logs under a PascalCase name (the .NET logging convention), while the response uses camelCase (the JSON convention):

| Response field | Value | Log field |
|---|---|---|
| `correlationId` | the `x-correlation-id` request header | `CorrelationId` (log scope added by this middleware) |
| `requestId` | the `x-request-id` request header | `ClientRequestId` (log scope added by this middleware) |
| `traceId` | `Activity.Current?.Id ?? HttpContext.TraceIdentifier` (the same rule as ASP.NET Core) | with tracing active (e.g. OpenTelemetry): the W3C id `00-<TraceId>-<SpanId>-01`, so search for its `TraceId` part (in Application Insights: `operation_Id`); without tracing: `RequestId` (ASP.NET Core's hosting scope) |

#### Exception details in `detail`

Exception details can reveal internals (type names, service names, inner messages), so they are only returned when `IncludeException` is `true`:

| `IncludeException` | Server error (5xx) | Client error (4xx, e.g. `ArgumentException`) |
|---|---|---|
| `false` (**default**) | no `detail` | the exception message, without the type name |
| `true` | type name, message and inner messages (e.g. `"InvalidData: boom"`) | same |

`UseGlobalErrorHandler` sets `IncludeException = true` automatically in the **Development** environment, before your configure callback runs, so an explicit setting always wins:

```csharp
app.UseGlobalErrorHandler(options =>
{
    options.IncludeException = true; // e.g. also in a test environment
});
```

When you register the middleware directly (`app.UseMiddleware<GlobalErrorHandlingMiddleware>()` or `new GlobalErrorHandlingOptions()`), there is no environment check: details are off unless you set `IncludeException = true` yourself.

The full exception is always logged on the server (see [Logging](#logging)), and clients can quote the `traceId` / `correlationId` from the response.

With `UseProblemDetailsAsResponseBody = false`, the middleware writes a plain JSON object (`status`, `title`, `instance`, `detail` and the id fields) with `Content-Type: application/json`, because that body is not RFC 9457 ProblemDetails. It uses the same `title`, but has no `type`.

#### A `ValidationException` thrown by a handler

A handler (or a service it calls) that validates by itself and throws `FluentValidation.ValidationException` or `System.ComponentModel.DataAnnotations.ValidationException` gets the same answer as when the `ValidationFilter` rejects the request: a 400 with the validation `title` "One or more validation errors occurred." and the field errors in `errors` (an `HttpValidationProblemDetails`, also through `AddProblemDetails()`; the plain dictionary body gets `errors` too):

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "instance": "/orders",
  "errors": { "name": ["'Name' must be a known thing."], "lines[2].sku": ["'Sku' must not be empty."] },
  "traceId": "…"
}
```

- FluentValidation: one key per `ValidationFailure.PropertyName`; failures on the same property share the key.
- DataAnnotations: every name in `ValidationResult.MemberNames`; a result without member names goes under the empty key `""` (an error about the request as a whole, as the filter reports a missing body).
- There is no `detail` when an error belongs to a field (the messages are in `errors`), also with `IncludeException = true`.
- When every error is about the request as a whole (only the key `""`, e.g. `new ValidationException("The order is closed.")` from DataAnnotations, or root-level FluentValidation rules), those messages are also the `detail`, so a client that shows `detail` shows the reason. A FluentValidation `ValidationException` constructed with a message only has no failures: it is answered as before, with the message in `detail` and no `errors`.
- **Keys** follow the app's JSON naming policy (`Microsoft.AspNetCore.Http.Json.JsonOptions`, camelCase by default) segment by segment: `Lines[2].Sku` → `lines[2].sku`. The middleware does not know which type was validated, so a `[JsonPropertyName]` that differs from the naming policy is **not** honoured here (the `ValidationFilter` does honour it). Name the failure after the wire name yourself if they differ, e.g. FluentValidation's `.OverridePropertyName("country_code")`. Likewise, the keys match the filter's only when the handler validates the body model: a validator for the parameters wrapper reports `Request.Name`, which becomes `request.name` here (the filter drops the body member: `name`).
- A custom mapping of a `ValidationException` to another status (`MapException<ValidationException>(HttpStatusCode.UnprocessableEntity)`) keeps the plain body with the message in `detail`.

**Migrating from 2.x:** in 2.x this 400 had the title "Bad Request" and the messages in `detail` (FluentValidation's "Validation failed: -- Name: …" text). Clients that showed `detail` should now read `errors`, as they do for a 400 from the `ValidationFilter`; a DataAnnotations `new ValidationException("message")` has its message in `errors[""]` and (since 3.1) in `detail` too. There is no switch back to the old body at 400; mapping the exception to another status (above) keeps a plain body.

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
- **Custom mappings take precedence** over built-in defaults (also over a default mapping of a more-derived type)
- **Inheritance support**: Derived exception types inherit mappings from base types. When several mapped types match, the most-derived one wins, whatever order the mappings were added in
- **Mapping the same type again** replaces its status code
- **Method chaining**: Multiple mappings can be chained fluently
- **Type-safe**: Uses `HttpStatusCode` enum for compile-time safety

### Default Exception Mappings

The following exception types have built-in mappings:

| Exception Type | HTTP Status Code |
|---------------|------------------|
| `FluentValidation.ValidationException` | 400 Bad Request |
| `System.ComponentModel.DataAnnotations.ValidationException` | 400 Bad Request |
| `BadHttpRequestException` | its own `StatusCode` (usually 400; e.g. 413 when Kestrel rejects a body that is too large), as ASP.NET Core's exception handler does |
| `ArgumentException` | 400 Bad Request |
| `ArgumentNullException`, `ArgumentOutOfRangeException` | 500 Internal Server Error |
| `NotImplementedException` | 501 Not Implemented |
| `TimeoutException` | 504 Gateway Timeout |
| `OperationCanceledException` (not caused by the client disconnecting) | 504 Gateway Timeout |
| All other exceptions (including `InvalidOperationException` and `UnauthorizedAccessException`) | 500 Internal Server Error |

Mappings also apply to derived exception types. When several mapped types match, the most-derived one wins: for example, `ArgumentOutOfRangeException` gives 500 even though its base type `ArgumentException` gives 400.

Why these defaults: a 4xx tells the client that *its* request was wrong. Exceptions that usually mean a bug or misconfiguration on the server are reported as 500, so they show up in 5xx monitoring and clients don't retry a request that can never succeed:

- `InvalidOperationException` (and derived types such as `ObjectDisposedException`) is what .NET and ASP.NET Core throw for invalid state or missing configuration (e.g. "Unable to find the required 'IAuthenticationService' service").
- `UnauthorizedAccessException` is mostly thrown by the file system and OS ("Access to the path … is denied"). A 401 would tell the client to log in again.
- `ArgumentNullException` and `ArgumentOutOfRangeException` usually come from coding errors (`ThrowIfNull`, an index out of range). Bad client input is normally rejected earlier, by model binding and the `ValidationFilter`.
- Plain `ArgumentException` stays 400, because it is commonly thrown on purpose for bad input. It is still ambiguous: framework code also throws it (e.g. adding a duplicate key to a dictionary). Its message becomes the `detail` in every environment - for a framework exception that can include data (a duplicate dictionary key reports the key), so map `ArgumentException` to 500 if your handlers do not throw it on purpose.

You can override any of these defaults by mapping the same exception type to a different status code.

#### Migrating from 2.x

In 2.x, `InvalidOperationException` mapped to 409, `UnauthorizedAccessException` to 401, and `ArgumentNullException` / `ArgumentOutOfRangeException` to 400 (through `ArgumentException`). If you throw these on purpose for those responses, restore the old mappings:

```csharp
app.UseGlobalErrorHandler(options =>
{
    options.MapException<InvalidOperationException>(HttpStatusCode.Conflict);
    options.MapException<UnauthorizedAccessException>(HttpStatusCode.Unauthorized);
    options.MapException<ArgumentNullException>(HttpStatusCode.BadRequest);
    options.MapException<ArgumentOutOfRangeException>(HttpStatusCode.BadRequest);
});
```

A better long-term fix is a domain exception of your own (e.g. `DuplicateResourceException`) mapped to the status you want.

### Metrics

For every exception it handles, the middleware reports the same metrics as ASP.NET Core's own exception handler, so dashboards and alerts work the same with either:

| Metric | What is added |
|--------|---------------|
| `http.server.request.duration` (meter `Microsoft.AspNetCore.Hosting`) | the tag `error.type` = the exception's full type name (e.g. `System.IO.InvalidDataException`) |
| `aspnetcore.diagnostics.exceptions` (meter `Microsoft.AspNetCore.Diagnostics`) | one count, tagged `error.type` and `aspnetcore.diagnostics.exception.result` = `handled` |

The counter uses ASP.NET Core's meter and instrument name, created through the app's `IMeterFactory` (every `WebApplication` registers one). As with `UseExceptionHandler`, `error.type` is also set when the exception is mapped to a 4xx status. A request aborted by the client and an exception after the response has started are not counted by the middleware.

### Differences from `UseExceptionHandler`

The middleware writes the error itself; it is not ASP.NET Core's exception handler, so:

- `IExceptionHandler` registrations (`AddExceptionHandler<T>()`) are not called - map the exception (`MapException`) or handle it in the endpoint instead.
- `IExceptionHandlerFeature` / `IExceptionHandlerPathFeature` are not set. `CustomizeProblemDetails` gets the exception from `ProblemDetailsContext.Exception`.
- An exception thrown while writing the ProblemDetails (e.g. by a `CustomizeProblemDetails` callback) replaces the original one.

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

`x-request-id` is logged as `ClientRequestId` because ASP.NET Core's hosting scope already uses `RequestId` for `HttpContext.TraceIdentifier`, a different value. The hosting scope also provides `TraceId`, `SpanId` and `RequestPath`. See [Correlating a response with the logs](#correlating-a-response-with-the-logs) for how the response's `traceId` maps to these.

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

# ✂️ Trimming and Native AOT

The package is **not yet trim- or Native AOT-compatible**: it does not declare `IsAotCompatible`, and parts of it use reflection.

| API | State |
|---|---|
| `AddEndpointDefinitions`, `AddEndpointAndServiceDefinitions` | Marked `[RequiresUnreferencedCode]`: they scan assemblies and create the definitions by reflection, so a trimmed app gets the warning at its own call site. Map the endpoints explicitly instead. `UseEndpointDefinitions` itself needs no reflection. |
| `SwaggerDefaultValues`, `SwaggerEnumDescriptionsDocumentFilter` | Marked `[RequiresUnreferencedCode]` (`SwaggerDefaultValues` also `[RequiresDynamicCode]`), like Swashbuckle itself. |
| `ValidationFilter<T>` | Reflection, not yet annotated: DataAnnotations validation walks the validated type's members, and the validator lookup for a body member closes `IValidator<>` at runtime. |
| `GlobalErrorHandlingMiddleware` | With `AddProblemDetails()`, error bodies are written by ASP.NET Core's `IProblemDetailsService`. The middleware's own fallback writer (and the plain dictionary body) still serializes with reflection-based `System.Text.Json`. |

Publishing a trimmed or Native AOT app that references the package therefore reports it as an assembly with trim warnings (IL2104). Making the validation filter and the middleware's writer trim-safe is planned for a later 3.x release.

# 💡 Sample Project

The sample project `Demo.Api` located in the [sample](/sample/) folder within the repository illustrates a practical implementation of the Atc.Rest.MinimalApi package, showcasing all the features and best practices detailed in this documentation. It's a comprehensive starting point for those looking to get a hands-on understanding of how to effectively utilize the library in real-world applications.

The Demo.Api project also leverages the `Asp.Versioning.Http` Nuget package to establish a proper versioning scheme. It's an example implementation of how API versioning can be easily managed and maintained.

Start everything with the Aspire AppHost (`sample/src/Demo.AppHost`, e.g. `aspire start`). It runs:

| Resource | Project | What it shows |
|---|---|---|
| `api` | `Demo.Api` | the API, with the error-handling simulations under `/api/test/errors/...` (see `api.http`) |
| `web` | `Demo.Web` (Blazor + MudBlazor) | user forms that put each validation error on its field by exact key |
| `react` | `Demo.React` (Vite + React + TypeScript + MUI) | the same users CRUD as the Blazor app in Material UI: each input's `name` is its wire path, so `errors[name]` is shown as that field's helper text |

`Demo.React` needs Node.js; Aspire installs its npm packages and starts Vite, which proxies `/api` to `Demo.Api`.

Tests for the sample frontends:

- `Demo.React` unit and component tests (Vitest + Testing Library): `npm test` in `sample/src/Demo.React` (API client, form-value mapping, and the form dialog showing each server error on its field).
- End-to-end tests (`sample/test/Demo.EndToEndTests`): start the AppHost and submit invalid create/edit forms in both frontends and run a create-update-delete round trip in the React app, with Playwright. They are **explicit**, so a normal `dotnet test` skips them. Run them with:

  ```bash
  pwsh sample/test/Demo.EndToEndTests/bin/Debug/net10.0/playwright.ps1 install chromium   # once, after building
  dotnet test --project sample/test/Demo.EndToEndTests -- --explicit only
  ```

# 📋 Requirements

* [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)

# 🤝 How to contribute

[Contribution Guidelines](https://atc-net.github.io/introduction/about-atc#how-to-contribute)

[Coding Guidelines](https://atc-net.github.io/introduction/about-atc#coding-guidelines)

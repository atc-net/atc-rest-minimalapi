namespace Demo.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection ConfigureHealthChecks(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        return services;
    }

    public static IServiceCollection ConfigureSecurity(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddCors(corsOptions =>
        {
            corsOptions.AddPolicy(PolicyConstants.Cors, configurePolicy =>
            {
                configurePolicy
                    .AllowAnyOrigin()
                    .AllowAnyMethod()
                    .AllowAnyHeader();
            });
        });

        return services;
    }

    public static IApiVersioningBuilder ConfigureApiVersioning(
        this IServiceCollection services)
        => services
            .AddApiVersioning(options =>
            {
                // Specify the default API Version
                options.DefaultApiVersion = new ApiVersion(1, 0);

                // If the client hasn't specified the API version in the request, use the default API version number
                options.AssumeDefaultVersionWhenUnspecified = true;

                // reporting api versions will return the headers
                // "api-supported-versions" and "api-deprecated-versions"
                options.ReportApiVersions = true;

                //// DEFAULT Version reader is QueryStringApiVersionReader();
                //// clients request the specific version using the x-api-version header
                //// Supporting multiple versioning scheme
                options.ApiVersionReader = ApiVersionReader.Combine(
                    new HeaderApiVersionReader(ApiVersionConstants.ApiVersionHeaderParameter),
                    new MediaTypeApiVersionReader(ApiVersionConstants.ApiVersionMediaTypeParameter),
                    new QueryStringApiVersionReader(ApiVersionConstants.ApiVersionQueryParameter),
                    new QueryStringApiVersionReader(ApiVersionConstants.ApiVersionQueryParameterShort),
                    new UrlSegmentApiVersionReader());
            });

    public static void ConfigureScalar(
        this IServiceCollection services,
        IApiVersioningBuilder apiVersioningBuilder)
    {
        services.ConfigureOptions<ConfigureScalarOptions>();
        services.AddEndpointsApiExplorer();

        apiVersioningBuilder
            .AddApiExplorer(options =>
            {
                // add the versioned api explorer, which also adds IApiVersionDescriptionProvider service
                // note: the specified format code will format the version as 'v'major[.minor][-status]"
                options.GroupNameFormat = "'v'VVV";

                // note: this option is only necessary when versioning by url segment. The SubstitutionFormat
                // can also be used to control the format of the API version in route templates
                options.SubstituteApiVersionInUrl = true;
            })
            .AddOpenApi();
    }

    public static IServiceCollection ConfigureRequestHandling(
        this IServiceCollection services)
    {
        services.AddEndpointDefinitions(typeof(IApiContractAssemblyMarker));

        services.AddValidatorsFromAssemblyContaining<IDomainAssemblyMarker>(ServiceLifetime.Singleton);

        services.AddSingleton(_ => new ValidationFilterOptions
        {
            SkipFirstLevelOnValidationKeys = true,
        });

        // JsonOptions configures the JSON serializer used by the entire minimal-API pipeline.
        // Without it, System.Text.Json defaults are used and JsonStringEnumConverter
        // is missing, so string-enum request bodies fail to deserialize.
        services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(o => JsonSerializerOptionsFactory.Create().Configure(o));

        return services;
    }

    public static IServiceCollection ConfigureObservability(
        this IServiceCollection services,
        IConfiguration configuration,
        bool isDevelopmentEnvironment)
    {
        services.AddLogging(logging =>
        {
            if (isDevelopmentEnvironment)
            {
                logging.SetMinimumLevel(LogLevel.Debug);
                logging.AddDebug();
                logging.AddConsole();
                services.AddHttpLogging(logOptions =>
                {
                    logOptions.LoggingFields = HttpLoggingFields.All;
                    logOptions.RequestBodyLogLimit = 4096;
                    logOptions.ResponseBodyLogLimit = 4096;
                    logOptions.MediaTypeOptions.AddText("application/json");
                });
            }
            else
            {
                logging.SetMinimumLevel(LogLevel.Information);
            }
        });

        services.ConfigureOpenTelemetry(
            configuration);

        return services;
    }

    private static IServiceCollection ConfigureOpenTelemetry(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var builder = services.AddOpenTelemetry();

        if (!string.IsNullOrWhiteSpace(configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
        {
            builder.UseOtlpExporter();
        }

        builder
            .ConfigureResource(resource => resource.AddService(
                Telemetry.ServiceName,
                TelemetryConstants.ServiceNamespace,
                Telemetry.ServiceVersion))
            .WithLogging(
                logger => logger.SetResourceBuilder(
                    ResourceBuilder.CreateDefault().AddService(
                        Telemetry.ServiceName,
                        TelemetryConstants.ServiceNamespace,
                        Telemetry.ServiceVersion)),
                options =>
                {
                    // Export log scopes (e.g. CorrelationId, ClientRequestId) and the formatted message
                    options.IncludeScopes = true;
                    options.IncludeFormattedMessage = true;
                })
            .WithMetrics(metrics => metrics
                .AddRuntimeInstrumentation()
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation())
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        // Health probes are pure noise in distributed traces.
                        options.Filter = context =>
                            !context.Request.Path.StartsWithSegments(HealthCheckEndpoints.Aliveness, StringComparison.OrdinalIgnoreCase) &&
                            !context.Request.Path.StartsWithSegments(HealthCheckEndpoints.Health, StringComparison.OrdinalIgnoreCase) &&
                            !context.Request.Path.StartsWithSegments(HealthCheckEndpoints.Extended, StringComparison.OrdinalIgnoreCase);
                    })
                    .AddHttpClientInstrumentation();
            });

        return services;
    }
}
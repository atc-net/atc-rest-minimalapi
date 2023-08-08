using Atc.Rest.MinimalApi.Filters.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.ConfigureLogging();

var services = builder.Services;

services.AddMemoryCache();

services.ConfigureDomainServices(builder.Configuration);

services.AddValidatorsFromAssemblyContaining<IDomainAssemblyMarker>(ServiceLifetime.Singleton);

services.ConfigureApiVersioning();

services.AddEndpointDefinitions(typeof(IApiContractAssemblyMarker));

services.ConfigureSwagger();

services.AddCors(corsOptions =>
{
    corsOptions.AddPolicy("DemoCorsPolicy", configurePolicy =>
    {
        configurePolicy
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

// This enables proper enum as string in Swagger UI
services.AddControllers().AddJsonOptions(o => JsonSerializerOptionsFactory.Create().Configure(o));
services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(o => JsonSerializerOptionsFactory.Create().Configure(o));

services.AddSingleton(_ => new ValidationFilterOptions
{
    SkipFirstLevelOnValidationKeys = true,
});

var app = builder.Build();

app.UseEndpointDefinitions();

app.AddGlobalErrorHandler();

// Enabling the status code pages middleware, will allow Problem Details to be used in some extra non-exception related framework scenarios,
// such as a 404 occurring due to a non-existent route or a 405 occurring due to a caller using an invalid HTTP method on an existing endpoint.
app.UseStatusCodePages();

app.UseStaticFiles();

if (app.Environment.IsDevelopment())
{
    app.ConfigureSwaggerUI(builder.Environment.ApplicationName);
}

app.InitializeDatabase();

app.UseHttpsRedirection();
app.UseHsts();

app.UseCors("DemoCorsPolicy");

////app.UseAuthentication();
////app.UseAuthorization();

app.Run();

// Make the implicit Program class public so test projects can access it
public partial class Program
{
}
global using System.Text;
global using System.Text.Json;

global using Asp.Versioning;
global using Asp.Versioning.ApiExplorer;
global using Atc.Rest.MinimalApi.Extensions;
global using Atc.Rest.MinimalApi.Filters.Swagger;
global using Atc.Rest.MinimalApi.Middleware;
global using Atc.Rest.MinimalApi.Versioning;
global using Atc.Serialization;

global using Demo.Api.Contracts;
global using Demo.Api.Extensions;
global using Demo.Api.Options;
global using Demo.Domain;
global using Demo.Domain.Extensions;

global using FluentValidation;

global using Microsoft.AspNetCore.HttpLogging;
global using Microsoft.AspNetCore.Mvc;
global using Microsoft.Extensions.Logging.ApplicationInsights;
global using Microsoft.Extensions.Options;
global using Microsoft.OpenApi.Models;

global using Swashbuckle.AspNetCore.SwaggerGen;
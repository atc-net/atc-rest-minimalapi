global using System.Diagnostics.CodeAnalysis;
global using System.Net;
global using System.Net.Mime;
global using System.Runtime.CompilerServices;
global using System.Text;
global using System.Text.Json;
global using System.Text.Json.Serialization;
global using System.Text.RegularExpressions;

global using Atc.Rest.MinimalApi.Abstractions;
global using Atc.Rest.MinimalApi.Extensions;
global using Atc.Rest.MinimalApi.Extensions.Internal;
global using FluentValidation;

global using Microsoft.AspNetCore.Builder;
global using Microsoft.AspNetCore.Http;
global using Microsoft.AspNetCore.Http.HttpResults;
global using Microsoft.AspNetCore.Http.Metadata;
global using Microsoft.AspNetCore.Mvc;
global using Microsoft.AspNetCore.Mvc.ApiExplorer;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.OpenApi.Any;
global using Microsoft.OpenApi.Models;

global using MiniValidation;

global using Swashbuckle.AspNetCore.SwaggerGen;
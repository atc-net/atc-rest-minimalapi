namespace Atc.Rest.MinimalApi.Abstractions;

public interface IEndpointDefinition
{
    void DefineEndpoints(WebApplication app);
}
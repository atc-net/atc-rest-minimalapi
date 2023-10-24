namespace Atc.Rest.MinimalApi.Abstractions;

public interface IEndpointAndServiceDefinition : IEndpointDefinition
{
    void DefineServices(
        IServiceCollection services);
}
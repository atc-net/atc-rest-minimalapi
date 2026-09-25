namespace Demo.AppHost;

public static class HealthCheckEndpoints
{
    public const string Aliveness = "/alive";
    public const string Health = "/health";
    public const string Extended = "/health-extended";
}
namespace Demo.Api;

internal static class Telemetry
{
    /// <summary>
    /// OpenTelemetry service.name for this workload. Combined with
    /// <c>TelemetryConstants.ServiceNamespace</c> it forms the App Insights AppRoleName
    /// <c>[demo]/api</c>, which the health-check and exception alert rules match on.
    /// </summary>
    internal const string ServiceName = "api";

    /// <summary>
    /// Version reported as the OpenTelemetry resource service.version. This project is the
    /// entry assembly, so its own informational version is the deployed version.
    /// </summary>
    internal static string? ServiceVersion { get; } = typeof(Telemetry)
        .Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
        .InformationalVersion;
}
namespace Demo.Api;

#pragma warning disable CA1034
public static class TelemetryConstants
{
    /// <summary>
    /// OpenTelemetry service.namespace shared by every workload.
    /// </summary>
    public const string ServiceNamespace = "demo.api";

    public static class Tags
    {
        /// <summary>
        /// OpenTelemetry semantic-convention attribute describing what went wrong. Set only on
        /// failed measurements; absent on the happy path. The value must be a low-cardinality,
        /// stable identifier such as an exception type name.
        /// </summary>
        /// <remarks>
        /// There is deliberately no device-identifying tag here, and none should be added.
        /// DeviceManagementId and device ids must never become metric or span tags: the
        /// cardinality is unbounded, and the values identify customer equipment.
        /// </remarks>
        public const string ErrorType = "error.type";
    }
}
#pragma warning restore CA1034
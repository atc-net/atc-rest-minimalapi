namespace Atc.Rest.MinimalApi;

internal static class LoggingEventIdConstants
{
    internal static class GlobalErrorHandlingMiddleware
    {
        public const int UnhandledServerError = 10_000;
        public const int HandledClientError = 10_001;
        public const int RequestAborted = 10_002;
    }
}
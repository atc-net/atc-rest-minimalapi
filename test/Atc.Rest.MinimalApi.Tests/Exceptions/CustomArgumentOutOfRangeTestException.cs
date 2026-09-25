namespace Atc.Rest.MinimalApi.Tests.Exceptions;

/// <summary>
/// Test exception derived from <see cref="ArgumentOutOfRangeException"/>, which in turn derives from
/// <see cref="ArgumentException"/>. Both base types have default mappings, so the most-derived one must win.
/// </summary>
public sealed class CustomArgumentOutOfRangeTestException : ArgumentOutOfRangeException
{
    public CustomArgumentOutOfRangeTestException()
    {
    }

    public CustomArgumentOutOfRangeTestException(string message)
        : base(paramName: null, message)
    {
    }

    public CustomArgumentOutOfRangeTestException(
        string message,
        Exception innerException)
        : base(message, innerException)
    {
    }
}
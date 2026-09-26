namespace Atc.Rest.MinimalApi.Tests.Exceptions;

/// <summary>
/// Test exception derived from <see cref="FileNotFoundException"/>, which in turn derives from <see cref="IOException"/>.
/// It is not mapped itself, so its status code comes from the nearest mapped base type.
/// </summary>
public sealed class CustomFileNotFoundTestException : FileNotFoundException
{
    public CustomFileNotFoundTestException()
    {
    }

    public CustomFileNotFoundTestException(string message)
        : base(message)
    {
    }

    public CustomFileNotFoundTestException(
        string message,
        Exception innerException)
        : base(message, innerException)
    {
    }
}
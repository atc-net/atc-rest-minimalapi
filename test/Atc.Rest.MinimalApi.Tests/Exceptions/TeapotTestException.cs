namespace Atc.Rest.MinimalApi.Tests.Exceptions;

/// <summary>
/// Test exception for demonstrating custom exception mapping.
/// </summary>
public sealed class TeapotTestException : Exception
{
    public TeapotTestException()
    {
    }

    public TeapotTestException(string message)
        : base(message)
    {
    }

    public TeapotTestException(
        string message,
        Exception innerException)
        : base(message, innerException)
    {
    }
}
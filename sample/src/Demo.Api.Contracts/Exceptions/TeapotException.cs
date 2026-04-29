namespace Demo.Api.Contracts.Exceptions;

/// <summary>
/// Custom exception for demonstrating custom exception-to-HTTP-status-code mapping.
/// Maps to HTTP 418 "I'm a teapot".
/// </summary>
public sealed class TeapotException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TeapotException"/> class.
    /// </summary>
    public TeapotException()
        : base("I'm a teapot!")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TeapotException"/> class.
    /// </summary>
    /// <param name="message">The exception message.</param>
    public TeapotException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TeapotException"/> class.
    /// </summary>
    /// <param name="message">The exception message.</param>
    /// <param name="innerException">The inner exception.</param>
    public TeapotException(
        string message,
        Exception innerException)
        : base(message, innerException)
    {
    }
}
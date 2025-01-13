using System;

namespace IRAAS.Exceptions;

public class InvalidProcessingOptionsException : ArgumentException
{
    public InvalidProcessingOptionsException(
        string message): base(message)
    {
    }
}
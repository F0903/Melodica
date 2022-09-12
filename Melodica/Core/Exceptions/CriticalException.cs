namespace Melodica.Core.Exceptions;

public sealed class CriticalException : Exception
{
    public CriticalException(string? msg = null, Exception? innerEx = null) : base(msg, innerEx)
    {
    }
}

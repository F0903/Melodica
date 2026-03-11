namespace Melodica.Core.Exceptions;

public sealed class CriticalException(string? msg = null, Exception? innerEx = null) : Exception(msg, innerEx)
{
}

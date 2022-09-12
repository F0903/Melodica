namespace Melodica.Services.Downloaders.Exceptions;

sealed class UrlNotSupportedException : Exception
{
    public UrlNotSupportedException(string? msg = null, Exception? inner = null) : base(msg, inner)
    { }
}

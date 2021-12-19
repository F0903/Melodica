namespace Melodica.Services.Playback.Exceptions;

[Serializable]
internal class EmptyChannelException : JukeboxException
{
    public EmptyChannelException(string? message = null, Exception? innerException = null) : base(message, innerException)
    { }
}

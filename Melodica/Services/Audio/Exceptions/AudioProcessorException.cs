namespace Melodica.Services.Audio.Exceptions;

public sealed class AudioProcessorException : Exception
{
    public AudioProcessorException(string? msg = null, Exception? ex = null) : base(msg, ex)
    {
    }
}

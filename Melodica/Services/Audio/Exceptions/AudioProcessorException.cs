namespace Melodica.Services.Audio.Exceptions;

public class AudioProcessorException : Exception
{
    public AudioProcessorException(string? msg = null, Exception? ex = null) : base(msg, ex)
    {
    }
}

using System;
using System.Runtime.Serialization;

namespace Melodica.Services.Playback.Exceptions
{
    [Serializable]
    internal class EmptyChannelException : Exception
    {
        public EmptyChannelException(string? message = null, Exception? innerException = null) : base(message, innerException)
        {
        }
    }
}
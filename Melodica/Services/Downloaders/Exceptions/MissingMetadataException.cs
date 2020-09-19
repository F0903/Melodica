using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Melodica.Services.Downloaders.Exceptions
{
    public class MissingMetadataException : DownloaderException
    {
        public MissingMetadataException(string? msg = null, Exception? inner = null) : base(msg, inner) { }
    }
}

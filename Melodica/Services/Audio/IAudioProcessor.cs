using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Melodica.Services.Audio;
internal interface IAsyncAudioProcessor : IDisposable
{
    public ValueTask<Stream> ProcessAsync();
}

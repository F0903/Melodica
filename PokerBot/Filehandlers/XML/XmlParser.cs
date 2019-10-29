using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
using System.Xml.Linq;
using System.Threading.Tasks;

namespace PokerBot.Filehandlers.XML
{
    public static class XmlParser
    {
        public static async Task<T> ReadContentAsync<T>(string nodeName, string path = "./Settings.xml")
        {
            using var file = File.OpenRead(path);
            var doc = await XDocument.LoadAsync(file, LoadOptions.None, new System.Threading.CancellationToken(false));
            return (T)Convert.ChangeType(doc.Root.Descendants().Single(x => x.Name == nodeName).Value, typeof(T));
        }
    }
}

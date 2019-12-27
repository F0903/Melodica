using System;
using System.Collections.Generic;
using System.Text;

namespace CasinoBot.Modules.Jukebox.Models.Requests
{
    public class ExistingMediaRequest : IRequest
    {
        public ExistingMediaRequest(MediaCollection media)
        {
            this.media = media;
        }

        public bool IsDownloadRequest { get; } = false;

        private readonly MediaCollection media;

        public object GetRequest()
        {
            return media;
        }

        public Services.Downloaders.IAsyncDownloadService GetDownloader()
        {
            return null;
        }
    }
}

using CasinoBot.Modules.Jukebox.Services.Downloaders;
using System;
using System.Collections.Generic;
using System.Text;

namespace CasinoBot.Modules.Jukebox.Models.Requests
{
    public interface IRequest
    {
        public bool IsDownloadRequest { get; }

        public object GetRequest();

        public IAsyncDownloadService GetDownloader();
    }
}

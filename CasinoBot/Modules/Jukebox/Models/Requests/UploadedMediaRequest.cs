using CasinoBot.Modules.Jukebox.Services.Downloaders;
using System;
using System.Net;
using System.Collections.Generic;
using System.Text;

namespace CasinoBot.Modules.Jukebox.Models.Requests
{
    public class UploadedMediaRequest : IRequest
    {
        public UploadedMediaRequest(string url, string name)
        {
            using var client = new WebClient();
            this.data = client.DownloadData(url);

            this.name = name;
        }

        private readonly string name;
        private readonly byte[] data;

        public bool IsDownloadRequest => false;

        public object GetRequest() => data;

        public IAsyncDownloadService GetDownloader() => throw new NotImplementedException();
    }
}

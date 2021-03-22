using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;

using Melodica.Services.Media;

namespace Melodica.Services.Playback.Requests
{
    //TODO:
    public class AttachmentMediaRequest : IMediaRequest
    {
        public AttachmentMediaRequest(Discord.Attachment[] attachments) => attachment = attachments[0];

        private readonly Discord.Attachment? attachment;

        private MediaInfo? info;

        public Task<MediaInfo> GetInfoAsync()
        {
            throw new NotImplementedException();
        }

        public async Task<MediaCollection> GetMediaAsync()
        {
            throw new NotImplementedException();
        }
    }
}
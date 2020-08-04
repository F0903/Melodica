using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Melodica.Utility.Extensions;
using Melodica.Services.Jukebox.Models.Requests;

namespace Melodica.Services.Jukebox.Models
{
    public class SongQueue
    {
        private readonly object locker = new object();

        private readonly List<MediaRequestBase> list = new List<MediaRequestBase>();

        public bool IsEmpty { get => list.Count == 0; }

        public int Length { get => list.Count; }

        public MediaRequestBase this[int i]
        {
            get => list[i];
        }

        public TimeSpan GetTotalDuration() => list.Sum(x => x.GetInfo().Duration);

        public MediaRequestBase[] ToArray()
        {
            lock (locker)
                return list.ToArray();
        }

        public MediaMetadata GetMediaInfo() => new MediaMetadata() { Duration = GetTotalDuration(), Thumbnail = list[0].GetInfo().Thumbnail };

        public Task EnqueueAsync(MediaRequestBase item)
        {
            list.Add(item);
            return Task.CompletedTask;
        }

        public Task EnqueueAsync(IEnumerable<MediaRequestBase> items)
        {
            list.AddRange(items);
            return Task.CompletedTask;
        }

        public Task PutFirst(MediaRequestBase item)
        {
            list.Insert(0, item);
            return Task.CompletedTask;
        }

        public Task PutFirst(IEnumerable<MediaRequestBase> items)
        {
            list.InsertRange(0, items);
            return Task.CompletedTask;
        }

        public Task<MediaRequestBase> DequeueRandomAsync(bool keep = false)
        {
            var rng = new Random();
            MediaRequestBase item;
            lock (locker)
            {
                item = list[rng.Next(0, list.Count)];
                if(!keep) list.Remove(item);
            }
            return Task.FromResult(item);
        }

        public Task<MediaRequestBase> DequeueAsync(bool keep = false)
        {
            MediaRequestBase item;
            lock (locker)
            {
                item = list[0];
                if(!keep) list.Remove(item);
            }
            return Task.FromResult(item);
        }

        public Task ClearAsync()
        {
            list.Clear();
            return Task.CompletedTask;
        }

        public Task<MediaRequestBase> RemoveAtAsync(int index)
        {
            if (index < 0)
                throw new Exception("Index cannot be under 0.");
            else if (index > list.Count)
                throw new Exception("Index cannot be larger than the queues size.");

            MediaRequestBase item;
            lock (locker)
            {
                item = list[index];
                list.RemoveAt(index);
            }
            return Task.FromResult(item);
        }
    }
}
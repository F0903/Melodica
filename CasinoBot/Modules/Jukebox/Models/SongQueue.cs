using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using CasinoBot.Utility.Extensions;

namespace CasinoBot.Modules.Jukebox.Models
{
    public class SongQueue
    {
        private readonly object locker = new object();

        private readonly List<PlayableMedia> list = new List<PlayableMedia>();

        public bool IsEmpty { get => list.Count == 0; }

        public int Length { get => list.Count; }

        public PlayableMedia this[int i]
        {
            get => list[i]; 
        }

        public TimeSpan GetTotalDuration() => list.Sum(x => x.Meta.Duration);

        public PlayableMedia[] ToArray()
        {
            lock (locker)
                return list.ToArray();
        }

        public Task UnsafeEnqueueAsync(PlayableMedia item)
        {
            list.Add(item);
            return Task.CompletedTask;
        }

        public Task UnsafeEnqueueAsync(PlayableMedia[] items)
        {
            list.AddRange(items);
            return Task.CompletedTask;
        }

        public Task EnqueueAsync(PlayableMedia[] items)
        {
            lock (locker)
                list.AddRange(items);
            return Task.CompletedTask;
        }

        public Task EnqueueAsync(PlayableMedia item)
        {
            lock (locker)
                list.Add(item);
            return Task.CompletedTask;
        }

        public Task<PlayableMedia> DequeueRandomAsync()
        {
            var rng = new Random();
            PlayableMedia item;
            lock (locker)
            {
                item = list[rng.Next(0, list.Count)];
                list.Remove(item);
            }
            return Task.FromResult(item);
        }

        public Task<PlayableMedia> DequeueAsync()
        {
            PlayableMedia item;
            lock (locker)
            {
                item = list[0];
                list.Remove(item);
            }
            return Task.FromResult(item);
        }

        public Task ClearAsync()
        {
            list.Clear();
            return Task.CompletedTask;
        }

        public Task<PlayableMedia> RemoveAtAsync(int index)
        {
            PlayableMedia item;
            lock (locker)
            {
                item = list[index];
                list.RemoveAt(index);
            }
            return Task.FromResult(item);
        }
    }
}
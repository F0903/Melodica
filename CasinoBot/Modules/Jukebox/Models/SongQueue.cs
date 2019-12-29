using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CasinoBot.Modules.Jukebox.Models
{
    public class SongQueue<T>
    {
        private readonly object locker = new object();

        private readonly List<T> list = new List<T>();

        public bool IsEmpty { get => list.Count == 0; }

        public T[] ToArray()
        {
            lock (locker)
                return list.ToArray();
        }

        public Task UnsafeEnqueueAsync(T item)
        {
            list.Add(item);
            return Task.CompletedTask;
        }

        public Task UnsafeEnqueueAsync(T[] items)
        {
            list.AddRange(items);
            return Task.CompletedTask;
        }

        public Task EnqueueAsync(T[] items)
        {
            lock (locker)
                list.AddRange(items);
            return Task.CompletedTask;
        }

        public Task EnqueueAsync(T item)
        {
            lock (locker)
                list.Add(item);
            return Task.CompletedTask;
        }

        public Task<T> DequeueRandomAsync()
        {
            var rng = new Random();
            T item;
            lock (locker)
            {
                item = list[rng.Next(0, list.Count)];
                list.Remove(item);
            }
            return Task.FromResult(item);
        }

        public Task<T> DequeueAsync()
        {
            T item;
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

        public Task<T> RemoveAtAsync(int index)
        {
            T item;
            lock (locker)
            {
                item = list[index];
                list.RemoveAt(index);
            }
            return Task.FromResult(item);
        }
    }
}
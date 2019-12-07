﻿using System;
using System.Collections.Generic;
using System.Text;

namespace PokerBot.Services.Jukebox
{
    public sealed class JukeboxDictionary<Key, Val>
    {
        public JukeboxDictionary(int? capacity = null)
        {
            if (!capacity.HasValue)
                cache = new Dictionary<Key, Val>();
            else
                cache = new Dictionary<Key, Val>(capacity.Value);
        }

        private readonly Dictionary<Key, Val> cache;

        public void AddEntry(Key key, Val val)
        {
            lock (cache)
                cache.Add(key, val);
        }

        public bool TryGetEntry(Key key, out Val val)
        {
            lock (cache)
                return cache.ContainsKey(key) ? (val = this[key]) != null : (val = default) != default;
        }

        public Val GetEntry(Key key)
        {
            lock (cache)
                return cache[key];
        }

        public void RemoveEntry(Key key)
        {
            lock (cache)
                cache.Remove(key);
        }

        public Val this[Key key]
        {
            get => GetEntry(key);
            set
            {
                if (!cache.ContainsKey(key))
                    throw new NullReferenceException("Key did not exist in JukeboxCache.");
                cache[key] = value;
            }
        }
    }
}
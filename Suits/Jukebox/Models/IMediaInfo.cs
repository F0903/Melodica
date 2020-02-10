﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Suits.Jukebox.Models
{
    public interface IMediaInfo
    {
        public string GetTitle();
        public TimeSpan GetDuration();
    }
}
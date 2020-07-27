﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Melodica.Jukebox.Models.Exceptions
{
    public class MissingMetadataException : Exception
    {
        public MissingMetadataException(string? msg = null) : base(msg) { }
    }
}
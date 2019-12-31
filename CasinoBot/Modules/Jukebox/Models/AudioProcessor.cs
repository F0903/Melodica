﻿using Discord;
using System;
using System.Diagnostics;
using System.IO;

#nullable enable

namespace CasinoBot.Modules.Jukebox.Models
{
    public sealed class AudioProcessor : IDisposable
    {
        public AudioProcessor(string? path, int bitrate, int bufferSize, string? format = null)
        {
            playerProcess = new Process()
            {
                StartInfo = new ProcessStartInfo()
                {
                    FileName = "ffmpeg.exe",
                    Arguments = $"-hide_banner -loglevel fatal -vn {(format != null ? $"-f {format}" : string.Empty)} -i {$"\"{path}\"" ?? "pipe:0"} -f s16le -bufsize {bufferSize} -b:a {bitrate} -ac 2 -ar 48000 -y pipe:1", // -filter:a dynaudnorm=b=1:c=1:n=0:r=0.2
                    UseShellExecute = false,
                    RedirectStandardError = false,
                    RedirectStandardInput = (inputAvailable = (path == null ? true : false)),
                    RedirectStandardOutput = (outputAvailable = true),
                    CreateNoWindow = true,
                }
            };

            playerProcess.Start();
        }

        private readonly Process playerProcess;

        private readonly bool inputAvailable;
        private readonly bool outputAvailable;

        public Process GetBaseProcess() => playerProcess;

        public Stream? GetInput() => inputAvailable ? playerProcess.StandardInput.BaseStream : null;

        public Stream? GetOutput() => outputAvailable ? playerProcess.StandardOutput.BaseStream : null;

        public void Stop() => Dispose();

        public void Dispose()
        {
            if (inputAvailable)
                playerProcess.StandardInput.Dispose();
            if (outputAvailable)
                playerProcess.StandardOutput.Dispose();

            playerProcess.Kill();
            if (!playerProcess.HasExited)
                playerProcess.WaitForExit();
        }
    }
}
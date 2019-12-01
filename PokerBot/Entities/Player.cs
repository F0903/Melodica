using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace PokerBot.Entities
{
    public class Player
    {
        public Player(Process playerProcess)
        {
            this.playerProcess = playerProcess;

            playerProcess.Start();
        }

        private readonly Process playerProcess;

        public Process GetBaseProcess() => playerProcess;

        public Stream GetStdOut() => playerProcess.StandardOutput.BaseStream;
        public Stream GetStdIn() => playerProcess.StandardInput.BaseStream;

        public void Stop() => playerProcess.Close();

    }
}

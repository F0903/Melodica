using Suits.Core.Filehandlers.XML;
using System;
using Discord;
using Suits.Core.Services;
using System.IO;

namespace Suits.Core
{
    [Serializable]
    public class BotSettings
    {
        public static BotSettings GetOrSet(Func<BotSettings>? custom = null)
        {
            var data = LoadData();
            if (data != null)
                return data;
            var val = custom != null ? custom() : new BotSettings();
            val.SaveData();
            return val;
        }

        public const string SettingsExtension = ".ss";

        private const string SettingsPath = "./Settings/Bot" + SettingsExtension;

        private static readonly IAsyncSerializer serializer = new BinarySerializer();
        public string Token { get; } = "NTcxNDAwNTc4MDY0ODQyNzUy.XpPE3A.AmuPUeQUtAi2Aztr3lraWM89vSw";

        private LogSeverity logSeverity = LogSeverity.Debug;
        public LogSeverity LogSeverity
        {
            get => logSeverity;
            set
            {
                logSeverity = value;
                SaveData();
            }
        }

        private int maxFileCacheInMB = 1000;
        public int MaxFileCacheInMB
        {
            get => maxFileCacheInMB;
            set
            {
                maxFileCacheInMB = value;
                SaveData();
            }
        }

        private void SaveData()
        {
            var dirName = Path.GetDirectoryName(SettingsPath);
            if (!Directory.Exists(dirName))
                Directory.CreateDirectory(dirName);
            serializer.SerializeToFileAsync(SettingsPath, this);
        }

        private static BotSettings? LoadData()
        {
            if (!File.Exists(SettingsPath))
                return null;
            return serializer.DeserializeFileAsync<BotSettings>(SettingsPath).Result;
        }
    }
}

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
        private BotSettings() { }

        public static BotSettings Get(bool overrideOld = false)
        {
            var loadedData = LoadData();
            var data = overrideOld ? new BotSettings() : loadedData ?? new BotSettings();
            if (overrideOld || loadedData == null)
                data.SaveData();
            return data;
        }

        private const string settingsDir = "./Settings/";
        public static string SettingsDir 
        {
            get
            {
                if (!Directory.Exists(settingsDir))
                    Directory.CreateDirectory(settingsDir);
                return settingsDir;
            } 
        }

        public const string SettingsExtension = ".ss";

        private static readonly string BotSettingsPath = SettingsDir + "Bot" + SettingsExtension;

        private static readonly IAsyncSerializer serializer = new BinarySerializer();

        // For safety reasons, this should probably not be set here.
        public string Token { get; } = "NzA0Mjg3MjM0NTEzMzA1NzAw.Xqa-lg.5uK_cMps1lH9MQELZyj4dM25hpc";

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
            serializer.SerializeToFileAsync(BotSettingsPath, this);
        }

        private static BotSettings? LoadData()
        {
            if (!File.Exists(BotSettingsPath))
                return null;
            return serializer.DeserializeFileAsync<BotSettings>(BotSettingsPath).Result;
        }
    }
}

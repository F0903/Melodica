using System;
using Discord;
using Melodica.Services;
using System.IO;
using System.Diagnostics;
using Melodica.Services.Serialization;

namespace Melodica.Core
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

        public const ProcessPriorityClass ProcessPriority = ProcessPriorityClass.AboveNormal;

        public const string SettingsExtension = ".melodica";

        private static readonly string BotSettingsPath = SettingsDir + "Bot" + SettingsExtension;

        private static readonly IAsyncSerializer serializer = new BinarySerializer();

        // For safety reasons, this should probably not be set here.
        public string Token { get; } = "NTcxNDAwNTc4MDY0ODQyNzUy.XMNMQQ.8K9ovB1sbkExbYIK2wCI6OxzXSw";

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

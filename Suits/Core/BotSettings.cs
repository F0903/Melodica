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
        public static BotSettings GetOrSetSettings(Func<BotSettings> settingsFactory)
        {
            var data = LoadData();
            if (data != null)
                return data;
            var val = settingsFactory();
            val.SaveData();
            return val;
        }

        public const string SettingsExtension = ".ss";

        private const string SettingsPath = "./Settings/Bot" + SettingsExtension;

        private static readonly IAsyncSerializer serializer = new BinarySerializer();

        private string token = "NTcxNDAwNTc4MDY0ODQyNzUy.Xbf1bw.ZJnOjpza1owjye-gKi5YWoMrrkE";
        public string Token
        {
            get => token;
            private set
            {
                token = value;
                SaveData();
            }
        }

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
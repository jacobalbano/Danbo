using Danbo.Utility;
using System.Text.Json;

namespace Danbo
{
    public class ConfigFile
    {
        public string Token { get; init; } = "__CUSTOMIZE__";
        public int TickMilliseconds { get; init; } = 1000;
        public bool CheckpointEveryMutation { get; init; } = false;

        public static ConfigFile Prepare()
        {
            if (File.Exists(ConfigFileName))
            {
                var contents = File.ReadAllText(ConfigFileName);
                var config = JsonSerializer.Deserialize<ConfigFile>(contents);
                if (config != null)
                    return config;
            }

            try { File.WriteAllText(ConfigFileName, JsonSerializer.Serialize(new ConfigFile())); }
            catch (Exception e) { throw new Exception($"Unable to load a config file from '{ConfigFileName}'. An error occurred while trying to create a template.", e); }
            throw new Exception($"Unable to load a config file from '{ConfigFileName}'. A template file has been created.");
        }

        private const string ConfigFileName = "config.json";
    }
}
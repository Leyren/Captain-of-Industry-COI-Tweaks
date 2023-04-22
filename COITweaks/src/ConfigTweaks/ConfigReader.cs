using System;
using System.CodeDom;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;

namespace COITweaks
{
    internal class ConfigReader
    {
        // Property names in .ini file
        public static readonly string ENABLE = "Enable";
        public static readonly string PILLAR_TWEAKS_ENABLE = "PillarTweaks.Enable";
        public static readonly string CONFIG_TWEAKS_ENABLE = "ConfigTweaks.Enable";

        public static readonly string FLAT_CONVEYOR_SUPPORT_RADIUS = "FlatConveyor.MaxPillarSupportRadius";
        public static readonly string LOOSE_MATERIAL_CONVEYOR_SUPPORT_RADIUS = "LooseMaterialConveyor.MaxPillarSupportRadius";
        public static readonly string PIPE_SUPPORT_RADIUS = "Pipe.MaxPillarSupportRadius";
        public static readonly string MOLTEN_METAL_CHANNEL_SUPPORT_RADIUS = "MoltenMetalChannel.MaxPillarSupportRadius";
        public static readonly string SHAFT_SUPPORT_RADIUS = "Shaft.MaxPillarSupportRadius";

        public static readonly string MAX_PILLAR_HEIGHT = "MaxPillarHeight";

        private Dictionary<string, string> config;
        private readonly Logger logger = Logger.WithName("Config Reader");
        private static readonly string filename = "coitweaks_config.ini";

        private static ConfigReader _instance;

        private ConfigReader() {
            string assembly = Assembly.GetAssembly(typeof(COITweaks)).Location;
            string dir = Path.GetDirectoryName(assembly);
            string path = Path.Combine(dir, filename);
            logger.Info($"Instantiating configuration at {path}");

            if (!File.Exists(path))
            {
                logger.Info("File not found, creating new file");
                InitializeConfigFile(path);
            } 
            ReadConfig(path);
        } 

        public void InitializeConfigFile(string path)
        {
            // Get current assembly
            var assembly = Assembly.GetAssembly(typeof(COITweaks));
            logger.Info("Assembly resources: "+String.Join(", ", assembly.GetManifestResourceNames()));
            var resourceName = "COITweaks.src.Resources." + filename;

            string template = string.Empty;
            logger.Info("Try finding resource name: " + resourceName);

            // Read resource from assembly
            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            using (StreamReader reader = new StreamReader(stream))
            {
                template = reader.ReadToEnd();
            }

            // Write data in the new text file
            using (TextWriter writer = File.CreateText(path))
            {
                writer.WriteLine(template);
            }
        }

        public static ConfigReader Instance()
        {
            if (_instance == null)
            {
               _instance = new ConfigReader();
            }
            return _instance;  
        }

        private void ReadConfig(string path)
        {
            config = new Dictionary<string, string>();
            string[] lines = File.ReadAllLines(path);

            foreach (string line in lines)
            {
                if (line.StartsWith("[") && line.EndsWith("]") || line.StartsWith(";") || line.StartsWith("#"))
                {
                    continue;
                }

                string[] lineParts = Regex.Split(line, "\\s*=\\s*");

                if (lineParts.Length >= 2)
                {
                    logger.Info($"Read config line {lineParts[0]} = {lineParts[1]}");
                    config.Add(lineParts[0], lineParts[1]);
                }
            }
        }

        public string GetValue(string key, string defaultValue)
        {
            return config.ContainsKey(key) ? config[key] : defaultValue;
        }

        public string GetValue(string key)
        {
            return config.ContainsKey(key) ? config[key] : null;
        }

        public int GetInt(string key, int defaultValue)
        {
            return config.ContainsKey(key) && int.TryParse(config[key], out var result) ? result : defaultValue;
        }
        public int? GetInt(string key)
        {
            if (config.ContainsKey(key) && int.TryParse(config[key], out var result)) return result;
            return null;
        }

        public void ProcessInt(string key, Action<int, string> callback)
        {
            int? value = GetInt(key);
            if (value != null) callback.Invoke((int)value, key);
        }

        public bool GetBool(string key, bool defaultValue)
        {
            return config.ContainsKey(key) && bool.TryParse(config[key], out var result) ? result : defaultValue;
        }
        public bool? GetBool(string key)
        {
            if (config.ContainsKey(key) && bool.TryParse(config[key], out var result)) return result;
            return null;
        }

    }
}

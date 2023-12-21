/*
* Copyright(c) 2023 GiR-Zippo
* Licensed under the Mozilla Public License Version 2.0. See https://github.com/GiR-Zippo/XIV-Modexplorer/blob/main/LICENSE for full license information.
*/

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

namespace XIVModExplorer
{
    public static class Configuration
    {
        public static Dictionary<string, string> ConfigValues = new Dictionary<string, string>();

        public static string GetValue(string var)
        {
            if (ConfigValues.ContainsKey(var))
                return ConfigValues[var];
            return "";
        }

        public static bool GetBoolValue(string var)
        {
            if (ConfigValues.ContainsKey(var))
                return Convert.ToBoolean(ConfigValues[var]);
            return false;
        }

        public static void SetValue(string var, string value)
        {
            if (ConfigValues.ContainsKey(var))
                ConfigValues[var] = value;
            else
                ConfigValues.Add(var, value);
            SaveConfig();
        }

        public static void ReadConfig()
        {
            if (!File.Exists(AppContext.BaseDirectory + "\\Config.json"))
                return;
            string json = File.ReadAllText(AppContext.BaseDirectory + "\\Config.json");
            ConfigValues = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
        }

        public static void SaveConfig()
        {
            String json = JsonConvert.SerializeObject(ConfigValues, Formatting.Indented);
            File.WriteAllText(AppContext.BaseDirectory + "\\Config.json", json);
        }
        ////HttpUtility.UrlDecode(HttpUtility.UrlEncode(diff.OriginalString));
        public static string GetRelativeModPath(string filename)
        {
            Uri path1 = new Uri(GetValue("ModArchivePath"));
            Uri path2 = new Uri(filename);
            Uri diff = path1.MakeRelativeUri(path2);
            return Uri.UnescapeDataString(diff.OriginalString);


        }

        public static string GetAbsoluteModPath(string filename)
        {
            return GetValue("ModArchivePath") + filename;
        }
    }
}

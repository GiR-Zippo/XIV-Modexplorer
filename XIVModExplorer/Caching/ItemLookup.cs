/*
* Copyright(c) 2024 GiR-Zippo
* Licensed under the Mozilla Public License Version 2.0. See https://github.com/GiR-Zippo/XIV-Modexplorer/blob/main/LICENSE for full license information.
*/

using CsvHelper.Configuration;
using CsvHelper;
using System.Globalization;
using System.IO;
using System;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;
using System.Collections.Generic;

namespace XIVModExplorer.Caching
{
    public static class ItemLookup
    {
        /// <summary>
        /// Get the affected item from string using https://raw.githubusercontent.com/xivapi/ffxiv-datamining/master/csv/Item.csv
        /// </summary>
        /// <param name="Search"></param>
        /// <returns></returns>
        public static Type GetItem(string Search)
        {
            if (Search == "")
                return Type.NONE;

            return getItem(Search);
        }

        private static Type getItem(string Search)
        { 
            if (!File.Exists("Item.csv"))
                return Type.NONE;

            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,

            };
            using (var reader = new StreamReader("Item.csv"))
            using (var csv = new CsvParser(reader, config))
            {
                while (csv.Read())
                {
                    if (csv.Record[10].ToLower() == Search.ToLower())
                    {
                        switch (Convert.ToInt32(csv.Record[18]))
                        {
                            case 1:
                            case 2:
                                return Type.WEAPON;
                            case 3:
                                return Type.HEAD;
                            case 4:
                                return Type.TOP;
                            case 5:
                                return Type.HANDS;
                            case 6:
                            case 7:
                                return Type.BOTTOM;
                            case 8:
                                return Type.SHOE;
                            case 9:
                                return Type.EAR;
                            case 10:
                                return Type.NECK;
                            case 11:
                                return Type.ARM;
                            case 12:
                            case 13:
                                return Type.FINGER;
                        }
                    }
                }
            }
            return Type.NONE;
        }

        public static Type GetTypeByCategory(string Search)
        {
            switch (Search)
            {
                case "MainHand":
                case "OffHand":
                    return Type.WEAPON;
                case "Head":
                    return Type.HEAD;
                case "Body":
                    return Type.TOP;
                case "Gloves":
                case "Hands":
                    return Type.HANDS;
                case "Waist":
                case "Legs":
                    return Type.BOTTOM;
                case "Feet":
                    return Type.SHOE;
                case "Ears":
                    return Type.EAR;
                case "Neck":
                    return Type.NECK;
                case "Wrists":
                    return Type.ARM;
                case "Rings":
                case "FingerL":
                case "FingerR":
                    return Type.FINGER;
            }
            return Type.NONE;
        }


        public static UInt32 GetAffectedItems(string filename)
        {
            UInt32 mod = 0;
            List<string> foundFiles = new List<string>();

            string x = App.TempPath + Path.GetFileNameWithoutExtension(filename);
            Directory.CreateDirectory(x);
            {
                using (Stream stream = File.OpenRead(filename))
                {
                    var reader = ReaderFactory.Open(stream);
                    while (reader.MoveToNextEntry())
                    {
                        //Extract normal ttmp2 and pmb
                        if (!reader.Entry.IsDirectory)
                        {
                            if (reader.Entry.Key.EndsWith(".ttmp") || reader.Entry.Key.EndsWith(".ttmp2") || reader.Entry.Key.EndsWith(".pmp"))
                            {
                                try
                                {
                                    reader.WriteEntryToDirectory(x, new ExtractionOptions() { ExtractFullPath = true, Overwrite = true });
                                    foundFiles.Add(reader.Entry.Key);
                                }
                                catch (ArgumentException)
                                {
                                    string newName = x + "\\" + Utils.Util.MakeValidFileName(reader.Entry.Key);
                                    using (var entryStream = reader.OpenEntryStream())
                                    {
                                        var fs = new FileStream(newName, FileMode.Create);
                                        entryStream.CopyTo(fs);
                                        fs.Close();
                                    }
                                    foundFiles.Add(Utils.Util.MakeValidFileName(reader.Entry.Key));
                                }
                            }
                            //Extract archives and mod
                            if (reader.Entry.Key.EndsWith(".zip") || reader.Entry.Key.EndsWith(".rar"))
                            {
                                reader.WriteEntryToDirectory(x, new ExtractionOptions() { ExtractFullPath = true, Overwrite = true });
                                using (Stream innerStream = File.OpenRead(x + "\\" + reader.Entry.Key))
                                {
                                    var innerReader = ReaderFactory.Open(innerStream);
                                    while (innerReader.MoveToNextEntry())
                                    {
                                        if (innerReader.Entry.Key.EndsWith(".ttmp2") || innerReader.Entry.Key.EndsWith(".pmp"))
                                        {
                                            innerReader.WriteEntryToDirectory(x, new ExtractionOptions() { ExtractFullPath = true, Overwrite = true });
                                            foundFiles.Add(innerReader.Entry.Key);
                                        }
                                    }
                                    innerReader.Dispose();
                                }
                            }
                            if (reader.Entry.Key.EndsWith(".7z"))
                            {
                                reader.WriteEntryToDirectory(x, new ExtractionOptions() { ExtractFullPath = true, Overwrite = true });
                                var innerReader = ArchiveFactory.Open(x + "\\" + reader.Entry.Key).ExtractAllEntries();
                                while (innerReader.MoveToNextEntry())
                                {
                                    if (!innerReader.Entry.IsDirectory && (innerReader.Entry.Key.EndsWith(".ttmp2") || innerReader.Entry.Key.EndsWith(".pmp")))
                                    {
                                        innerReader.WriteEntryToDirectory(x, new ExtractionOptions() { ExtractFullPath = true, Overwrite = true });
                                        foundFiles.Add(innerReader.Entry.Key);
                                    }
                                }
                                innerReader.Dispose();
                            }
                        }
                    }
                    reader.Dispose();
                }

                foreach (string file in foundFiles)
                {
                    var archive = ArchiveFactory.Open(x + "/" + file);
                    foreach (var entry in archive.Entries)
                    {
                        if (!entry.IsDirectory)
                        {
                            if (entry.Key.EndsWith(".mpl"))
                            {
                                MemoryStream mem = new MemoryStream();
                                entry.WriteTo(mem);
                                mem.Position = 0;
                                TextReader tr = new StreamReader(mem);

                                using (JsonTextReader reader = new JsonTextReader(tr))
                                {
                                    JObject o2 = (JObject)JToken.ReadFrom(reader);
                                    foreach (var pi in o2["ModPackPages"])
                                        foreach (var modgrp in pi["ModGroups"])
                                            foreach (var optionlist in modgrp["OptionList"])
                                                foreach (var ModsJsons in optionlist["ModsJsons"])
                                                {
                                                    if ((mod & (UInt32)GetTypeByCategory(ModsJsons["Category"].ToString())) != 0)
                                                        continue;
                                                    mod |= (UInt32)GetTypeByCategory(ModsJsons["Category"].ToString());
                                                }
                                }

                                tr.Close();
                                mem.Close();
                            }
                        }
                    }
                    archive.Dispose();
                }
            }
            return mod;
        }
    }
}

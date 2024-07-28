/*
* Copyright(c) 2024 GiR-Zippo
* Licensed under the Mozilla Public License Version 2.0. See https://github.com/GiR-Zippo/XIV-Modexplorer/blob/main/LICENSE for full license information.
*/

using LiteDB;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using XIVModExplorer.HelperWindows;

namespace XIVModExplorer.Caching
{
    public enum Type
    {
        NONE    = 0b00000000000000000000000000000000,
        //Body
        WEAPON  = 0b00000000000000000000000000000001,
        HEAD    = 0b00000000000000000000000000000010,
        TOP     = 0b00000000000000000000000000000100,
        HANDS   = 0b00000000000000000000000000001000,
        BOTTOM  = 0b00000000000000000000000000010000,
        SHOE    = 0b00000000000000000000000000100000,
        EAR     = 0b00000000000000000000000001000000,
        NECK    = 0b00000000000000000000000010000000,
        ARM     = 0b00000000000000000000000100000000,
        FINGER  = 0b00000000000000000000001000000000,

        //Pets
        MINION  = 0b00000000000000000000010000000000,
        MOUNT   = 0b00000000000000000000100000000000,
                //0b00000000000000000001000000000000,
                //0b00000000000000000010000000000000,
                //0b00000000000000000100000000000000,
        ONACC   = 0b00000000000000001000000000000000,    //ists ein mod-accessoire
        BREPLAC = 0b00000000000000100000000000000000,
        HAIR    = 0b00000000000001000000000000000000,
        FACE    = 0b00000000000010000000000000000000,
        SKIN    = 0b00000000000100000000000000000000,

        HOUSING = 0b00001000000000000000000000000000,

        VFX     = 0b00010000000000000000000000000000,
      ANIMATION = 0b00100000000000000000000000000000,
        
        MISC    = 0b01000000000000000000000000000000,


    }

    [Serializable]
    public class ModEntry
    {
        [BsonId]
        public Guid Id { get; set; }
        public string ModName { get; set; } = "";
        public UInt32 ModTypeFlag { get; set; } = 0;
        public UInt32 AccModTypeFlag { get; set; } = 0;
        public string Description { get; set; } = "";
        public byte[] picture { get; set; } = null;
        public string Url { get; set; } = "";
        public string Filename { get; set; } = "";
        public byte[] HashSha1 { get; set; } = null;
        public DateTime CreationDate { get; set; } = DateTime.MinValue;
        public DateTime ModificationDate { get; set; } = DateTime.MinValue;
        public bool FoundInPenumbra { get; set; } = false;
        public string PenumbraName { get; set; } = "";
        public string PenumbraPath { get; set; } = "";
        public bool IsForDT { get; set; } = false;
    }

    public sealed class Database : IDisposable
    {
        private static Database _instance;

        public static void Initialize(string filename)
        {
            if (Initialized) return;
            _instance = CreateInstance(filename);
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    this.dbi.Dispose();
                }
                disposedValue = true;
            }
        }

        public static bool Initialized => _instance != null;
        public static Database Instance => _instance ?? throw new Exception("Init first");

        private readonly LiteDatabase dbi;
        private bool disposedValue;

        private Database(LiteDatabase dbi)
        {
            this.dbi = dbi;
            this.disposedValue = false;
        }

        internal static Database CreateInstance(string dbPath)
        {
            var dbi = new LiteDatabase(@"filename=" + dbPath + "; journal=false");
            return new Database(dbi);
        }

        public void CheckOrphanedEntries()
        {
            Dictionary<string, string> tempDict = new Dictionary<string, string>();
            var list = this.dbi.GetCollection<ModEntry>().FindAll().ToList();
            foreach (var t in list)
            {
                if (t.Filename != null && t.Filename.Length != 0)
                {
                    if (!File.Exists(Configuration.GetAbsoluteModPath(t.Filename)))
                        tempDict[t.Filename] = t.Filename;
                }
            }
            if (tempDict.Count == 0)
            {
                MessageWindow.Show("None found.");
                return;
            }
            DataGridInput li = new DataGridInput(tempDict, "Select to remove");
            var f = li.ShowDialog();
            if (f.Count() != 0)
            {
                foreach (var t in f)
                {
                    var item = list.Find(i => i.Filename.Equals(t));
                    if (item != null)
                        this.dbi.GetCollection<ModEntry>().Delete(item.Id);
                }
            }
            tempDict.Clear();
            list.Clear();
        }

        public void Optimize(bool full = false)
        {
            LogWindow.Message("[Database - Optimize] Creating Checkpoint");
            try
            {
                this.dbi.Checkpoint();
            }
            catch
            { }

            if (full)
            {
                LogWindow.Message("[Database] Optimize full");
                Dictionary<string, string> tempDict = new Dictionary<string, string>();
                var list = this.dbi.GetCollection<ModEntry>().FindAll().ToList();
                foreach (var t in list)
                {
                    if (t.Filename != null && t.Filename.Length != 0)
                    {
                        if (!File.Exists(Configuration.GetAbsoluteModPath(t.Filename)))
                            tempDict[t.Filename] = t.Filename;
                    }
                }
                DataGridInput li = new DataGridInput(tempDict, "Select to remove");
                var f = li.ShowDialog();
                if (f.Count() != 0)
                {
                    foreach (var t in f)
                    {
                        var item = list.Find(i=> i.Filename.Equals(t));
                        if (item != null)
                            this.dbi.GetCollection<ModEntry>().Delete(item.Id);
                    }
                }
                tempDict.Clear();
                list.Clear();
            }
            LogWindow.Message("[Database - Optimize] Rebuild database");
            this.dbi.Rebuild();
            LogWindow.Message("[Database] Optimize done");
        }

        public static bool DBFindData(ModEntry me, string name, string description, UInt32 typeFlag, UInt32 accModTypeFlag, string url)
        {
            if (me.ModName != null && name != null)
                if (!me.ModName.ToLower().Contains(name.ToLower()))
                    return false;

            if (me.Description !=null)
                if (description != "")
                    if (!me.Description.ToLower().Contains(description.ToLower()))
                        return false;
            
            if (me.Url != null)
                if (url != "")
                    if (!me.Url.ToLower().Contains(url.ToLower()))
                        return false;

            if (typeFlag != 0)
                if ((me.ModTypeFlag & typeFlag) == 0)
                    return false;

            if (accModTypeFlag != 0)
                if ((me.AccModTypeFlag & accModTypeFlag) == 0)
                    return false;

            return true;
        }

        public async Task<List<ModEntry>> FindModsAsync(string name, string description, UInt32 typeFlag, UInt32 accModTypeFlag, string url)
        {
            List<ModEntry> list = new List<ModEntry>();
            await Task.Run(() =>
            {
                foreach (var x in dbi.GetCollection<ModEntry>().FindAll())
                    if (DBFindData(x, name, description, typeFlag, accModTypeFlag, url))
                        list.Add(x);
            });
            return list;
        }

        public ModEntry FindData(string filename)
        {
            return dbi.GetCollection<ModEntry>().FindOne(n=> n.Filename.Equals(filename));
        }

        public async Task<List<ModEntry>> GetModListAsync()
        {
            List<ModEntry> list = new List<ModEntry>();
            await Task.Run(() =>
            {
                foreach (var x in dbi.GetCollection<ModEntry>().FindAll())
                    list.Add(x);
            });
            return list;
        }

        public ModEntry GetModByIdAsync(string Id)
        {
            foreach (var p in dbi.GetCollection<ModEntry>().Find(n => n.HashSha1 != null))
            {
                if (p.Id == Guid.Parse(Id))
                    return p;
            }
            return null;
        }

        public ModEntry GetModByUrl(string url)
        {
            foreach (var p in dbi.GetCollection<ModEntry>().Find(n => n.HashSha1 != null && n.Url != null))
            {
                if (p.Url.Equals(url))
                    return p;
            }
            return null;
        }

        public ModEntry DoesHashExists(byte[] hash)
        {
            foreach (var p in dbi.GetCollection<ModEntry>().Find(n => n.HashSha1 != null))
            {
                if (p.HashSha1.SequenceEqual(hash))
                    return p;
            }
            return null;
        }

        public void SaveData(ModEntry me)
        {
            var found = dbi.GetCollection<ModEntry>().FindOne(n => n.Id == me.Id);
            if (found != null)
                dbi.GetCollection<ModEntry>().Update(found.Id,me);
            else
            {
                me.Id = Guid.NewGuid();
                dbi.GetCollection<ModEntry>().Insert(me);
            }
        }

        public static void SaveMinimalData(string url, string modname, string discription, byte[] pictureBytes, string file, bool dtready)
        {
            if (!Database.Initialized)
                return;

            string relFile = Configuration.GetRelativeModPath(file);
            ModEntry mod = new ModEntry
            {
                Url = url,
                ModName = modname,
                Description = discription,
                picture = pictureBytes,
                Filename = relFile,
                IsForDT = dtready
            };

            SHA1Managed managed = new SHA1Managed();
            using (FileStream stream = File.OpenRead(file))
            {
                mod.HashSha1 = managed.ComputeHashAsync(stream).Result;
                mod.ModificationDate = File.GetLastWriteTime(file);
            }
            Database.Instance.SaveData(mod);
        }
    }
}

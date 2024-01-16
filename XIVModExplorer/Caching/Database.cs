/*
* Copyright(c) 2024 GiR-Zippo
* Licensed under the Mozilla Public License Version 2.0. See https://github.com/GiR-Zippo/XIV-Modexplorer/blob/main/LICENSE for full license information.
*/

using LiteDB;
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading.Tasks;
using XIVModExplorer.Scraping;

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
        private readonly LiteCollection<ModEntry> collection;
        private bool disposedValue;

        private Database(LiteDatabase dbi)
        {
            this.dbi = dbi;
            this.collection = (LiteCollection<ModEntry>)dbi.GetCollection<ModEntry>();
            this.disposedValue = false;
        }

        internal static Database CreateInstance(string dbPath)
        {
            var dbi = new LiteDatabase(@"filename=" + dbPath + "; journal = false");
            return new Database(dbi);
        }

        public void Optimize()
        {
            this.dbi.Checkpoint();
            this.dbi.Rebuild();
        }

        public static bool DBFindData(ModEntry me, string name, string description, UInt32 typeFlag, UInt32 accModTypeFlag, string url)
        {
            if (name != "")
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
                foreach (var x in collection.FindAll())
                    if (DBFindData(x, name, description, typeFlag, accModTypeFlag, url))
                        list.Add(x);
            });
            return list;
        }

        public ModEntry FindData(string filename)
        {
            return collection.FindOne(n=> n.Filename.Equals(filename));
        }

        public ModEntry DoesHashExists(byte[] hash)
        {
            foreach (var p in collection.Find(n => n.HashSha1 != null))
            {
                if (p.HashSha1.SequenceEqual(hash))
                    return p;
            }
            return null;
        }

        public void SaveData(ModEntry me)
        {
            var found = collection.FindOne(n => n.Id == me.Id);
            if (found != null)
                collection.Update(found.Id,me);
            else
            {
                me.Id = Guid.NewGuid();
                collection.Insert(me);
            }
        }
    }
}

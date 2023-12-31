/*
* Copyright(c) 2023 GiR-Zippo
* Licensed under the Mozilla Public License Version 2.0. See https://github.com/GiR-Zippo/XIV-Modexplorer/blob/main/LICENSE for full license information.
*/

using LiteDB;
using System;
using System.Linq;

namespace XIVModExplorer.Caching
{
    public enum Type
    {
        NONE    = 0b0000000000000000,
        WEAPON  = 0b0000000000000001,
        HEAD    = 0b0000000000000010,
        TOP     = 0b0000000000000100,
        HANDS   = 0b0000000000001000,
        BOTTOM  = 0b0000000000010000,
        SHOE    = 0b0000000000100000,
        EAR     = 0b0000000001000000,
        NECK    = 0b0000000010000000,
        ARM     = 0b0000000100000000,
        FINGER  = 0b0000001000000000,

        MINION  = 0b0000010000000000,
        MOUNT   = 0b0000100000000000,

        VFX     = 0b0001000000000000,
      ANIMATION = 0b0010000000000000,
        
        MISC    = 0b0100000000000000,

        ONACC   = 0b1000000000000000    //ists ein mod-accessoire
    }

    [Serializable]
    public class ModEntry
    {
        [BsonId]
        public Guid Id { get; set; }
        public string ModName { get; set; } = "";
        public UInt16 ModTypeFlag { get; set; } = 0;
        public UInt16 AccModTypeFlag { get; set; } = 0;
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
            var dbi = new LiteDatabase(dbPath);
            return new Database(dbi);
        }

        public void Optimize()
        {
            this.dbi.Checkpoint();
            this.dbi.Rebuild();
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

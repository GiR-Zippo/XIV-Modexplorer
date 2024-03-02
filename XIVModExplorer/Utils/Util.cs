/*
* Copyright(c) 2024 GiR-Zippo
* Licensed under the Mozilla Public License Version 2.0. See https://github.com/GiR-Zippo/XIV-Modexplorer/blob/main/LICENSE for full license information.
*/

using SharpCompress.Archives;
using SharpCompress.Common;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using XIVModExplorer.Caching;

namespace XIVModExplorer.Utils
{
    public class ConcurrentList<T> : IList<T>
    {
        private readonly IList<T> _list = new List<T>();
        private readonly object _syncRoot = new object();

        public T this[int index] { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public int Count
        {
            get
            {
                lock (_syncRoot)
                {
                    return _list.Count;
                }
            }
        }

        public bool IsReadOnly 
        {
            get
            {
                lock (_syncRoot)
                {
                    return _list.IsReadOnly;
                }
}
        }

        // Define all other IList<T> members here...
        public void Add(T item)
        {
            lock (_syncRoot)
            {
                _list.Add(item);
            }
        }

        public void Clear()
        {
            lock (_syncRoot)
            {
                _list.Clear();
            }
        }

        public bool Contains(T item)
        {
            lock (_syncRoot)
            {
                return _list.Contains(item);
            }
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        public IEnumerator<T> GetEnumerator()
        {
            throw new NotImplementedException();
        }

        public int IndexOf(T item)
        {
            throw new NotImplementedException();
        }

        public void Insert(int index, T item)
        {
            throw new NotImplementedException();
        }

        public bool Remove(T item)
        {
            lock (_syncRoot)
            {
                return _list.Remove(item);
            }
        }

        public void RemoveAt(int index)
        {
            throw new NotImplementedException();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            throw new NotImplementedException();
        }
        // Continue implementing the rest of the IList<T> interface...
    }

    public static class StringOperations
    {
        public static double CalculateSimilarity(string source, string target)
        {
            if ((source == null) || (target == null)) return 0.0;
            if ((source.Length == 0) || (target.Length == 0)) return 0.0;
            if (source == target) return 1.0;

            int stepsToSame = LevenshteinDistance(source, target);
            return (1.0 - ((double)stepsToSame / (double)Math.Max(source.Length, target.Length)));
        }

        public static int LevenshteinDistance(string source, string target)
        {
            // degenerate cases
            if (source == target) return 0;
            if (source.Length == 0) return target.Length;
            if (target.Length == 0) return source.Length;

            // create two work vectors of integer distances
            int[] v0 = new int[target.Length + 1];
            int[] v1 = new int[target.Length + 1];

            // initialize v0 (the previous row of distances)
            // this row is A[0][i]: edit distance for an empty s
            // the distance is just the number of characters to delete from t
            for (int i = 0; i < v0.Length; i++)
                v0[i] = i;

            for (int i = 0; i < source.Length; i++)
            {
                // calculate v1 (current row distances) from the previous row v0

                // first element of v1 is A[i+1][0]
                //   edit distance is delete (i+1) chars from s to match empty t
                v1[0] = i + 1;

                // use formula to fill in the rest of the row
                for (int j = 0; j < target.Length; j++)
                {
                    var cost = (source[i] == target[j]) ? 0 : 1;
                    v1[j + 1] = Math.Min(v1[j] + 1, Math.Min(v0[j + 1] + 1, v0[j] + cost));
                }

                // copy v1 (current row) to v0 (previous row) for next iteration
                for (int j = 0; j < v0.Length; j++)
                    v0[j] = v1[j];
            }

            return v1[target.Length];
        }
    }

    public static class Util
    {
        /// <summary>
        /// Compress a directory as a Zip-file
        /// </summary>
        public static void CompressToArchive(string current_Directory)
        {
            //archive the directory
            var dirName = new DirectoryInfo(current_Directory).Name;
            string result = new DirectoryInfo(current_Directory).Parent.FullName;
            var carchive = ArchiveFactory.Create(ArchiveType.Zip);
            carchive.AddAllFromDirectory(current_Directory);

            string g = result + "\\" + dirName + ".zip";
            carchive.SaveTo(g, CompressionType.Deflate);
            carchive.Dispose();
        }

        public static void CreateMetaEntry(string current_Directory, string url, string Modname, string Description)
        {
            var dirName = new DirectoryInfo(current_Directory).Name;
            string result = new DirectoryInfo(current_Directory).Parent.FullName;
            string g = result + "\\" + dirName + ".zip";
            byte[] pictureData = null;
            if (File.Exists(result + "\\" + dirName + "\\" + "preview-0.png"))
            {
                Image png = Image.FromFile(result + "\\" + dirName + "\\" + "preview-0.png");
                using (var fx = new MemoryStream())
                {
                    png.Save(fx, ImageFormat.Jpeg);
                    pictureData = fx.GetBuffer();
                }
            }
            var converter = new ReverseMarkdown.Converter();
            Database.SaveMinimalData(url, Modname, converter.Convert(Description), pictureData, g);
        }

        /// <summary>
        /// Search for a directory
        /// </summary>
        /// <returns>The directory name or nearest possible directory names</returns>
        public static List<string> SearchForDirectory(string root, string search)
        {
            List<string> foundDirs = new List<string>();
            if (Directory.Exists(root + "\\" + search))
                foundDirs.Add(search);
            else
            {
                Dictionary<string, string> entryList = new Dictionary<string, string>();
                foreach (var ent in Directory.GetDirectories(root).Select(x => x.Split('\\').Last())) //only get last path name
                {
                    var ret = StringOperations.CalculateSimilarity(search, ent);
                    if (ret > 0.1)
                        entryList.Add(ent, ret.ToString());
                }
                entryList = entryList.OrderByDescending(obj => obj.Value).ToDictionary(obj => obj.Key, obj => obj.Key);
                foreach (var ent in entryList)
                    foundDirs.Add(ent.Value);
            }
            return foundDirs;
        }

        public static string MakeValidFileName(string name)
        {
            string invalidChars = System.Text.RegularExpressions.Regex.Escape(new string(System.IO.Path.GetInvalidFileNameChars()));
            string invalidRegStr = string.Format(@"([{0}]*\.+$)|([{0}]+)", invalidChars);

            return System.Text.RegularExpressions.Regex.Replace(name, invalidRegStr, "_");
        }
    }
}

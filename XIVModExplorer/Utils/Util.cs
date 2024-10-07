/*
* Copyright(c) 2024 GiR-Zippo
* Licensed under the Mozilla Public License Version 2.0. See https://github.com/GiR-Zippo/XIV-Modexplorer/blob/main/LICENSE for full license information.
*/

using ReverseMarkdown.Converters;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using XIVModExplorer.Caching;
using XIVModExplorer.HelperWindows;

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
        /// Compute the SHA1 from a file
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        public static byte[] GetSHA1FromFile(string filename)
        {
            SHA1Managed managed = new SHA1Managed();
            using (FileStream stream = File.OpenRead(filename))
            {
                return managed.ComputeHashAsync(stream).Result;
            }
        }

        /// <summary>
        /// Compute the SHA1 from a stream
        /// </summary>
        /// <param name="sha1"></param>
        /// <param name="inputStream"></param>
        /// <returns></returns>
        public static async Task<byte[]> ComputeHashAsync(this SHA1 sha1, Stream inputStream)
        {
            LogWindow.Message("[Metadata-Extentions] Calculating hash");
            const int BufferSize = 4096;

            sha1.Initialize();

            var buffer = new byte[BufferSize];
            var streamLength = inputStream.Length;
            while (true)
            {
                var read = await inputStream.ReadAsync(buffer, 0, BufferSize).ConfigureAwait(false);
                if (inputStream.Position == streamLength)
                {
                    sha1.TransformFinalBlock(buffer, 0, read);
                    break;
                }
                sha1.TransformBlock(buffer, 0, read, default(byte[]), default(int));
            }
            LogWindow.Message("[Metadata-Extentions] Calculating hash done");
            return sha1.Hash;
        }

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

        /// <summary>
        /// Create a minimal database entry
        /// </summary>
        /// <param name="current_Directory"></param>
        /// <param name="url"></param>
        /// <param name="Modname"></param>
        /// <param name="Description"></param>
        /// <param name="modflag"></param>
        /// <param name="dtready"></param>
        public static void CreateMetaEntry(string current_Directory, string url, string Modname, string Description, UInt32 modflag = 0, bool dtready = false)
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
            Database.SaveMinimalData(url, Modname, converter.Convert(Description), pictureData, g, modflag, dtready);
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

        /// <summary>
        /// Remove invalid chars from a filename-string
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static string MakeValidFileName(string name)
        {
            string invalidChars = System.Text.RegularExpressions.Regex.Escape(new string(System.IO.Path.GetInvalidFileNameChars()));
            string invalidRegStr = string.Format(@"([{0}]*\.+$)|([{0}]+)", invalidChars);

            return System.Text.RegularExpressions.Regex.Replace(name, invalidRegStr, "_");
        }

        /// <summary>
        /// Upgrade mods in a directory to DT
        /// </summary>
        /// <param name="path"></param>
        public static void UpgradeDownloadModsToDT(string path)
        {
            var extensions = new List<string> { ".7z", ".zip", ".rar" };
            var files = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories)
                                .Where(f => extensions
                                .Any(extn => string.Compare(Path.GetExtension(f), extn, StringComparison.InvariantCultureIgnoreCase) == 0))
                                .ToArray();

            foreach (var file in files)
            {
                string pth = Path.GetDirectoryName(file) + "\\";
                string fname = Path.GetFileNameWithoutExtension(file);
                //create dir
                Directory.CreateDirectory(pth + fname);
                //ectract
                var archive = ArchiveFactory.Open(file).ExtractAllEntries();
                archive.WriteAllToDirectory(pth + fname, new ExtractionOptions() { Overwrite = true, ExtractFullPath=true });
                archive.Dispose();
                //delete the old archive
                File.Delete(file);
            }

            //get mods
            extensions = new List<string> { ".ttmp", ".ttmp2", ".pmb" };
            var innerfiles = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories)
                                .Where(f => extensions
                                .Any(extn => string.Compare(Path.GetExtension(f), extn, StringComparison.InvariantCultureIgnoreCase) == 0))
                                .ToArray();
            
            foreach (string modfile in innerfiles)
                File.Delete(UpgradeModToDT(modfile)); //delete the old ones
        }

        /// <summary>
        /// upgrade mod to dt ([string] file with path)
        /// </summary>
        /// <param name="file"></param>
        public static string UpgradeModToDT(string file)
        {
            string pth = Path.GetDirectoryName(file) + "\\";
            string fname = Path.GetFileNameWithoutExtension(file);
            string extention = Path.GetExtension(file);
            File.Move(pth + fname + extention, pth + fname + "-EW" + extention);
            Process p = new Process()
            {
                StartInfo =
                {
                    CreateNoWindow = true,
                    WorkingDirectory = Configuration.GetValue("TextToolsPath")+"\\",
                    FileName = Configuration.GetValue("TextToolsPath") + "\\ConsoleTools.exe",
                    Arguments = @"/upgrade " +
                                "\"" + pth + fname + "-EW" + extention + "\" " +
                                "\"" + pth + fname +  extention + "\""
                }
            };
            p.EnableRaisingEvents = true;
            // redirect the output
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.RedirectStandardError = true;

            // hookup the eventhandlers to capture the data that is received
            p.OutputDataReceived += (sender, args) => LogWindow.Message(args.Data);
            p.ErrorDataReceived += (sender, args) => LogWindow.Message(args.Data);

            // direct start
            p.StartInfo.UseShellExecute = false;

            p.Start();

            // start our event pumps
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();

            // until we are done
            p.WaitForExit();
            return pth + fname + "-EW" + extention;
        }
    }
}

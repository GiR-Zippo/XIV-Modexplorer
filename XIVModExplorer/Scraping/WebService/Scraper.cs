/*
* Copyright(c) 2024 GiR-Zippo
* Licensed under the Mozilla Public License Version 2.0. See https://github.com/GiR-Zippo/XIV-Modexplorer/blob/main/LICENSE for full license information.
*/

using ImageMagick;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using XIVModExplorer.Caching;
using XIVModExplorer.HelperWindows;
using XIVModExplorer.Scraping.Internal;
using XIVModExplorer.Utils;

namespace XIVModExplorer.Scraping
{
    public class CollectedData : IDisposable
    {
        public List<string> Images = new List<string>();
        public string Modname { get; set; } = "";
        public string Description { get; set; } = "";
        public List<string> DownloadUrl { get; set; } = new List<string>();
        public List<string> Replaces { get; set; } = new List<string>();
        public string ExternalSite { get; set; } = ""; //in case the mod refs to patreon, coomer...
        
        public void Dispose()
        {
            Images.Clear();
            DownloadUrl.Clear();
        }
    }

    public partial class Scraper : IDisposable
    {
        /// <summary>
        /// Is our data ready
        /// </summary>
        private ConcurrentList<string> DataReady { get; set; } = new ConcurrentList<string>();

        /// <summary>
        /// the collection
        /// </summary>
        private CollectedData collectedData { get; set; } = null;

        private string current_downloadPath { get; set; } = "";
        public int Retry { get; set; } = 3;

        public Scraper()
        {
            WebService.Instance.OnRequestFinished += Instance_RequestFinished;
        }

        public void Dispose()
        {
            WebService.Instance.OnRequestFinished -= Instance_RequestFinished;
        }

        public async Task<bool> DownloadMod(string url, string path, bool archive = false, bool deldir = false, bool dtupgrade = false)
        {
            current_downloadPath = "";
            ScanURLforData(url);

            LogWindow.Message("[Scraper - DownloadMod] Waiting for scraped data");
            //wait until our data is ready
            while (!DataReady.Contains(url))
                await Task.Delay(200);
            DataReady.Remove(url);

            //no data no download
            if (collectedData == null)
            {
                MessageWindow.Show("No collectable data found.");
                return false;
            }

            if (collectedData.ExternalSite != "")
            {
                LogWindow.Message("[Scraper - DownloadMod] It's an external site, let's get the data there");
                return await DownloadMod(collectedData.ExternalSite, path, archive, deldir, dtupgrade);
            }
            //no download url, no download
            if (collectedData.DownloadUrl.Count() == 0)
            {
                MessageWindow.Show("Can't download mod from:\r\n" + url, "Error");
                return false;
            }

            //Check if it's a filehoster and try download
            LogWindow.Message("[Scraper - DownloadMod] Download binary data...");
            if (!Helper.IsSameDomain(url, collectedData.DownloadUrl[0]))
                /*if (_downloadUrl.Contains("drive.google"))
                    newPath = downloadGoogleDrive(_downloadUrl[0], path);
                else*/ if (collectedData.DownloadUrl[0].Contains("mega.nz"))
                    await downloadMega(collectedData.DownloadUrl[0], path);
                else if (collectedData.DownloadUrl[0].StartsWith("https://gofile.io"))
                    downloadGoFileIo(collectedData.DownloadUrl[0], path);
                else if (collectedData.DownloadUrl[0].StartsWith("https://pixeldrain.com"))
                    downloadPixeldrain(collectedData.DownloadUrl[0], path);
                else
                    saveData(collectedData.DownloadUrl[0], path);
            else
                saveData(collectedData.DownloadUrl[0], path);

            //wait until our data is ready
            while (DataReady.Count() == 0)
                await Task.Delay(200);
            DataReady.Clear();
            LogWindow.Message("[Scraper - DownloadMod] Download binary data done");

            if (current_downloadPath == "")
            {
                MessageWindow.Show("Download-path error.");
                return false;
            }
            //Remove the first element
            collectedData.DownloadUrl.RemoveAt(0);

            //save more downloadable files (wenn noch welche da sind)
            Parallel.ForEach(collectedData.DownloadUrl, new ParallelOptions { MaxDegreeOfParallelism = 2 }, (target, state) =>
            {
                if (!Helper.IsSameDomain(url, target))
                    /*if (target.Contains("drive.google"))
                        downloadGoogleDrive(target, newPath, false);
                    else*/ if (target.Contains("mega.nz"))
                        downloadMega(target, current_downloadPath, false);
                    else
                        saveData(target, current_downloadPath, false);
                else
                    saveData(target, current_downloadPath, false);
            });

            //save the description file
            saveText(collectedData.Description, current_downloadPath + "\\description.md");

            //Save the images
            int index = 0;
            foreach (var imageUrl in collectedData.Images)
            {
                WebService.Instance.AddToQueue(new WebService.GetRequest()
                {
                    Url = imageUrl,
                    Requester = WebService.Requester.IMAGE,
                    Parameters = current_downloadPath + "\\" + "preview-" + index.ToString() + ".png"
                });
                index++;
            }

            LogWindow.Message("[Scraper - DownloadMod] Download everything else...");
            //wait until all data is ready
            while (DataReady.Count() != collectedData.Images.Count() + collectedData.DownloadUrl.Count())
                await Task.Delay(500);
            DataReady.Clear();
            LogWindow.Message("[Scraper - DownloadMod] Download everything else done");

            if (current_downloadPath == "")
                return true;

            if (dtupgrade)
            {
                LogWindow.Message("[Scraper - DownloadMod] Do the DT Upgrade");
                Util.UpgradeDownloadModsToDT(current_downloadPath);
            }

            if (!archive)
                return true;

            await Task.Run(() =>
            {
                LogWindow.Message("[Scraper - DownloadMod] Compressing to archive");
                //archive the directory
                Util.CompressToArchive(current_downloadPath);

                //cache min data, if db is enabled
                if (Configuration.GetBoolValue("UseDatabase"))
                {
                    //get the modtype if we have any
                    UInt32 modflag = 0;
                    foreach (string item in collectedData.Replaces)
                        modflag |= (UInt32)ItemLookup.GetItem(item);
                    Util.CreateMetaEntry(current_downloadPath, url, collectedData.Modname, collectedData.Description, modflag, true);
                }
                //delete the dir
                if (deldir)
                {
                    try
                    {
                        Directory.Delete(current_downloadPath, true);
                    }
                    catch (IOException e)
                    {
                        Console.WriteLine("Expception: {0}", e.Message);
                        TrashRemover.RemoveDirectoryList.Enqueue(current_downloadPath);
                        LogWindow.Message($"[Scraper - DownloadMod] Can't delete {current_downloadPath}, deleting later.");
                    }
                }
                LogWindow.Message("[Scraper - DownloadMod] Compressing to archive done");
            });

            return true;
        }

        public async Task<bool> ScrapeURLforData(string url, string path, bool archive = false, bool deldir = false)
        {
            Retry = 4;
            ScanURLforData(url);

            while (!DataReady.Contains(url))
                await Task.Delay(200);
            DataReady.Remove(url);

            if (collectedData == null)
                return false;

            saveText(collectedData.Description, path + "\\description.md");

            int index = 0;
            foreach (var imageUrl in collectedData.Images)
            {
                WebService.Instance.AddToQueue(new WebService.GetRequest()
                {
                    Url = imageUrl,
                    Requester = WebService.Requester.IMAGE,
                    Parameters = path + "\\" + "preview-" + index.ToString() + ".png"
                });
                index++;
            }

            //wait until all data is ready
            while (DataReady.Count() != collectedData.Images.Count())
                await Task.Delay(500);

            DataReady.Clear();

            if (!archive)
                return true;

            await Task.Run(() =>
            {
                //archive the directory
                Util.CompressToArchive(path);

                //cache min data, if db is enabled
                if (Configuration.GetBoolValue("UseDatabase"))
                    Util.CreateMetaEntry(path, url, collectedData.Modname, collectedData.Description);

                //delete the dir
                if (deldir)
                {
                    try
                    {
                        Directory.Delete(path, true);
                    }
                    catch (IOException e)
                    {
                        Console.WriteLine("Expception: {0}", e.Message);
                        TrashRemover.RemoveDirectoryList.Enqueue(path);
                    }
                }
            });
            return true;
        }

        private void ScanURLforData(string url)
        {
            collectedData = null;
            WebService.Instance.AddToQueue(new WebService.GetRequest()
            {
                Url = url,
                Requester = WebService.Requester.HTML
            });
        }

        /// <summary>
        /// save the description
        /// </summary>
        /// <param name="data"></param>
        /// <param name="filename"></param>
        private static void saveText(string data, string filename)
        {
            using (StreamWriter writer = new StreamWriter(filename))
            {
                var converter = new ReverseMarkdown.Converter();
                writer.Write(converter.Convert(data));
            }
        }

        #region Requester
        /// <summary>
        /// Save the data
        /// </summary>
        private void saveData(string downloadUrl, string path, bool createDir = true, bool touch_dlpath = true)
        {
            string finalUrl = Helper.GetFinalRedirect(downloadUrl);
            if (!Helper.IsSameDomain(finalUrl, downloadUrl))
                downloadUrl = finalUrl;

            WebService.Instance.AddToQueue(new WebService.GetRequest()
            {
                Url = downloadUrl,
                Requester = WebService.Requester.DOWNLOAD,
                Parameters = new KeyValuePair<string, KeyValuePair<bool, bool>>(path, new KeyValuePair<bool, bool>(createDir, touch_dlpath))
            });
        }

        private void downloadPixeldrain(string downloadUrl, string path, bool createDir = true, bool touch_dlpath = true)
        {
            var temp = InternalScraper.GetPixelDrainDownload(downloadUrl);
            foreach (var url in temp.Key)
            {
                WebService.Instance.AddToQueue(new WebService.GetRequest()
                {
                    Url = url,
                    Requester = WebService.Requester.DOWNLOAD,
                    CookieJar = temp.Value,
                    Parameters = new KeyValuePair<string, KeyValuePair<bool, bool>>(path, new KeyValuePair<bool, bool>(createDir, touch_dlpath))
                });
            }
        }

        private void downloadGoFileIo(string downloadUrl, string path, bool createDir = true, bool touch_dlpath = true)
        {
            var temp = InternalScraper.GetGOFileDownloads(downloadUrl);
            foreach (var url in temp.Key)
            {
                WebService.Instance.AddToQueue(new WebService.GetRequest()
                {
                    Url = url,
                    Requester = WebService.Requester.DOWNLOAD,
                    CookieJar = temp.Value,
                    Parameters = new KeyValuePair<string, KeyValuePair<bool, bool>>(path, new KeyValuePair<bool, bool>(createDir, touch_dlpath))
                });
            }
        }
        #endregion

        private void Instance_RequestFinished(object sender, object e)
        {
            if (e is WebService.GetRequest)
            {
                var f = e as WebService.GetRequest;
                if (f.Requester == WebService.Requester.HTML)
                    ScanURLforData(f);
                if (f.Requester == WebService.Requester.IMAGE)
                    saveImage(f);
                if (f.Requester == WebService.Requester.DOWNLOAD)
                    saveData(f);
            }
        }

        #region ResponseHandler
        private void ScanURLforData(WebService.GetRequest request)
        {
            if (request.ResponseCode == HttpStatusCode.ServiceUnavailable && Retry != 0)
            {
                Retry--;
                Thread.Sleep(2000);
                ScanURLforData(request.Url);
                return;
            }

            if (request.ResponseCode != HttpStatusCode.OK)
            {
                MessageWindow.Show("Server is telling us: \r\n" + request.ResponseMsg + "\r\nPlz try again or give up.", "Http Error");
                DataReady.Add(request.Url);
                return;
            }

            var html = request.ResponseBody.ReadAsStringAsync().Result;
            var lines = html.Split("\r\n".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            LogWindow.Message($"[Scraper] Searching for data at {request.Host}");
            if (request.Host.Contains("patreon.com"))
                collectedData = InternalScraper.ReadPatreon(html);
            else if (request.Host.Contains("ko-fi.com"))
                collectedData = InternalScraper.ReadKofi(lines);
            else
            {
                string strExeFilePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                string strWorkPath = Path.GetDirectoryName(strExeFilePath);
                var myFiles = Directory.EnumerateFiles(strWorkPath + "\\ScraperLua", "*.*", SearchOption.AllDirectories);
                var x = myFiles.Where(n => n.EndsWith(request.Host.Replace("www.", "") + ".lua")).FirstOrDefault();
                if (x == null)
                {
                    request.Dispose();
                    DataReady.Add(request.Url);
                    return;
                }

                LuaScraper l = new LuaScraper();
                if (!l.Execute(x, html))
                {
                    request.Dispose();
                    DataReady.Add(request.Url);
                    return;
                }

                //Set the collected data
                collectedData = new CollectedData();
                collectedData.Modname = l.ModName;
                collectedData.Images = new List<string>(l.Pictures);
                collectedData.Description = l.Content;
                collectedData.DownloadUrl = new List<string>(l.DownloadLink);
                collectedData.Replaces = new List<string>(l.Replaces);
                collectedData.ExternalSite = l.ExternalSite;
                l.Dispose();
            }
            request.Dispose();
            DataReady.Add(request.Url);
            LogWindow.Message($"[Scraper] Searching for data at {request.Host} done");
        }

        /// <summary>
        /// get the image, convert it and save it as png
        /// </summary>
        /// <param name="imageUrl"></param>
        /// <param name="filename"></param>
        private Task saveImage(WebService.GetRequest request)
        {
            try
            {
                using (var image = new MagickImage(request.ResponseBody.ReadAsStreamAsync().Result))
                {
                    using (var outputStream = new MemoryStream())
                    {
                        image.Format = MagickFormat.Png;
                        image.Quality = 80;
                        image.Write(outputStream);
                        using (var yourImage = Image.FromStream(outputStream))
                        {
                            yourImage.Save((string)request.Parameters, ImageFormat.Png);
                            yourImage.Dispose();
                            LogWindow.Message($"[Scraper] Download picture {(string)request.Parameters} done");
                        }
                    }
                }
            }
            catch
            {
                LogWindow.Message($"[Scraper] Error getting/converting picture");
            }
            request.Dispose();
            DataReady.Add(request.Url);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Downloads a file and creates the folder 
        /// </summary>
        /// <param name="downloadUrl"></param>
        /// <param name="path"></param>
        /// <returns></returns>
        private Task saveData(WebService.GetRequest request)
        {
            LogWindow.Message($"[Scraper] Got binary data");
            string downloadUrl = request.Url;
            var Param = (KeyValuePair<string, KeyValuePair<bool, bool>>)request.Parameters;
            bool create_newdir = Param.Value.Key;
            bool untouch_dlpath = Param.Value.Value;

            string fileName = "";
            if (request.ResponseBody.Headers.TryGetValues("Content-Disposition", out IEnumerable<string> contentDisp))
                fileName = contentDisp.First().Substring(contentDisp.First().IndexOf("filename=") + 9).Replace("\"", "").Split(';')[0];
            else
            {
                //set filename by modname if no dl fn defined
                if (collectedData.Modname != "")
                    fileName = collectedData.Modname + "." + downloadUrl.Split('/').Last().Split('.').Last();
                else
                    fileName = downloadUrl.Split('/').Last();
            }

            fileName = Helper.ReplaceInvalidChars(Uri.UnescapeDataString(fileName)); //we are dealing with html, let's unescape

            byte[] data;
            using (MemoryStream ms = new MemoryStream())
            {
                request.ResponseBody.ReadAsStreamAsync().Result.CopyTo(ms);
                data = ms.ToArray();
                request.ResponseBody.Dispose();
                request.Dispose();
            }

            //if we are dealing with plain html, just cancel
            if (Helper.ArrayStartsWith(data, Encoding.ASCII.GetBytes("<!DOCTYPE html>")))
            {
                if (untouch_dlpath)
                    current_downloadPath = "";

                DataReady.Add(request.Url);
                return Task.CompletedTask;
            }
            string extension = Path.GetExtension(fileName);
            //check the ext and complete them
            if (extension == "")
            {
                if (data[0] == 55 && data[1] == 122)
                    extension = ".7z";

                fileName += extension;
            }

            string result = "";
            //create a new direcory
            if (create_newdir)
            {
                result = fileName.Substring(0, fileName.Length - extension.Length).TrimEnd() + "\\";
                if (!Directory.Exists(Param.Key + "\\" + fileName.Substring(0, fileName.Length - extension.Length)))
                    Directory.CreateDirectory(Param.Key + "\\" + fileName.Substring(0, fileName.Length - extension.Length));
            }

            LogWindow.Message($"[Scraper] Saving binary data");
            BinaryWriter binWriter = new BinaryWriter(File.Open(Param.Key + "\\" + result + fileName, FileMode.Create));
            binWriter.Write(data);
            binWriter.Close();
            binWriter.Dispose();
            LogWindow.Message($"[Scraper] Saving binary data done");
            //var res = request.ResponseBody.ReadAsStreamAsync().Result;
            //res.CopyTo(File.Open(Param.Key + "\\" + result + fileName, FileMode.Create));
            //res.Close();

            if (untouch_dlpath)
                current_downloadPath = Param.Key + "\\" + result;

            DataReady.Add(request.Url);
            return Task.CompletedTask;
        }
        #endregion
    }
}

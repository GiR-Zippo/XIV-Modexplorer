/*
* Copyright(c) 2023 GiR-Zippo
* Licensed under the Mozilla Public License Version 2.0. See https://github.com/GiR-Zippo/XIV-Modexplorer/blob/main/LICENSE for full license information.
*/

using System;
using System.Drawing.Imaging;
using System.Drawing;
using System.IO;
using System.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using SharpCompress.Archives;
using SharpCompress.Common;
using ImageMagick;
using System.Windows;
using System.Linq;
using System.Threading.Tasks;
using System.Text;

namespace XIVModExplorer.Scraping
{
    public static class URL_ImgFind
    {
        private static List<string> _images = new List<string>();
        private static string _modname { get; set; } = "";
        private static string _description { get; set; } = "";
        private static List<string> _downloadUrl { get; set; } = new List<string>();
        private static string _externalSite { get; set; } = ""; //in case the mod refs to patreon, coomer...

        public static bool DownloadMod(string url, string path, bool archive = false, bool deldir = false)
        {
            _images.Clear();
            _modname = ""; //used when no filename is avail
            _description = "";
            _downloadUrl.Clear();
            _externalSite = "";
            string newPath = "";

            //scan the url for data
            if (!ScanURLforData(url, path))
                return false;

            //no download url, no download
            if (_downloadUrl.Count() == 0)
            {
                MessageBox.Show("Can't download mod from:\r\n" + url, "Error");
                return false;
            }
            //an external site, let's get the data there
            if (_externalSite != "")
                return DownloadMod(url, path, archive, deldir);

            //Check if it's a filehoster and try download
            if (!isSameDomain(url, _downloadUrl[0]))
                if (_downloadUrl.Contains("drive.google"))
                    newPath = downloadGoogleDrive(_downloadUrl[0], path);
                else if (_downloadUrl.Contains("mega.nz"))
                    newPath = downloadMega(_downloadUrl[0], path);
                else
                    newPath = saveData(_downloadUrl[0], path);
            else
                newPath = saveData(_downloadUrl[0], path);

            if (newPath == "")
                return false;

            //Remove the first element
            _downloadUrl.RemoveAt(0);

            //save more downloadable files (wenn noch welche da sind)
            Parallel.ForEach(_downloadUrl, new ParallelOptions { MaxDegreeOfParallelism = 2 }, (target, state, index) =>
            {
                if (!isSameDomain(url, target))
                    if (_downloadUrl.Contains("drive.google"))
                        downloadGoogleDrive(target, newPath, false);
                    else if (_downloadUrl.Contains("mega.nz"))
                        downloadMega(target, newPath, false);
                    else
                        saveData(target, newPath, false);
                else
                    saveData(target, newPath, false);
            });

            //Save the images
            Parallel.ForEach(_images, new ParallelOptions { MaxDegreeOfParallelism = 2 }, (target, state, index) =>
            {
                saveImage(target, newPath + "\\" + "preview-" + index.ToString() + ".png");
            });

            //save the description file
            saveText(_description, newPath + "\\description.md");

            if (!archive)
                return true;

            //archive the directory
            var dirName = new DirectoryInfo(newPath).Name;
            string result = new DirectoryInfo(newPath).Parent.FullName;
            var carchive = ArchiveFactory.Create(ArchiveType.Zip);
            carchive.AddAllFromDirectory(newPath);

            string g = result + "\\" + dirName + ".zip";
            carchive.SaveTo(g, CompressionType.Deflate);
            carchive.Dispose();

            if (!deldir)
                return true;

            //delete the dir
            try
            {
                Directory.Delete(newPath, true);
            }
            catch (IOException)
            {
            }
            return true;
        }

        public static bool ScrapeURLforData(string url, string path)
        {
            _images.Clear();
            _description = "";
            _downloadUrl.Clear();
            _externalSite = "";
            if (!ScanURLforData(url, path))
                return false;

            Parallel.ForEach(_images, new ParallelOptions { MaxDegreeOfParallelism = 2 }, (target, state, index) =>
            {
                saveImage(target, path + "\\" + "preview-" + index.ToString() + ".png");
            });
            saveText(_description, path + "\\description.md");

            return true;
        }

        private static bool ScanURLforData(string url, string path)
        {
            string urlAddress = url;
            string hostname = "";
            HttpWebResponse response;
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(urlAddress);
                request.UserAgent = @"Mozilla/5.0 (Windows NT 10.0; Win64; x64)";
                //AppleWebKit/537.36 (KHTML, like Gecko) Chrome/51.0.2704.106 Safari/537.36";
                request.Accept = "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8";
                request.Proxy.Credentials = System.Net.CredentialCache.DefaultNetworkCredentials;
                request.AllowAutoRedirect = true;
                request.MaximumAutomaticRedirections = 2;
                hostname = request.RequestUri.DnsSafeHost.ToLower();
                response = (HttpWebResponse)request.GetResponse();
            }
            catch (System.Net.WebException e)
            {
                int StatusCode = (int)((HttpWebResponse)e.Response).StatusCode;
                if (StatusCode == 302 || (int)StatusCode == 308)
                {
                    return true;
                }
                MessageBox.Show("Server is telling us: \r\n" + ((HttpWebResponse)e.Response).StatusCode.ToString() + "\r\nPlz try again or give up.", "Http Error");
                return false;
            }
            StreamReader sr = new StreamReader(response.GetResponseStream());
            var html = sr.ReadToEnd();
            var lines = html.Split("\r\n".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            sr.Close();

            if (url.Contains("xivmodarchive.com"))
                readXIVArchive(lines);
            else if (url.Contains("patreon.com"))
                readPatreon(html);
            else if (url.Contains("ko-fi.com"))
                readKofi(lines);
            else if (url.Contains("beta.aetherlink.app"))
                readAetherlink(html);
            else
            {
                string host = hostname.Replace("www.", "");
                string strExeFilePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                string strWorkPath = Path.GetDirectoryName(strExeFilePath);
                var myFiles = Directory.EnumerateFiles(strWorkPath + "\\ScraperLua", "*.*", SearchOption.AllDirectories);
                var x = myFiles.Where(n => n.EndsWith(host + ".lua")).FirstOrDefault();
                if (x == null)
                    return false;

                LuaScraper l = new LuaScraper();
                if (!l.Execute(x, html))
                    return false;
                _modname = l.ModName;
                _images = l.Pictures;
                _description = l.Content;
                _downloadUrl = l.DownloadLink;
            }
            return true;
        }

        #region DataCollectors
        /// <summary>
        /// read from Patreon
        /// </summary>
        /// <param name="html"></param>
        private static void readPatreon(string html)
        {
            string[] term = { "<script id=\"__NEXT_DATA__\" type=\"application/json\">" };
            var xx = html.Split(term, StringSplitOptions.RemoveEmptyEntries)[1].Replace("</script></body></html>", "");
            TextReader dr = new StringReader(xx);
            using (JsonTextReader reader = new JsonTextReader(dr))
            {
                JObject o2 = (JObject)JToken.ReadFrom(reader);
                int ix = 0;
                var abde = o2["props"]["pageProps"]["bootstrapEnvelope"]["bootstrap"]["post"]["data"]["attributes"]["content"].Value<string>();
                _description = abde;

                foreach (var d in o2["props"]["pageProps"]["bootstrapEnvelope"]["bootstrap"]["post"]["included"])
                {
                    var p = o2["props"]["pageProps"]["bootstrapEnvelope"]["bootstrap"]["post"]["included"][ix];
                    if (p["type"].Value<string>().ToString() == "attachment")
                        _downloadUrl.Add(p["attributes"]["url"].Value<string>());
                    if (p["type"].Value<string>().ToString() == "media")
                        _images.Add(p["attributes"]["image_urls"]["original"].Value<string>());
                    ix++;
                }
            }
            dr.Close();
            return;
        }

        /// <summary>
        /// read from XIVMOD
        /// </summary>
        /// <param name="lines"></param>
        /// <param name="path"></param>
        private static void readXIVArchive(string[] lines)
        {
            //get the text
            foreach (var line in lines)
            {
                if (line.Contains("mod-carousel-image"))
                {
                    var dline = line.Split('\"')[3];
                    dline = dline.Split('\"')[0];
                    _images.Add(dline);
                }
            }

            foreach (var line in lines)
            {
                if (line.Contains(": [ via <a href=\"") && line.Contains(">Direct Download</a> ]") && !line.Contains("</li>"))
                {
                    string patt = ": [ via <a href=\"";
                    string url = ("https://www.xivmodarchive.com" + line.Substring(line.IndexOf(patt) + patt.Length).Split('\"')[0]);
                    url = normalizeUrl(url);
                    _downloadUrl.Add(url);
                }
                else if (line.Contains(": [ via <a href=\"") && line.Contains(">patreon.com</a> ]") && !line.Contains("</li>"))
                {
                    string patt = ": [ via <a href=\"";
                    string url = line.Substring(line.IndexOf(patt) + patt.Length).Split('\"')[0];
                    url = normalizeUrl(url);
                    _externalSite = url;
                }
                else if (line.Contains(": [ via <a href=\"") && line.Contains("drive.google.com</a> ]") && !line.Contains("</li>"))
                {
                    string patt = ": [ via <a href=\"";
                    string url = line.Substring(line.IndexOf(patt) + patt.Length).Split('\"')[0];
                    url = normalizeUrl(url);
                    _downloadUrl.Add(url);
                }
                else if (line.Contains(": [ via <a href=\"") && line.Contains(">mega.nz</a> ]") && !line.Contains("</li>"))
                {
                    string patt = ": [ via <a href=\"";
                    string url = line.Substring(line.IndexOf(patt) + patt.Length).Split('\"')[0];
                    url = normalizeUrl(url);
                    _downloadUrl.Add(url);
                }
            }

            int i = 0;
            //get the Text
            foreach (var line in lines)
            {
                if (line.Contains("</div>") && i == -2)
                    i = 0;
                if (i == -2)
                    _description = line;
                if (line.Contains("<div class=\"px-2\">") && i == -1)
                    i = -2;
                if (line.Contains("<p class=\"lead\">Author's Comments:</p>"))
                    i = -1;
            }
        }

        /// <summary>
        /// read from KoFi, !no download support!
        /// </summary>
        /// <param name="lines"></param>
        private static void readKofi(string[] lines)
        {
            //get the text
            string text = "";
            bool content = false;
            foreach (var line in lines)
            {
                if (line.Contains("<p class=\"line-breaks kfds-c-word-wrap\" v-pre>"))
                {
                    content = true;
                }
                if (content)
                    text += line + "<p>";
                if (content && line.Contains("</p>"))
                    content = false;
            }
            _description = text;

            //get the images
            foreach (var line in lines)
            {
                if (line.Contains("<img class=\"kfds-c-carousel-product-img"))
                {
                    string x = line.Split(new string[] { "<img class=\"kfds-c-carousel-product-img disable-dbl-tap-zoom\" src=\"" }, StringSplitOptions.RemoveEmptyEntries)[1]
                                   .Split('\"')[0];
                    _images.Add(x);
                }
            }
        }

        /// <summary>
        /// read from aetherlink
        /// </summary>
        /// <param name="lines"></param>
        private static void readAetherlink(string html)
        {
            string[] term = { "<script id=\"__NEXT_DATA__\" type=\"application/json\">" };
            var xx = html.Split(term, StringSplitOptions.RemoveEmptyEntries)[1].Replace("</script></body></html>", "");
            TextReader dr = new StringReader(xx);
            using (JsonTextReader reader = new JsonTextReader(dr))
            {
                JObject o2 = (JObject)JToken.ReadFrom(reader);
                _modname = o2["props"]["pageProps"]["mod"]["meta"]["name"]["short"].Value<string>();
                _description = o2["props"]["pageProps"]["mod"]["meta"]["description"]["html"].Value<string>();

                foreach (var d in o2["props"]["pageProps"]["downloads"])
                    _downloadUrl.Add(d["url"].Value<string>());
                foreach (var d in o2["props"]["pageProps"]["slides"])
                    _images.Add(d["url"].Value<string>());
            }
            dr.Close();
        }
        #endregion

        #region data saver
        /// <summary>
        /// Downloads a file and creates the folder 
        /// </summary>
        /// <param name="downloadUrl"></param>
        /// <param name="path"></param>
        /// <returns></returns>
        private static string saveData(string downloadUrl, string path, bool createDir = true)
        {
            string finalUrl = GetFinalRedirect(downloadUrl);
            if (!isSameDomain(finalUrl, downloadUrl))
                downloadUrl = finalUrl;

            WebClient wc = new WebClient();
            var data = wc.DownloadData(downloadUrl);
            string fileName = "";

            if (!String.IsNullOrEmpty(wc.ResponseHeaders["Content-Disposition"]))
                fileName = wc.ResponseHeaders["Content-Disposition"].Substring(wc.ResponseHeaders["Content-Disposition"].IndexOf("filename=") + 9).Replace("\"", "").Split(';')[0];
            else
            {
                //set filename by modname if no dl fn defined
                if (_modname != "")
                    fileName = _modname+"."+downloadUrl.Split('/').Last().Split('.').Last();
                else
                    fileName = downloadUrl.Split('/').Last();
            }
               
            fileName = ReplaceInvalidChars(Uri.UnescapeDataString(fileName)); //we are dealing with html, let's unescape

            //if we are dealing with plain html, just cancel
            if (ArrayStartsWith(data, Encoding.ASCII.GetBytes("<!DOCTYPE html>")))
                return "";

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
            if (createDir)
            {
                result = fileName.Substring(0, fileName.Length - extension.Length) + "\\";
                if (!Directory.Exists(path + "\\" + fileName.Substring(0, fileName.Length - extension.Length)))
                    Directory.CreateDirectory(path + "\\" + fileName.Substring(0, fileName.Length - extension.Length));
            }

            BinaryWriter binWriter = new BinaryWriter(File.Open(path + "\\" + result + fileName, FileMode.Create));
            binWriter.Write(data);
            binWriter.Close();
            binWriter.Dispose();
            wc.Dispose();
            return path + "\\" + result;
        }

        /// <summary>
        /// Downloads from GD, only file is supported now
        /// </summary>
        /// <param name="url"></param>
        /// <param name="path"></param>
        /// <returns></returns>
        private static string downloadGoogleDrive(string url, string path, bool createDir = true)
        {
            if (!url.Contains("file/d/"))
            {
                Debug.WriteLine("it's afolder");
                return "";
            }
            string urlAddress = "https://drive.google.com/uc?id=" +
                               url.Split(new string[] { "file/d/" }, StringSplitOptions.RemoveEmptyEntries)[1]
                                  .Split(new string[] { "/view" }, StringSplitOptions.RemoveEmptyEntries)[0] +
                                "&export=download";
            HttpWebResponse response;
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(urlAddress);
                request.UserAgent = @"Mozilla/5.0 (Windows NT 10.0; Win64; x64)";
                //AppleWebKit/537.36 (KHTML, like Gecko) Chrome/51.0.2704.106 Safari/537.36";
                request.Accept = "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8";
                request.Proxy.Credentials = System.Net.CredentialCache.DefaultCredentials;
                response = (HttpWebResponse)request.GetResponse();
            }
            catch (WebException)
            {
                return "";
            }

            //Debug.WriteLine(response.Headers["Content-Type"]);
            if (response.Headers["Content-Type"].Contains("text/html;"))
            {
                StreamReader sr = new StreamReader(response.GetResponseStream());
                var html = sr.ReadToEnd();
                var fileName = ReplaceInvalidChars(html.Split(new string[] { "uc-name-size" }, StringSplitOptions.RemoveEmptyEntries)[4].Split('>')[2].Split('<')[0]);
                string extension = Path.GetExtension(fileName);

                string folderName = "";
                //create a new direcory
                if (createDir)
                {
                    folderName = fileName.Substring(0, fileName.Length - extension.Length) + "\\";
                    if (!Directory.Exists(path + "\\" + fileName.Substring(0, fileName.Length - extension.Length)))
                        Directory.CreateDirectory(path + "\\" + fileName.Substring(0, fileName.Length - extension.Length));
                }
                sr.Close();
                sr.DiscardBufferedData();
                sr.Dispose();
                DriveDownloader fileDownloader = new DriveDownloader();
                fileDownloader.DownloadFile(urlAddress, path + "\\" + fileName);
                return path + "\\" + folderName;
            }
            //If this is a already a binary, save the stream
            else if (response.Headers["Content-Type"].Contains("application/x-7z-compressed"))
            {
                string fileName = response.Headers["Content-Disposition"].Substring(response.Headers["Content-Disposition"].IndexOf("filename=") + 9).Replace("\"", "").Split(';')[0];
                fileName = ReplaceInvalidChars(Uri.UnescapeDataString(fileName));

                string folderName = "";
                //create a new direcory
                if (createDir)
                {
                    string extension = Path.GetExtension(fileName);
                    folderName = fileName.Substring(0, fileName.Length - extension.Length);
                    if (!Directory.Exists(path + "\\" + fileName.Substring(0, fileName.Length - extension.Length)))
                        Directory.CreateDirectory(path + "\\" + fileName.Substring(0, fileName.Length - extension.Length));
                }

                BinaryWriter binWriter = new BinaryWriter(File.Open(path + "\\" + folderName + fileName, FileMode.Create));
                response.GetResponseStream().CopyTo(binWriter.BaseStream);
                binWriter.Close();
                binWriter.Dispose();
                response.Dispose();
                return path + "\\" + folderName;
            }
            return "";
        }

        public static string downloadMega(string url, string path, bool createDir = true)
        {
            CG.Web.MegaApiClient.MegaApiClient client = new CG.Web.MegaApiClient.MegaApiClient();
            client.LoginAnonymous();

            Uri fileLink = new Uri(url);

            CG.Web.MegaApiClient.INode node = client.GetNodeFromLink(fileLink);

            string result = "";
            if (createDir)
            {
                string extension = Path.GetExtension(node.Name);
                result = node.Name.Substring(0, node.Name.Length - extension.Length)+"\\";
                if (!Directory.Exists(path + "\\" + node.Name.Substring(0, node.Name.Length - extension.Length)))
                    Directory.CreateDirectory(path + "\\" + node.Name.Substring(0, node.Name.Length - extension.Length));
            }

            client.DownloadFile(fileLink, path + "\\" + result + node.Name);

            client.Logout();
            return path + "\\" + result;
        }

        /// <summary>
        /// get the image, convert it and save it as png
        /// </summary>
        /// <param name="imageUrl"></param>
        /// <param name="filename"></param>
        private static void saveImage(string imageUrl, string filename)
        {
            using (WebClient webClient = new WebClient())
            {
                byte[] data = webClient.DownloadData(imageUrl);

                using (var stream = new MemoryStream(data))
                {
                    using (var image = new MagickImage(stream))
                    {
                        using (var outputStream = new MemoryStream())
                        {
                            image.Format = MagickFormat.Png;
                            image.Quality = 80;
                            image.Write(outputStream);
                            using (var yourImage = Image.FromStream(outputStream))
                            {
                                yourImage.Save(filename, ImageFormat.Png);
                            }
                        }
                    }
                }
            }
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
                string result = converter.Convert(data);
                writer.Write(result);
            }
        }
        #endregion

        #region dirty little helpers
        public static bool ArrayStartsWith(byte[] source, byte[] pattern)
        {
            int i = 0;
            int match = pattern.Count();
            foreach (var b in pattern)
            {
                if (source[i] == b)
                    match--;
                i++;
            }
            if (match == 0)
                return true;
            return false;
        }

        public static string ReplaceInvalidChars(string filename)
        {
            return string.Join("_", filename.Split(Path.GetInvalidFileNameChars()));
        }

        private static string normalizeUrl(string url)
        {
            string norm = url;
            norm = norm.Replace("&#39;", "'");
            norm = norm.Replace("&amp;", "&");
            return norm;
        }

        private static bool isSameDomain(string a, string b)
        {
            Uri aUri = new Uri(a);
            Uri bUri = new Uri(b);
            if (aUri.Host == bUri.Host)
                return true;
            return false;
        }

        public static string GetFinalRedirect(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return url;

            int maxRedirCount = 8;  // prevent infinite loops
            string newUrl = url;
            do
            {
                HttpWebRequest req = null;
                HttpWebResponse resp = null;
                try
                {
                    req = (HttpWebRequest)HttpWebRequest.Create(url);
                    req.Method = "HEAD";
                    req.AllowAutoRedirect = false;
                    resp = (HttpWebResponse)req.GetResponse();
                    switch (resp.StatusCode)
                    {
                        case HttpStatusCode.OK:
                            return newUrl;
                        case (System.Net.HttpStatusCode)308:
                            newUrl = resp.Headers["Location"];
                            break;
                        case HttpStatusCode.Redirect:
                        case HttpStatusCode.MovedPermanently:
                        case HttpStatusCode.RedirectKeepVerb:
                        case HttpStatusCode.RedirectMethod:
                            newUrl = resp.Headers["Location"];
                            if (newUrl == null)
                                return url;

                            if (newUrl.IndexOf("://", System.StringComparison.Ordinal) == -1)
                            {
                                // Doesn't have a URL Schema, meaning it's a relative or absolute URL
                                Uri u = new Uri(new Uri(url), newUrl);
                                newUrl = u.ToString();
                            }
                            break;
                        default:
                            return newUrl;
                    }
                    url = newUrl;
                }
                catch (WebException)
                {
                    // Return the last known good URL
                    return newUrl;
                }
                catch (Exception)
                {
                    return null;
                }
                finally
                {
                    if (resp != null)
                        resp.Close();
                }
            } while (maxRedirCount-- > 0);

            return newUrl;
        }
        #endregion
    }
}


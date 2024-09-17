/*
* Copyright(c) 2024 GiR-Zippo
* Licensed under the Mozilla Public License Version 2.0. See https://github.com/GiR-Zippo/XIV-Modexplorer/blob/main/LICENSE for full license information.
*/

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Text;
using HtmlAgilityPack;
using XIVModExplorer.Scraping.Internal;
using XIVModExplorer.HelperWindows;

namespace XIVModExplorer.Scraping
{
    public partial class Scraper
    {
        /// <summary>
        /// Downloads from GD, only file is supported now
        /// </summary>
        /// <param name="url"></param>
        /// <param name="path"></param>
        /// <returns></returns>
        private void downloadGoogleDrive(string url, string path, bool createDir = true)
        {
            if (url.Contains("/drive/folders"))
            {
                HttpWebResponse folderlist;
                try
                {
                    HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                    request.UserAgent = Helper.UserAgent;
                    request.Accept = Helper.Accept;
                    request.Proxy.Credentials = CredentialCache.DefaultCredentials;
                    folderlist = (HttpWebResponse)request.GetResponse();
                }
                catch (WebException)
                {
                    current_downloadPath = "";
                    DataReady.Add("url");
                    return;
                }

                StreamReader sr = new StreamReader(folderlist.GetResponseStream());
                var htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(sr.ReadToEnd());
                var f = htmlDoc.DocumentNode.SelectNodes("//*[text()[contains(., '.ttmp') or contains(., '.pmp') or contains(., '.pose')]]");
                if (f.Count == 0)
                    return;

                string folderName = "";
                FileDownloader fileDownloader = new FileDownloader();
                foreach (HtmlNode node in f)
                {
                    if (node.Name != "div")
                        continue;
                    string iName = node.InnerHtml;

                    if (createDir && folderName == "")
                    {
                        folderName = Path.GetFileNameWithoutExtension(iName);
                        if (!Directory.Exists(path + "\\" + folderName))
                            Directory.CreateDirectory(path + "\\" + folderName);
                    }

                    var dlId = node.ParentNode.ParentNode.ParentNode.ParentNode.ParentNode.GetAttributeValue("data-id", "");
                    LogWindow.Message("[Scraper - gDrive] Downloading: " + iName);
                    fileDownloader.DownloadFile("https://drive.usercontent.google.com/download?id=" + dlId + "&export=download&confirm=t", path + "\\" + folderName + "\\" + iName);
                    LogWindow.Message("[Scraper - gDrive] Done downloading: " + iName);
                }

                sr.Close();
                fileDownloader.Dispose();
                current_downloadPath = path + "\\" + folderName;
                DataReady.Add("url");
                return;
            }
            else if (url.Contains("file/d/"))
            {
                url = url.TrimEnd('/').Remove(url.LastIndexOf('/'));
                url = url + "&export=download&confirm=t";
                url = url.Replace("https://drive.google.com/file/d/", "https://drive.usercontent.google.com/download?id=");
                HttpWebResponse response;
                try
                {
                    HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                    request.UserAgent = Helper.UserAgent;
                    request.Accept = Helper.Accept;
                    request.Proxy.Credentials = CredentialCache.DefaultCredentials;
                    response = (HttpWebResponse)request.GetResponse();
                }
                catch (WebException)
                {
                    current_downloadPath = "";
                    DataReady.Add("url");
                    return;
                }

                //If this is a binary, save the stream
                if (response.ContentType.Contains("application"))
                {
                    string fileName = response.Headers["Content-Disposition"].Substring(response.Headers["Content-Disposition"].IndexOf("filename=") + 9).Replace("\"", "").Split(';')[0];
                    fileName = Helper.ReplaceInvalidChars(Uri.UnescapeDataString(fileName));

                    string folderName = "";
                    //create a new direcory
                    if (createDir)
                    {
                        string extension = Path.GetExtension(fileName);
                        folderName = fileName.Substring(0, fileName.Length - extension.Length);
                        if (!Directory.Exists(path + "\\" + fileName.Substring(0, fileName.Length - extension.Length)))
                            Directory.CreateDirectory(path + "\\" + fileName.Substring(0, fileName.Length - extension.Length));
                    }

                    BinaryWriter binWriter = new BinaryWriter(File.Open(path + "\\" + folderName + "\\" + fileName, FileMode.Create));
                    response.GetResponseStream().CopyTo(binWriter.BaseStream);
                    binWriter.Close();
                    binWriter.Dispose();
                    response.Dispose();
                    current_downloadPath = path + "\\" + folderName;
                    DataReady.Add("url");
                    return;
                }

                //TODO
                else if(response.Headers["Content-Type"].Contains("text/html;"))
                {
                    StreamReader sr = new StreamReader(response.GetResponseStream());
                    var html = sr.ReadToEnd();
                    var fileName = Helper.ReplaceInvalidChars(html.Split(new string[] { "uc-name-size" }, StringSplitOptions.RemoveEmptyEntries)[4].Split('>')[2].Split('<')[0]);
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
                    FileDownloader fileDownloader = new FileDownloader();
                    fileDownloader.DownloadFile(url, path + "\\" + fileName);
                    current_downloadPath = path + "\\" + folderName;
                    DataReady.Add("url");
                    return;
                }
            }
            current_downloadPath = "";
            DataReady.Add("url");
            return;
        }
    }

    public class FileDownloader : IDisposable
    {
        private const string GOOGLE_DRIVE_DOMAIN = "drive.google.com";
        private const string GOOGLE_DRIVE_DOMAIN2 = "https://drive.google.com";

        // In the worst case, it is necessary to send 3 download requests to the Drive address
        //   1. an NID cookie is returned instead of a download_warning cookie
        //   2. download_warning cookie returned
        //   3. the actual file is downloaded
        private const int GOOGLE_DRIVE_MAX_DOWNLOAD_ATTEMPT = 3;

        public delegate void DownloadProgressChangedEventHandler(object sender, DownloadProgress progress);

        // Custom download progress reporting (needed for Google Drive)
        public class DownloadProgress
        {
            public long BytesReceived, TotalBytesToReceive;
            public object UserState;

            public int ProgressPercentage
            {
                get
                {
                    if (TotalBytesToReceive > 0L)
                        return (int)(((double)BytesReceived / TotalBytesToReceive) * 100);

                    return 0;
                }
            }
        }

        // Web client that preserves cookies (needed for Google Drive)
        private class CookieAwareWebClient : WebClient
        {
            private class CookieContainer
            {
                private readonly Dictionary<string, string> cookies = new Dictionary<string, string>();

                public string this[Uri address]
                {
                    get
                    {
                        string cookie;
                        if (cookies.TryGetValue(address.Host, out cookie))
                            return cookie;

                        return null;
                    }
                    set
                    {
                        cookies[address.Host] = value;
                    }
                }
            }

            private readonly CookieContainer cookies = new CookieContainer();
            public DownloadProgress ContentRangeTarget;

            protected override WebRequest GetWebRequest(Uri address)
            {
                WebRequest request = base.GetWebRequest(address);
                if (request is HttpWebRequest)
                {
                    string cookie = cookies[address];
                    if (cookie != null)
                        ((HttpWebRequest)request).Headers.Set("cookie", cookie);

                    if (ContentRangeTarget != null)
                        ((HttpWebRequest)request).AddRange(0);
                }

                return request;
            }

            protected override WebResponse GetWebResponse(WebRequest request, IAsyncResult result)
            {
                return ProcessResponse(base.GetWebResponse(request, result));
            }

            protected override WebResponse GetWebResponse(WebRequest request)
            {
                return ProcessResponse(base.GetWebResponse(request));
            }

            private WebResponse ProcessResponse(WebResponse response)
            {
                string[] cookies = response.Headers.GetValues("Set-Cookie");
                if (cookies != null && cookies.Length > 0)
                {
                    int length = 0;
                    for (int i = 0; i < cookies.Length; i++)
                        length += cookies[i].Length;

                    StringBuilder cookie = new StringBuilder(length);
                    for (int i = 0; i < cookies.Length; i++)
                        cookie.Append(cookies[i]);

                    this.cookies[response.ResponseUri] = cookie.ToString();
                }

                if (ContentRangeTarget != null)
                {
                    string[] rangeLengthHeader = response.Headers.GetValues("Content-Range");
                    if (rangeLengthHeader != null && rangeLengthHeader.Length > 0)
                    {
                        int splitIndex = rangeLengthHeader[0].LastIndexOf('/');
                        if (splitIndex >= 0 && splitIndex < rangeLengthHeader[0].Length - 1)
                        {
                            long length;
                            if (long.TryParse(rangeLengthHeader[0].Substring(splitIndex + 1), out length))
                                ContentRangeTarget.TotalBytesToReceive = length;
                        }
                    }
                }

                return response;
            }
        }

        private readonly CookieAwareWebClient webClient;
        private readonly DownloadProgress downloadProgress;

        private Uri downloadAddress;
        private string downloadPath;

        private bool asyncDownload;
        private object userToken;

        private bool downloadingDriveFile;
        private int driveDownloadAttempt;

        public event DownloadProgressChangedEventHandler DownloadProgressChanged;
        public event AsyncCompletedEventHandler DownloadFileCompleted;

        public FileDownloader()
        {
            webClient = new CookieAwareWebClient();
            webClient.DownloadProgressChanged += DownloadProgressChangedCallback;
            webClient.DownloadFileCompleted += DownloadFileCompletedCallback;

            downloadProgress = new DownloadProgress();
        }

        public void DownloadFile(string address, string fileName)
        {
            DownloadFile(address, fileName, false, null);
        }

        public void DownloadFileAsync(string address, string fileName, object userToken = null)
        {
            DownloadFile(address, fileName, true, userToken);
        }

        private void DownloadFile(string address, string fileName, bool asyncDownload, object userToken)
        {
            downloadingDriveFile = address.StartsWith(GOOGLE_DRIVE_DOMAIN) || address.StartsWith(GOOGLE_DRIVE_DOMAIN2);
            if (downloadingDriveFile)
            {
                address = GetGoogleDriveDownloadAddress(address);
                driveDownloadAttempt = 1;

                webClient.ContentRangeTarget = downloadProgress;
            }
            else
                webClient.ContentRangeTarget = null;

            downloadAddress = new Uri(address);
            downloadPath = fileName;

            downloadProgress.TotalBytesToReceive = -1L;
            downloadProgress.UserState = userToken;

            this.asyncDownload = asyncDownload;
            this.userToken = userToken;

            DownloadFileInternal();
        }

        private void DownloadFileInternal()
        {
            if (!asyncDownload)
            {
                webClient.DownloadFile(downloadAddress, downloadPath);

                // This callback isn't triggered for synchronous downloads, manually trigger it
                DownloadFileCompletedCallback(webClient, new AsyncCompletedEventArgs(null, false, null));
            }
            else if (userToken == null)
                webClient.DownloadFileAsync(downloadAddress, downloadPath);
            else
                webClient.DownloadFileAsync(downloadAddress, downloadPath, userToken);
        }

        private void DownloadProgressChangedCallback(object sender, System.Net.DownloadProgressChangedEventArgs e)
        {
            if (DownloadProgressChanged != null)
            {
                downloadProgress.BytesReceived = e.BytesReceived;
                if (e.TotalBytesToReceive > 0L)
                    downloadProgress.TotalBytesToReceive = e.TotalBytesToReceive;

                DownloadProgressChanged(this, downloadProgress);
            }
        }

        private void DownloadFileCompletedCallback(object sender, AsyncCompletedEventArgs e)
        {
            if (!downloadingDriveFile)
            {
                if (DownloadFileCompleted != null)
                    DownloadFileCompleted(this, e);
            }
            else
            {
                if (driveDownloadAttempt < GOOGLE_DRIVE_MAX_DOWNLOAD_ATTEMPT && !ProcessDriveDownload())
                {
                    // Try downloading the Drive file again
                    driveDownloadAttempt++;
                    DownloadFileInternal();
                }
                else if (DownloadFileCompleted != null)
                    DownloadFileCompleted(this, e);
            }
        }

        // Downloading large files from Google Drive prompts a warning screen and requires manual confirmation
        // Consider that case and try to confirm the download automatically if warning prompt occurs
        // Returns true, if no more download requests are necessary
        private bool ProcessDriveDownload()
        {
            FileInfo downloadedFile = new FileInfo(downloadPath);
            if (downloadedFile == null)
                return true;

            // Confirmation page is around 50KB, shouldn't be larger than 60KB
            if (downloadedFile.Length > 60000L)
                return true;

            // Downloaded file might be the confirmation page, check it
            string content;
            using (var reader = downloadedFile.OpenText())
            {
                // Confirmation page starts with <!DOCTYPE html>, which can be preceeded by a newline
                char[] header = new char[20];
                int readCount = reader.ReadBlock(header, 0, 20);
                if (readCount < 20 || !(new string(header).Contains("<!DOCTYPE html>")))
                    return true;

                content = reader.ReadToEnd();
            }

            int linkIndex = content.LastIndexOf("href=\"/uc?");
            if (linkIndex >= 0)
            {
                linkIndex += 6;
                int linkEnd = content.IndexOf('"', linkIndex);
                if (linkEnd >= 0)
                {
                    downloadAddress = new Uri("https://drive.google.com" + content.Substring(linkIndex, linkEnd - linkIndex).Replace("&amp;", "&"));
                    return false;
                }
            }

            int formIndex = content.LastIndexOf("<form id=\"download-form\"");
            if (formIndex >= 0)
            {
                int formEndIndex = content.IndexOf("</form>", formIndex + 10);
                int inputIndex = formIndex;
                StringBuilder sb = new StringBuilder().Append("https://drive.usercontent.google.com/download");
                bool isFirstArgument = true;
                while ((inputIndex = content.IndexOf("<input type=\"hidden\"", inputIndex + 10)) >= 0 && inputIndex < formEndIndex)
                {
                    linkIndex = content.IndexOf("name=", inputIndex + 10) + 6;
                    sb.Append(isFirstArgument ? '?' : '&').Append(content, linkIndex, content.IndexOf('"', linkIndex) - linkIndex).Append('=');

                    linkIndex = content.IndexOf("value=", linkIndex) + 7;
                    sb.Append(content, linkIndex, content.IndexOf('"', linkIndex) - linkIndex);

                    isFirstArgument = false;
                }

                downloadAddress = new Uri(sb.ToString());
                return false;
            }

            return true;
        }

        // Handles the following formats (links can be preceeded by https://):
        // - drive.google.com/open?id=FILEID&resourcekey=RESOURCEKEY
        // - drive.google.com/file/d/FILEID/view?usp=sharing&resourcekey=RESOURCEKEY
        // - drive.google.com/uc?id=FILEID&export=download&resourcekey=RESOURCEKEY
        private string GetGoogleDriveDownloadAddress(string address)
        {
            int index = address.IndexOf("id=");
            int closingIndex;
            if (index > 0)
            {
                index += 3;
                closingIndex = address.IndexOf('&', index);
                if (closingIndex < 0)
                    closingIndex = address.Length;
            }
            else
            {
                index = address.IndexOf("file/d/");
                if (index < 0) // address is not in any of the supported forms
                    return string.Empty;

                index += 7;

                closingIndex = address.IndexOf('/', index);
                if (closingIndex < 0)
                {
                    closingIndex = address.IndexOf('?', index);
                    if (closingIndex < 0)
                        closingIndex = address.Length;
                }
            }

            string fileID = address.Substring(index, closingIndex - index);

            index = address.IndexOf("resourcekey=");
            if (index > 0)
            {
                index += 12;
                closingIndex = address.IndexOf('&', index);
                if (closingIndex < 0)
                    closingIndex = address.Length;

                string resourceKey = address.Substring(index, closingIndex - index);
                return string.Concat("https://drive.google.com/uc?id=", fileID, "&export=download&resourcekey=", resourceKey, "&confirm=t");
            }
            else
                return string.Concat("https://drive.google.com/uc?id=", fileID, "&export=download&confirm=t");
        }

        public void Dispose()
        {
            webClient.Dispose();
        }
    }
}
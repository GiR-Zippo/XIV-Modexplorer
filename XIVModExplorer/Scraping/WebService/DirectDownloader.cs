/*
* Copyright(c) 2024 GiR-Zippo
* Licensed under the Mozilla Public License Version 2.0. See https://github.com/GiR-Zippo/XIV-Modexplorer/blob/main/LICENSE for full license information.
*/

using Downloader;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using XIVModExplorer.HelperWindows;
using static XIVModExplorer.Scraping.WebService;

namespace XIVModExplorer.Scraping
{
    public class DirectDownloader : IDisposable
    {
        DownloadConfiguration downloadOpt { get; set; }
        DownloadService downloader { get; set; }

        public GetRequest Request { get; set; }

        public DirectDownloader(GetRequest request, CookieContainer cookies)
        {
            Request = request;
            downloadOpt = new DownloadConfiguration()
            {
                // file parts to download, the default value is 1
                ChunkCount = 2,
                // the maximum number of times to fail
                MaxTryAgainOnFailover = 5,
                // release memory buffer after each 50 MB
                MaximumMemoryBufferBytes = 1024 * 1024 * 50,
                // download parts of the file as parallel or not. The default value is false
                ParallelDownload = true,
                // number of parallel downloads. The default value is the same as the chunk count
                ParallelCount = 2,
                // timeout (millisecond) per stream block reader, default values is 1000
                Timeout = 1000,
                // set true if you want to download just a specific range of bytes of a large file
                RangeDownload = false,
                // floor offset of download range of a large file
                RangeLow = 0,
                // ceiling offset of download range of a large file
                RangeHigh = 0,
                // clear package chunks data when download completed with failure, default value is false
                ClearPackageOnCompletionWithFailure = true,
                // config and customize request headers
                RequestConfiguration =
                {
                    Accept = "*/*",
                    CookieContainer = cookies,
                    Headers = new WebHeaderCollection(),
                    KeepAlive = false, // default value is false
                    ProtocolVersion = HttpVersion.Version11, // default value is HTTP 1.1
                    UseDefaultCredentials = false,
                    UserAgent = request.UserAgent,
                }
            };

            downloader = new DownloadService(downloadOpt);
            downloader.DownloadStarted += Downloader_DownloadStarted;
            downloader.ChunkDownloadProgressChanged += Downloader_ChunkDownloadProgressChanged;
            downloader.DownloadProgressChanged += Downloader_DownloadProgressChanged;
            downloader.DownloadFileCompleted += Downloader_DownloadFileCompleted;
        }

        public void Download()
        {
            ProgressWindow.Show(Request.Url, "Downloading");
            LogWindow.Message($"[Scraper] Got a direct download link");
            Stream destinationStream = downloader.DownloadFileTaskAsync(Request.Url).Result;
            if (downloader.Status == DownloadStatus.Failed)
            {
                Request.ResponseCode = HttpStatusCode.ServiceUnavailable;
                Request.ResponseMsg = "Download failed";
                LogWindow.Message($"[Scraper] direct download failed");
                return;
            }

            HttpResponseMessage response = new HttpResponseMessage()
            {
                Content = new StreamContent(destinationStream)
            };
            Request.ResponseBody = response.Content;
            Request.Host = new Uri(Request.Url).DnsSafeHost;
            Request.ResponseCode = HttpStatusCode.OK;
            Request.ResponseMsg = "";

            LogWindow.Message($"[Scraper] direct download done");
            ProgressWindow.WndClose();
        }

        private void Downloader_DownloadStarted(object sender, DownloadStartedEventArgs e)
        {
        }

        private void Downloader_ChunkDownloadProgressChanged(object sender, Downloader.DownloadProgressChangedEventArgs e)
        {

        }

        private void Downloader_DownloadProgressChanged(object sender, Downloader.DownloadProgressChangedEventArgs e)
        {
            ProgressWindow.Update(e.ProgressPercentage);
        }

        private void Downloader_DownloadFileCompleted(object sender, System.ComponentModel.AsyncCompletedEventArgs e)
        {
        }

        public void Dispose()
        {
            downloader.DownloadStarted -= Downloader_DownloadStarted;
            downloader.ChunkDownloadProgressChanged -= Downloader_ChunkDownloadProgressChanged;
            downloader.DownloadProgressChanged -= Downloader_DownloadProgressChanged;
            downloader.DownloadFileCompleted -= Downloader_DownloadFileCompleted;
            downloader.Dispose();
        }
    }
}

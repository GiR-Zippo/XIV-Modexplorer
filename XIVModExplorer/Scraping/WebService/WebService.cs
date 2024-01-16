/*
* Copyright(c) 2024 GiR-Zippo
* Licensed under the Mozilla Public License Version 2.0. See https://github.com/GiR-Zippo/XIV-Modexplorer/blob/main/LICENSE for full license information.
*/

using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace XIVModExplorer.Scraping
{
    public class WebService : IDisposable
    {
        public enum Requester
        {
            NONE = 0,
            HTML = 1,
            IMAGE = 2,
            DOWNLOAD = 3,
            PENUMBRA = 4
        }

        public class GetRequest :IDisposable
        {
            public string Url { get; set; } = "";
            public Requester Requester { get; set; } = Requester.NONE;
            public object Parameters { get; set; } = null;
            public string UserAgent { get; set; } = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)";
            public string Accept { get; set; } = "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8";

            public HttpContent ResponseBody { get; set; } = null;

            public HttpStatusCode ResponseCode { get; set; } = HttpStatusCode.Unused;
            public string ResponseMsg { get; set; } = "";

            public string Host { get; set; } = "";

            public System.Collections.ObjectModel.ReadOnlyCollection<OpenQA.Selenium.Cookie> CookieJar { get; set; } = null;

            public void Dispose()
            {
                ResponseBody.Dispose();
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }

        public class PostRequest : IDisposable
        {
            public string Url { get; set; } = "";
            public Requester Requester { get; set; } = Requester.NONE;
            public object Parameters { get; set; } = null;
            public string UserAgent { get; set; } = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)";
            public string Accept { get; set; } = "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8";

            public HttpContent Content { get; set; } = null;

            public HttpStatusCode ResponseCode { get; set; } = HttpStatusCode.Unused;
            public string ResponseMsg { get; set; } = "";

            public string Host { get; set; } = "";

            public System.Collections.ObjectModel.ReadOnlyCollection<OpenQA.Selenium.Cookie> CookieJar { get; set; } = null;

            public void Dispose()
            {
                Content.Dispose();
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }

        public EventHandler<object> OnRequestFinished;

        private HttpClient httpClient { get; set; } = null;
        private HttpClientHandler httpClientHandler { get;set;} = null;

        private ConcurrentQueue<object> downloadQueue = new ConcurrentQueue<object>();
        private CancellationTokenSource cancelTokenSource;

        #region Construct/Destruct
        private static WebService _instance;
        public static bool Initialized => _instance != null;
        public static WebService Instance => _instance ?? throw new Exception("Init WebService first");
        private bool disposedValue;

        public static void Initialize()
        {
            if (Initialized) return;
            _instance = CreateInstance();
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
                    this.StopWorkerThread();
                    this.httpClient.Dispose();
                }
                disposedValue = true;
            }
        }

        private WebService()
        {
            httpClientHandler = new HttpClientHandler
            {
                UseProxy = true,
                MaxAutomaticRedirections = 2,
                MaxConnectionsPerServer = 2
            };


            httpClient = new HttpClient(handler: httpClientHandler);
            disposedValue = false;
            StartWorkerThread();
        }
        internal static WebService CreateInstance()
        {
            return new WebService();
        }
        #endregion

        private void StartWorkerThread()
        {
            downloadQueue = new ConcurrentQueue<object>();
            cancelTokenSource = new CancellationTokenSource();
            Task.Factory.StartNew(() => RunEventsHandler(cancelTokenSource.Token), TaskCreationOptions.LongRunning);
        }

        private void StopWorkerThread()
        {
            cancelTokenSource.Cancel();
            while (downloadQueue.TryDequeue(out _))
            { }
        }

        private async Task RunEventsHandler(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                while (downloadQueue.TryDequeue(out var request))
                {
                    if (token.IsCancellationRequested)
                        break;

                    if (request is GetRequest)
                        _ = GetHtmlAsync(request as GetRequest);
                    if (request is PostRequest)
                        _ = PostHtmlAsync(request as PostRequest);

                }
                await Task.Delay(100, token).ContinueWith(tsk => { });
            }
        }

        public void AddToDownload(object dl)
        {
            downloadQueue.Enqueue(dl);
        }

        private async Task GetHtmlAsync(GetRequest request)
        {
            foreach (Cookie co in httpClientHandler.CookieContainer.GetCookies(new Uri(request.Url)))
            {
                co.Expires = DateTime.Now.Subtract(TimeSpan.FromDays(1));
            }

            httpClient.DefaultRequestHeaders.Add("User-Agent", request.UserAgent);
            if (request.Accept != "")
                httpClient.DefaultRequestHeaders.Add("Accept", request.Accept);

            if (request.CookieJar != null)
            {
                foreach (var cookie in request.CookieJar)
                    httpClientHandler.CookieContainer.Add(new Cookie(cookie.Name, cookie.Value, cookie.Path, string.IsNullOrWhiteSpace(cookie.Domain) ? new Uri(request.Url).Host : cookie.Domain));
            }

            HttpResponseMessage response = await httpClient.GetAsync(request.Url);
            request.ResponseBody = response.Content;
            request.Host = new Uri(request.Url).DnsSafeHost;
            request.ResponseCode = response.StatusCode;
            request.ResponseMsg = response.ReasonPhrase;
            OnRequestFinished(this, request);
        }

        private async Task PostHtmlAsync(PostRequest request)
        {
            foreach (Cookie co in httpClientHandler.CookieContainer.GetCookies(new Uri(request.Url)))
            {
                co.Expires = DateTime.Now.Subtract(TimeSpan.FromDays(1));
            }

            httpClient.DefaultRequestHeaders.Add("User-Agent", request.UserAgent);
            if (request.Accept != "")
                httpClient.DefaultRequestHeaders.Add("Accept", request.Accept);

            if (request.CookieJar != null)
            {
                foreach (var cookie in request.CookieJar)
                    httpClientHandler.CookieContainer.Add(new Cookie(cookie.Name, cookie.Value, cookie.Path, string.IsNullOrWhiteSpace(cookie.Domain) ? new Uri(request.Url).Host : cookie.Domain));
            }
            try
            {
                HttpResponseMessage response = await httpClient.PostAsync(request.Url, request.Content);
                request.Content = response.Content;
                request.Host = new Uri(request.Url).DnsSafeHost;
                request.ResponseCode = response.StatusCode;
                request.ResponseMsg = response.ReasonPhrase;
            }
            catch (HttpRequestException e)
            {
                request.ResponseCode = HttpStatusCode.ServiceUnavailable;
                request.ResponseMsg = e.InnerException.Message;
            }
            OnRequestFinished(this, request);
        }

    }
}

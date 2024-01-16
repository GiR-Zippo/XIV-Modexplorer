/*
* Copyright(c) 2024 GiR-Zippo
* Licensed under the Mozilla Public License Version 2.0. See https://github.com/GiR-Zippo/XIV-Modexplorer/blob/main/LICENSE for full license information.
*/

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using XIVModExplorer.Scraping;
using XIVModExplorer.HelperWindows;

namespace XIVModExplorer.Penumbra
{
    public class Entry
    {
        public object Length { get; set; } = null;
        public object Unk { get; set; } = null;
        public string Item1 { get; set; } = "";
        public string Item2 { get; set; } = "";
    };

    public class ModInstallData
    {
        public string Path { get; set; } = "";
    }

    public class RedrawData
    {
        public enum RedrawType
        {
            Redraw,
            AfterGPose,
        }
        public string Name { get; set; } = string.Empty;
        public int ObjectTableIndex { get; set; } = -1;
        public RedrawType Type { get; set; } = RedrawType.Redraw;
    }

    public class PenumbraApi : IDisposable
    {
        private const string Url = "http://localhost:42069/api";

        public PenumbraApi()
        {
            WebService.Instance.OnRequestFinished += Instance_Response;
        }

        public void Dispose()
        {
            WebService.Instance.OnRequestFinished -= Instance_Response;
        }

        private void Instance_Response(object sender, object e)
        {
            if (e is WebService.PostRequest)
                PostResponse(e as WebService.PostRequest);

        }

        private void PostResponse(WebService.PostRequest post)
        {
            if (post.ResponseCode == HttpStatusCode.ServiceUnavailable)
                MessageWindow.Show("Penumbra HTTP isn't active!\r\n"+post.ResponseMsg, "Error");
        }


        public List<Entry> GetMods()
        {
            string URI = "http://localhost:42069/api/mods";
            using (WebClient wc = new WebClient())
            {
                wc.Headers[HttpRequestHeader.ContentType] = "application/json; charset=utf-8";
                string HtmlResult = wc.DownloadString(URI);

                return JsonConvert.DeserializeObject<List<Entry>>(HtmlResult);
            }
        }

        /// <summary>
        /// -1 all
        /// 0 self
        /// </summary>
        /// <param name="targetIndex"></param>
        public void Redraw(int targetIndex)
        {
            RedrawData data = new RedrawData();
            data.ObjectTableIndex = targetIndex;
            data.Type = RedrawData.RedrawType.Redraw;

            WebService.PostRequest postRequest = new WebService.PostRequest();
            postRequest.Requester = WebService.Requester.PENUMBRA;
            postRequest.Url = Url + "/redraw";
            postRequest.Accept = "application/json";
            postRequest.Content = Serialize(data);
            postRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            WebService.Instance.AddToDownload(postRequest);

        }
        public void Install(string modPath)
        {
            ModInstallData data = new ModInstallData();
            data.Path = modPath;

            WebService.PostRequest postRequest = new WebService.PostRequest();
            postRequest.Requester = WebService.Requester.PENUMBRA;
            postRequest.Url = Url + "/installmod";
            postRequest.Accept = "application/json";
            postRequest.Content = Serialize(data);
            postRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            WebService.Instance.AddToDownload(postRequest);
        }

        private ByteArrayContent Serialize(object obj)
        {
            string json = JsonConvert.SerializeObject(obj);
            var buffer = Encoding.UTF8.GetBytes(json);
            var byteContent = new ByteArrayContent(buffer);
            return byteContent;
        }

    }
}

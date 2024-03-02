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
        public EventHandler<object> OnModRequestFinished;

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
            if (e is WebService.GetRequest)
            {
                var req = e as WebService.GetRequest;
                if (req.Url.EndsWith("api/mods"))
                    GetModsResponse(e as WebService.GetRequest);
            }
            else if (e is WebService.PostRequest)
                PostResponse(e as WebService.PostRequest);
        }

        /// <summary>
        /// GetModlist and compare (needs some more work)
        /// </summary>
        /// <param name="get"></param>
        /// <returns></returns>
        private void GetModsResponse(WebService.GetRequest get)
        {
            if (get.ResponseCode == HttpStatusCode.ServiceUnavailable)
            {
                MessageWindow.Show("Penumbra HTTP isn't active!\r\n" + get.ResponseMsg, "Error");
                return;
            }
            else
            {
                var t = (get.Parameters as List<object>)[0] as string;
                if (t.Equals("GetMods"))
                {
                    var x = JsonConvert.DeserializeObject<List<Entry>>(get.ResponseBody.ReadAsStringAsync().Result);
                    if (OnModRequestFinished != null)
                        OnModRequestFinished(this, x);
                }
            }
            /*if ((bool)get.Parameters)
            {
                foreach (var i in oList)
                {
                    Regex rgx = new Regex("[^a-zA-Z0-9 -]");
                    var m = x.Find(n => rgx.Replace(n.Item1,"").Contains(rgx.Replace(i.ModName,""))); //n.Item1.Equals(i.ModName));
                    if (m != null)
                    {
                        string dest = rgx.Replace(i.ModName, "");
                        string tar = rgx.Replace(m.Item1, "");
                        int dist = LevenshteinDistance(dest, tar);
                        if (dist < 31)
                        {
                            Debug.WriteLine(dest);
                            Debug.WriteLine(tar);
                            Debug.WriteLine(dist);
                            i.FoundInPenumbra = true;
                            i.PenumbraName = m.Item1;
                            Database.Instance.SaveData(i);
                        }
                    }

                    i.FoundInPenumbra = false;
                    i.PenumbraName = "";
                    Database.Instance.SaveData(i);
                }
            }
            else
            {
                foreach (var i in x)
                {
                    Regex rgx = new Regex("[^a-zA-Z0-9 -]");
                    var m = oList.Find(n => (n.ModName != null) && rgx.Replace(n.ModName, "").Contains(rgx.Replace(i.Item1, "")) && !n.FoundInPenumbra);
                    if (m != null)
                    {
                        string dest = rgx.Replace(m.ModName, "");
                        string tar = rgx.Replace(i.Item1, "");
                        int dist = LevenshteinDistance(dest, tar);
                        if (dist > 30)
                            continue;

                        Debug.WriteLine(dest);
                        Debug.WriteLine(tar);
                        
                        m.FoundInPenumbra = true;
                        m.PenumbraName = i.Item1;
                        Database.Instance.SaveData(m);
                    }
                }
            }*/


            //oList.Clear();
        }

        private void PostResponse(WebService.PostRequest post)
        {
            if (post.ResponseCode == HttpStatusCode.ServiceUnavailable)
                MessageWindow.Show("Penumbra HTTP isn't active!\r\n"+post.ResponseMsg, "Error");
        }


        public void GetMods(bool forceReread = false)
        {
            WebService.Instance.AddToQueue(new WebService.GetRequest()
            {
                Url = "http://localhost:42069/api/mods",
                Requester = WebService.Requester.PENUMBRA,
                Parameters = new List<object> { "GetMods", forceReread }
            });
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
            WebService.Instance.AddToQueue(postRequest);

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
            WebService.Instance.AddToQueue(postRequest);
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

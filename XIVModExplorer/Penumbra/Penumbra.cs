/*
* Copyright(c) 2024 GiR-Zippo
* Licensed under the Mozilla Public License Version 2.0. See https://github.com/GiR-Zippo/XIV-Modexplorer/blob/main/LICENSE for full license information.
*/

using Newtonsoft.Json;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

namespace XIVModExplorer.Penumbra
{
    public class PenumbraApi
    {
        public PenumbraApi()
        {
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
        public async void Redraw(int targetIndex)
        {
            RedrawData data = new RedrawData();
            data.ObjectTableIndex = targetIndex;
            data.Type = RedrawData.RedrawType.Redraw;

            await PenumbraHttpApi.Post("/redraw", data);

            await Task.Delay(500);
        }
        public async void Install(string modPath)
        {
            ModInstallData data = new ModInstallData();
            data.Path = modPath;
            await PenumbraHttpApi.Post("/installmod", data);
            await Task.Delay(500);
        }
    }
}

// © Anamnesis.
// Licensed under the MIT license.

using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

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

    public static class PenumbraHttpApi
    {
        private const string Url = "http://localhost:42069/api";
        private const int TimeoutMs = 500;
        private static bool calledWarningOnce = false;
        public static async Task Post(string route, object content)
        {
            await PostRequest(route, content);
        }

        public static async Task<T> Post<T>(string route, object content)
        {
            HttpResponseMessage response = await PostRequest(route, content);

            StreamReader sr = new StreamReader(await response.Content.ReadAsStreamAsync());
            string json = sr.ReadToEnd();

            return JsonConvert.DeserializeObject<T>(json);
        }

        private async static Task<HttpResponseMessage> PostRequest(string route, object content)
        {
            if (!route.StartsWith("/"))
                route = '/' + route;

            try
            {
                string json = JsonConvert.SerializeObject(content);
                HttpClient client = new HttpClient();
                client.Timeout = TimeSpan.FromMilliseconds(TimeoutMs);
                var buffer = Encoding.UTF8.GetBytes(json);
                var byteContent = new ByteArrayContent(buffer);
                byteContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                var response = await client.PostAsync(Url + route, byteContent);
                return response;
            }
            catch (Exception ex)
            {
                var t = ex;
                Debug.WriteLine("");
                if (!calledWarningOnce)
                {
                    MessageBox.Show(@"Error communicating with Penumbra. Try to select ""Enable HTTP API"" inside of penumbra under ""Settings -> Advanced"".");
                    calledWarningOnce = true;
                }
                return null;
            }
        }
    }
}

using Newtonsoft.Json;
using System;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Runtime.Remoting.Contexts;
using System.Text;
using System.Web;
using XIVModExplorer.Caching;


namespace XIVModExplorer.RestApi
{
    public class ExplorerRestServer : IDisposable
    {
        private static ExplorerRestServer _instance;
        public static bool Initialized => _instance != null;
        public static ExplorerRestServer Instance => _instance ?? throw new Exception("Init WebService first");
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

                }
                disposedValue = true;
            }
        }

        internal static ExplorerRestServer CreateInstance()
        {
            return new ExplorerRestServer();
        }

        private ExplorerRestServer()
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add("http://127.0.0.1:" + Port.ToString() + "/");
            _listener.Start();
            Receive();
        }


        public int Port = 8081;

        private HttpListener _listener;

        public void Start()
        {

        }

        public void Stop()
        {
            _listener.Stop();
        }

        private void Receive()
        {
            _listener.BeginGetContext(new AsyncCallback(ListenerCallback), _listener);
        }

        private void ListenerCallback(IAsyncResult result)
        {
            if (_listener.IsListening)
            {
                var context = _listener.EndGetContext(result);
                var request = context.Request;

                // do something with the request
                //Console.WriteLine($"{request.Url.LocalPath}");

                try
                {
                    if (request.Url.AbsolutePath.StartsWith("/api/list"))
                        GetModList(request.Url.AbsolutePath, request.Url.Query, context.Response);
                    else if (request.Url.AbsolutePath.StartsWith("/api/item"))
                        GetModItem(request.Url.AbsolutePath, context.Response);
                    else
                    {

                        //var f = Database.Instance.GetModListAsync().Result;
                        //byte[] output = Encoding.ASCII.GetBytes(JsonConvert.SerializeObject(f.First()));


                            var response = context.Response;
                            response.StatusCode = (int)HttpStatusCode.NotFound;
                            //response.ContentType = "application/json";
                            //response.OutputStream.Write(output, 0, output.Length);
                            response.OutputStream.Close();

                    }
                }
                catch { }
                Receive();
            }
        }

        private void GetModItem(string localPath, HttpListenerResponse response)
        {
            string mod_id = localPath.Split('=')[1];

            ModEntry mod = Database.Instance.GetModByIdAsync(mod_id);

            if (mod == null)
                response.StatusCode = (int)HttpStatusCode.NotFound;
            else
            {


                byte[] output = Encoding.ASCII.GetBytes(JsonConvert.SerializeObject(mod));
                response.StatusCode = (int)HttpStatusCode.OK;
                response.ContentType = "application/json";
                response.OutputStream.Write(output, 0, output.Length);
            }
            response.OutputStream.Close();
        }

        private void GetModList(string localPath, string queries, HttpListenerResponse response)
        {
            var queryString = HttpUtility.ParseQueryString(queries);
            if (queryString.HasKeys())
            {
                UInt32 type = Convert.ToUInt32(queryString.GetValues("cat")[0]);
                Console.WriteLine(queryString["cot"]);
                int idx_page = Convert.ToInt32(localPath.Split('=')[1]);
                byte[] output = Encoding.ASCII.GetBytes(JsonConvert.SerializeObject(Database.Instance.FindModsAsync("","", type, 0, "").Result.ToList()));
                response.StatusCode = (int)HttpStatusCode.OK;
                response.ContentType = "application/json";
                response.OutputStream.Write(output, 0, output.Length);
                response.OutputStream.Close();
            }
            else
            {
                int idx_page = Convert.ToInt32(localPath.Split('=')[1]);
                byte[] output = Encoding.ASCII.GetBytes(JsonConvert.SerializeObject(Database.Instance.GetModListAsync().Result.GetRange(idx_page * 12, 12).ToList()));
                response.StatusCode = (int)HttpStatusCode.OK;
                response.ContentType = "application/json";
                response.OutputStream.Write(output, 0, output.Length);
                response.OutputStream.Close();
            }
        }
    }
}


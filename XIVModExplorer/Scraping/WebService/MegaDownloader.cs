/*
* Copyright(c) 2024 GiR-Zippo
* Licensed under the Mozilla Public License Version 2.0. See https://github.com/GiR-Zippo/XIV-Modexplorer/blob/main/LICENSE for full license information.
*/

using CG.Web.MegaApiClient;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using XIVModExplorer.HelperWindows;

namespace XIVModExplorer.Scraping
{
    public partial class Scraper
    {
        public Task downloadMega(string url, string path, bool createDir = true)
        {
            LogWindow.Message("[MegaDownloader] Starting...");
            MegaApiClient client = new MegaApiClient();
            client.LoginAnonymous();

            //in case it's a folder, unfold(er) it
            if (url.Contains("/folder/"))
            {
                MegaDownloader_DownloadFolder(client, url, path, createDir);
                return Task.CompletedTask;
            }

            //If it's a file, just download
            LogWindow.Message($"[MegaDownloader] Downloading file");
            Uri fileLink = new Uri(url);
            INode node = client.GetNodeFromLink(fileLink);

            string result = "";
            if (createDir)
            {
                string extension = Path.GetExtension(node.Name);
                result = node.Name.Substring(0, node.Name.Length - extension.Length) + "\\";
                if (!Directory.Exists(path + "\\" + node.Name.Substring(0, node.Name.Length - extension.Length)))
                    Directory.CreateDirectory(path + "\\" + node.Name.Substring(0, node.Name.Length - extension.Length));
            }

            client.DownloadFile(fileLink, path + "\\" + result + node.Name);

            client.Logout();
            current_downloadPath = path + "\\" + result;
            DataReady.Add("url");
            LogWindow.Message($"[MegaDownloader] Downloading file done.");
            return Task.CompletedTask;
        }

        private async void MegaDownloader_DownloadFolder(MegaApiClient client, string url, string path, bool createDir)
        {
            LogWindow.Message($"[MegaDownloader] Downloading folder(s)");
            var splitted = url.Split('?');
            var foldeUrl = splitted.FirstOrDefault();
            var folderLink = new Uri(foldeUrl);
            var nodes = await client.GetNodesFromLinkAsync(folderLink);
            Dictionary<string, KeyValuePair<string, string>> dirstruct = new Dictionary<string, KeyValuePair<string, string>>();

            //create the dir structure
            foreach (var node in nodes)
            {
                if (node.Type == NodeType.Root)
                {
                    dirstruct.Add(node.Id, new KeyValuePair<string, string>(node.Id, ""));
                }
                if (node.Type == NodeType.Directory)
                {
                    if (!dirstruct.ContainsKey(node.Id))
                        dirstruct.Add(node.Id, new KeyValuePair<string, string>(node.ParentId, node.Name));
                }

            }

            //download the files
            foreach (var node in nodes)
            {
                //use the name of the root node and create the dir
                if (node.Type == NodeType.Root)
                {
                    if (createDir)
                    {
                        if (!Directory.Exists(path + "\\" + node.Name + "\\ModData"))
                            Directory.CreateDirectory(path + "\\" + node.Name + "\\ModData");

                    }
                    current_downloadPath = path + "\\" + node.Name;
                
                }
                if (node.Type == NodeType.File)
                {
                    LogWindow.Message($"[MegaDownloader] Downloading file {node.Name}");
                    string destDir = current_downloadPath + "\\ModData\\" + MegaDownloader_TargetGetDirectory(dirstruct, node.ParentId);
                    if (!Directory.Exists(destDir))
                        Directory.CreateDirectory(destDir);

                    Console.WriteLine($"{path}/ModData/{MegaDownloader_TargetGetDirectory(dirstruct, node.ParentId)}/{node.Name}");
                    if (!File.Exists(destDir + "\\" + node.Name))
                        await client.DownloadFileAsync(node, destDir+"\\"+ node.Name);
                }
            }
            /*var doubleProgress = new Progress<double>((p) => progress?.Report((int)p));
            downloadFileLocation = GetDownloadFilePath(downloadFileRootPath, fileNameNoExtension, GetFileExtension(node.Name));*/
            DataReady.Add("url");
            LogWindow.Message($"[MegaDownloader] Downloading folder(s) done");
        }

        static string MegaDownloader_TargetGetDirectory(Dictionary<string, KeyValuePair<string, string>> dirstruct, string parent)
        {
            if (dirstruct.TryGetValue(parent, out KeyValuePair<string, string> data))
            {
                if (data.Value == "")
                    return "";


                string empty = data.Value;
                KeyValuePair<string, KeyValuePair<string, string>> last = new KeyValuePair<string, KeyValuePair<string, string>>("", new KeyValuePair<string, string>(data.Key, ""));
                while (true)
                {
                    foreach (var t in dirstruct)
                    {
                        if (t.Key.Equals(last.Value.Key))
                        {
                            last = t;
                            if (t.Key == t.Value.Key)
                                return empty;
                            empty = t.Value.Value + "/" + empty;
                            break;
                        }
                    }
                }
            }
            return "";
        }

    }
}

/*
* Copyright(c) 2023 GiR-Zippo
* Licensed under the Mozilla Public License Version 2.0. See https://github.com/GiR-Zippo/XIV-Modexplorer/blob/main/LICENSE for full license information.
*/

using OpenQA.Selenium.Chrome;
using OpenQA.Selenium;
using System;
using System.Collections.Generic;
using OpenQA.Selenium.Support.UI;
using SeleniumExtras.WaitHelpers;
using System.IO;
using HtmlAgilityPack;
using System.Linq;
using System.Net;
using System.Collections.ObjectModel;
using System.Windows;
using OpenQA.Selenium.Edge;

namespace XIVModExplorer.Scraping
{
    public partial class DynamicSite
    {
        IWebDriver _driver;
        string _url = "";

        public DynamicSite(string url)
        {
            var DriverService = EdgeDriverService.CreateDefaultService();
            DriverService.HideCommandPromptWindow = true;
            _url = url;
            _driver = new EdgeDriver(DriverService, new EdgeOptions());
            _driver.Manage().Window.Minimize();
            _driver.Navigate().GoToUrl(url);
        }

        public string DownloadGoFile(string path, bool createDir = true)
        {
            WebDriverWait wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(10));
            wait.Until(ExpectedConditions.ElementIsVisible(By.Id("filesContentTableContent")));

            var dlList = GoFile_GetDownLoads(_driver.PageSource); //get the downloads
            var cookies = _driver.Manage().Cookies.AllCookies;    //get the cookies
            _driver.Quit();     //close chrome
            _driver.Dispose();  //dispose

            string result = "";
            //do the classic download
            foreach (var link in dlList)
            {
                var data = Download(link, cookies);
                string fileName = Uri.UnescapeDataString(link).Split('/').Last();
                string extension = System.IO.Path.GetExtension(fileName);
                //create a new direcory
                if (createDir && (result == ""))
                {
                    result = fileName.Substring(0, fileName.Length - extension.Length) + "\\";
                    if (!Directory.Exists(path + "\\" + fileName.Substring(0, fileName.Length - extension.Length)))
                        Directory.CreateDirectory(path + "\\" + fileName.Substring(0, fileName.Length - extension.Length));
                }

                BinaryWriter binWriter = new BinaryWriter(File.Open(path + "\\" + result + fileName, FileMode.Create));
                binWriter.Write(data);
                binWriter.Close();
                binWriter.Dispose();
            }
            return path + "\\" + result;
        }

        private List<string> GoFile_GetDownLoads(string data)
        {
            List<string> downloadUrl = new List<string>();
            
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(data);

            var snode = htmlDoc.DocumentNode.Descendants("div").Where(n => n.Id.Equals("filesContentTable")).First();
            IEnumerable<HtmlNode> nodes = snode.Descendants("div").Where(n => n.Id.Equals("filesContentTableContent"));
            foreach (var node in nodes)
            {
                IEnumerable<HtmlNode> xNodes = node.Descendants("div");
                foreach (var xnode in xNodes)
                {
                    if (xnode.InnerHtml.Contains("class=\"contentName\""))
                    {
                        string patt = "<a href=\"";
                        var bUrl = xnode.InnerHtml.Substring(xnode.InnerHtml.IndexOf(patt) + patt.Length).Split('\"')[0];
                        Uri aUri = new Uri(_url);
                        if (bUrl.Contains(aUri.Host))
                            downloadUrl.Add(bUrl);
                    }
                }
            }           
            return downloadUrl;
        }

        private static byte[] Download(string url, ReadOnlyCollection<OpenQA.Selenium.Cookie> cookies)
        {
            var uri = new Uri(url);
            var path = url;

            byte[] data = null;
            try
            {
                var webRequest = (HttpWebRequest)WebRequest.Create(path);

                webRequest.CookieContainer = new CookieContainer();
                foreach (var cookie in cookies)
                    webRequest.CookieContainer.Add(new System.Net.Cookie(cookie.Name, cookie.Value, cookie.Path, string.IsNullOrWhiteSpace(cookie.Domain) ? uri.Host : cookie.Domain));

                var webResponse = (HttpWebResponse)webRequest.GetResponse();
                var ms = new MemoryStream();
                var responseStream = webResponse.GetResponseStream();
                responseStream.CopyTo(ms);
                data = ms.ToArray();
                responseStream.Close();
                webResponse.Close();
            }
            catch (WebException webex)
            {
                var errResp = webex.Response;
                using (var respStream = errResp.GetResponseStream())
                {
                    var reader = new StreamReader(respStream);
                    MessageBox.Show($"Error getting file from the server({webex.Status} - {webex.Message}): {reader.ReadToEnd()}.", "Error");
                }
            }
            return data;
        }

    }
}
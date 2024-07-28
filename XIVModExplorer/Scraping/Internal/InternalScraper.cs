/*
* Copyright(c) 2024 GiR-Zippo
* Licensed under the Mozilla Public License Version 2.0. See https://github.com/GiR-Zippo/XIV-Modexplorer/blob/main/LICENSE for full license information.
*/

using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System;
using System.IO;
using OpenQA.Selenium.Edge;
using OpenQA.Selenium;
using System.Collections.Generic;
using HtmlAgilityPack;
using System.Linq;
using OpenQA.Selenium.Support.UI;
using SeleniumExtras.WaitHelpers;
using XIVModExplorer.HelperWindows;

namespace XIVModExplorer.Scraping.Internal
{
    public static class InternalScraper
    {
        /// <summary>
        /// read from XIVMOD
        /// </summary>
        /// <param name="lines"></param>
        public static CollectedData ReadXIVArchive(string[] lines)
        {
            LogWindow.Message($"[Scraper] Using XIVArchive reader");
            CollectedData collectedData = new CollectedData();
            //get the name
            foreach (var line in lines)
            {
                if (line.Contains("<h1 class=\"display-5\" style=\"font-size: 2rem\""))
                {
                    var dline = line.Split('>')[1];
                    dline = dline.Split('<')[0];
                    collectedData.Modname = dline;
                }
            }

            //get the images
            foreach (var line in lines)
            {
                if (line.Contains("mod-carousel-image"))
                {
                    var dline = line.Split('\"')[3];
                    dline = dline.Split('\"')[0];
                    collectedData.Images.Add(dline);
                }
            }

            //the downloads
            foreach (var line in lines)
            {
                if (line.Contains(": [ via <a href=\"") && line.Contains(">Direct Download</a> ]") && !line.Contains("</li>"))
                {
                    string patt = ": [ via <a href=\"";
                    string url = ("https://www.xivmodarchive.com" + line.Substring(line.IndexOf(patt) + patt.Length).Split('\"')[0]);
                    url = Helper.NormalizeUrl(url);
                    collectedData.DownloadUrl.Add(url);
                }
                else if (line.Contains(": [ via <a href=\"") && line.Contains(">patreon.com</a> ]") && !line.Contains("</li>"))
                {
                    string patt = ": [ via <a href=\"";
                    string url = line.Substring(line.IndexOf(patt) + patt.Length).Split('\"')[0];
                    url = Helper.NormalizeUrl(url);
                    collectedData.ExternalSite = url.Split('?')[0];
                }
                else if (line.Contains(": [ via <a href=\"") && line.Contains("drive.google.com</a> ]") && !line.Contains("</li>"))
                {
                    string patt = ": [ via <a href=\"";
                    string url = line.Substring(line.IndexOf(patt) + patt.Length).Split('\"')[0];
                    url = Helper.NormalizeUrl(url);
                    collectedData.DownloadUrl.Add(url);
                }
                else if (line.Contains(": [ via <a href=\"") && line.Contains(">mega.nz</a> ]") && !line.Contains("</li>"))
                {
                    string patt = ": [ via <a href=\"";
                    string url = line.Substring(line.IndexOf(patt) + patt.Length).Split('\"')[0];
                    url = Helper.NormalizeUrl(url);
                    collectedData.DownloadUrl.Add(url);
                }
            }

            int i = 0;
            //get the Text
            foreach (var line in lines)
            {
                if (line.Contains("</div>") && i == -2)
                    i = 0;
                if (i == -2)
                    collectedData.Description = line;
                if (line.Contains("<div class=\"px-2\">") && i == -1)
                    i = -2;
                if (line.Contains("<p class=\"lead\">Author's Comments:</p>"))
                    i = -1;
            }
            return collectedData;
        }

        /// <summary>
        /// read from Patreon
        /// </summary>
        /// <param name="html"></param>
        public static CollectedData ReadPatreon(string html)
        {
            CollectedData collectedData = new CollectedData();
            string[] term = { "<script id=\"__NEXT_DATA__\" type=\"application/json\">" };
            var xx = html.Split(term, StringSplitOptions.RemoveEmptyEntries)[1].Replace("</script></body></html>", "");
            TextReader dr = new StringReader(xx);
            using (JsonTextReader reader = new JsonTextReader(dr))
            {
                JObject o2 = (JObject)JToken.ReadFrom(reader);
                int ix = 0;
                collectedData.Modname =  o2["props"]["pageProps"]["bootstrapEnvelope"]["pageBootstrap"]["post"]["data"]["attributes"]["title"].Value<string>().TrimEnd();
                collectedData.Description = o2["props"]["pageProps"]["bootstrapEnvelope"]["pageBootstrap"]["post"]["data"]["attributes"]["content"].Value<string>(); ;

                foreach (var d in o2["props"]["pageProps"]["bootstrapEnvelope"]["pageBootstrap"]["post"]["included"])
                {
                    var p = o2["props"]["pageProps"]["bootstrapEnvelope"]["pageBootstrap"]["post"]["included"][ix];
                    if (p["type"].Value<string>().ToString() == "attachment")
                        collectedData.DownloadUrl.Add(p["attributes"]["url"].Value<string>());
                    if (p["type"].Value<string>().ToString() == "media")
                        collectedData.Images.Add(p["attributes"]["image_urls"]["original"].Value<string>());
                    ix++;
                }
            }
            dr.Close();
            return collectedData;
        }

        /// <summary>
        /// read from KoFi, !no download support!
        /// </summary>
        /// <param name="lines"></param>
        public static CollectedData ReadKofi(string[] lines)
        {
            LogWindow.Message($"[Scraper] Using Kofi reader");
            CollectedData collectedData = new CollectedData();
            //get the text
            string text = "";
            bool content = false;
            foreach (var line in lines)
            {
                if (line.Contains("<p class=\"line-breaks kfds-c-word-wrap\" v-pre>"))
                {
                    content = true;
                }
                if (content)
                    text += line + "<p>";
                if (content && line.Contains("</p>"))
                    content = false;
            }
            collectedData.Description = text;

            //get the images
            foreach (var line in lines)
            {
                if (line.Contains("<img class=\"kfds-c-carousel-product-img"))
                {
                    string x = line.Split(new string[] { "<img class=\"kfds-c-carousel-product-img disable-dbl-tap-zoom\" src=\"" }, StringSplitOptions.RemoveEmptyEntries)[1]
                                   .Split('\"')[0];
                    collectedData.Images.Add(x);
                }
            }
            return collectedData;
        }

        /// <summary>
        /// read from aetherlink
        /// </summary>
        /// <param name="lines"></param>
        public static CollectedData ReadAetherlink(string html)
        {
            LogWindow.Message($"[Scraper] Using Aetherlink reader");
            CollectedData collectedData = new CollectedData();
            string[] term = { "<script id=\"__NEXT_DATA__\" type=\"application/json\">" };
            var xx = html.Split(term, StringSplitOptions.RemoveEmptyEntries)[1].Replace("</script></body></html>", "");
            TextReader dr = new StringReader(xx);
            using (JsonTextReader reader = new JsonTextReader(dr))
            {
                JObject o2 = (JObject)JToken.ReadFrom(reader);
                collectedData.Modname = o2["props"]["pageProps"]["mod"]["meta"]["name"]["short"].Value<string>().TrimEnd();
                collectedData.Description = o2["props"]["pageProps"]["mod"]["meta"]["description"]["html"].Value<string>();

                foreach (var d in o2["props"]["pageProps"]["downloads"])
                    collectedData.DownloadUrl.Add(d["url"].Value<string>());
                foreach (var d in o2["props"]["pageProps"]["slides"])
                    collectedData.Images.Add(d["url"].Value<string>());
            }
            dr.Close();
            return collectedData;
        }

        #region ExtDownload
        public static KeyValuePair<string[], System.Collections.ObjectModel.ReadOnlyCollection<Cookie>> GetPixelDrainDownload(string url)
        {
            LogWindow.Message($"[Scraper] Using Pixeldrain reader");
            List<string> downloadUrl = new List<string>();

            var DriverService = EdgeDriverService.CreateDefaultService();
            DriverService.HideCommandPromptWindow = true;

            IWebDriver driver = new EdgeDriver(DriverService, new EdgeOptions());
            driver.Manage().Window.Minimize();
            driver.Navigate().GoToUrl(url);

            WebDriverWait wait = new WebDriverWait(driver, TimeSpan.FromSeconds(10));
            wait.Until(ExpectedConditions.ElementIsVisible(By.Id("body")));

            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(driver.PageSource);

            downloadUrl.Add(@"https://pixeldrain.com/api/file/"+url.Split('/').Last()+"?download");
            var cookies = driver.Manage().Cookies.AllCookies;

            driver.Quit();
            driver.Dispose();
            return new KeyValuePair<string[], System.Collections.ObjectModel.ReadOnlyCollection<Cookie>>(downloadUrl.ToArray(), cookies);
        }

        public static KeyValuePair<string[], System.Collections.ObjectModel.ReadOnlyCollection<Cookie>> GetGOFileDownloads(string url)
        {
            LogWindow.Message($"[Scraper] Using GOFile reader");
            List<string> downloadUrl = new List<string>();

            var DriverService = EdgeDriverService.CreateDefaultService();
            DriverService.HideCommandPromptWindow = true;

            IWebDriver driver = new EdgeDriver(DriverService, new EdgeOptions());
            driver.Manage().Window.Minimize();
            driver.Navigate().GoToUrl(url);

            WebDriverWait wait = new WebDriverWait(driver, TimeSpan.FromSeconds(10));
            wait.Until(ExpectedConditions.ElementIsVisible(By.Id("filesContentTableContent")));

            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(driver.PageSource);

            string h = htmlDoc.ParsedText;

            var snode = htmlDoc.DocumentNode.Descendants("div").Where(n => n.Id.Equals("filesContentTable")).First();
            IEnumerable<HtmlNode> nodes = snode.Descendants("div").Where(n => n.Id.Equals("filesContentTableContent"));
            foreach (var node in nodes)
            {
                IEnumerable<HtmlNode> xNodes = node.Descendants("div");
                foreach (var xnode in xNodes)
                {
                    if (xnode.InnerHtml.Contains("class=\"dropdown-item target=\""))
                    {
                        string patt = "class=\"dropdown-item target=\" _blank\"=\"\" href=\"";
                        var bUrl = xnode.InnerHtml.Substring(xnode.InnerHtml.IndexOf(patt) + patt.Length).Split('\"')[0];
                        Uri aUri = new Uri(url);
                        if (bUrl.Contains(aUri.Host) && !downloadUrl.Contains(bUrl))
                            downloadUrl.Add(bUrl);
                    }
                }
            }

            var cookies = driver.Manage().Cookies.AllCookies;

            driver.Quit();
            driver.Dispose();
            return new KeyValuePair<string[], System.Collections.ObjectModel.ReadOnlyCollection<Cookie>>(downloadUrl.ToArray(), cookies);
        }
        #endregion
    }
}

/*
* Copyright(c) 2024 GiR-Zippo
* Licensed under the Mozilla Public License Version 2.0. See https://github.com/GiR-Zippo/XIV-Modexplorer/blob/main/LICENSE for full license information.
*/

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;

namespace XIVModExplorer.Scraping.Internal
{
    public static class Helper
    {
        public static string UserAgent { get; set; } = "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:131.0) Gecko/20100101 Firefox/131.0";
        public static string Accept { get; set; } = "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/png,image/svg+xml,*/*;q=0.8";

        public static bool IsArchive(string input) { return input.EndsWith(".ttmp") || input.EndsWith(".ttmp2") || input.EndsWith(".pmp") ||
                                                            input.EndsWith(".zip")  || input.EndsWith(".rar")   || input.EndsWith(".7z"); }

        #region dirty little helpers
        public static bool ArrayStartsWith(byte[] source, byte[] pattern)
        {
            int i = 0;
            int match = pattern.Count();
            foreach (var b in pattern)
            {
                if (source[i] == b)
                    match--;
                i++;
            }
            if (match == 0)
                return true;
            return false;
        }

        public static string ReplaceInvalidChars(string filename)
        {
            return string.Join("_", filename.Split(Path.GetInvalidFileNameChars()));
        }

        public static string NormalizeUrl(string url)
        {
            string norm = url;
            norm = norm.Replace("&#39;", "'");
            norm = norm.Replace("&amp;", "&");
            return norm;
        }

        public static bool IsSameDomain(string a, string b)
        {
            Uri aUri = new Uri(a);
            Uri bUri = new Uri(b);
            if (aUri.Host == bUri.Host)
                return true;
            return false;
        }

        public static string GetFinalRedirect(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return url;

            int maxRedirCount = 8;  // prevent infinite loops
            string newUrl = url;
            do
            {
                HttpWebRequest req = null;
                HttpWebResponse resp = null;
                try
                {
                    req = (HttpWebRequest)HttpWebRequest.Create(url);
                    req.Method = "HEAD";
                    req.AllowAutoRedirect = false;
                    resp = (HttpWebResponse)req.GetResponse();
                    switch (resp.StatusCode)
                    {
                        case HttpStatusCode.OK:
                            return newUrl;
                        case (System.Net.HttpStatusCode)308:
                            newUrl = resp.Headers["Location"];
                            break;
                        case HttpStatusCode.Redirect:
                        case HttpStatusCode.MovedPermanently:
                        case HttpStatusCode.RedirectKeepVerb:
                        case HttpStatusCode.RedirectMethod:
                            foreach (var t in resp.Headers)
                                Debug.WriteLine(t);
                            newUrl = resp.Headers["Location"];
                            if (newUrl == null)
                                return url;

                            if (newUrl.IndexOf("://", System.StringComparison.Ordinal) == -1)
                            {
                                // Doesn't have a URL Schema, meaning it's a relative or absolute URL
                                Uri u = new Uri(new Uri(url), newUrl);
                                newUrl = u.ToString();
                            }
                            break;
                        default:
                            return newUrl;
                    }
                    url = newUrl;
                }
                catch (WebException)
                {
                    // Return the last known good URL
                    return newUrl;
                }
                catch (Exception)
                {
                    return null;
                }
                finally
                {
                    if (resp != null)
                        resp.Close();
                }
            } while (maxRedirCount-- > 0);

            return newUrl;
        }
        #endregion
    }
}

/*
* Copyright(c) 2024 GiR-Zippo
* Licensed under the Mozilla Public License Version 2.0. See https://github.com/GiR-Zippo/XIV-Modexplorer/blob/main/LICENSE for full license information.
*/

using Neo.IronLua;
using Newtonsoft.Json;
using SharpCompress;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using XIVModExplorer.HelperWindows;

namespace XIVModExplorer.Scraping
{
    public class LuaScraper : IDisposable
    {
        private static string GetJsonToken(string data, string token)
        {
            dynamic jData = JsonConvert.DeserializeObject(data);
            var f = jData[token];
            return JsonConvert.SerializeObject(f);
        }

        private static LuaTable GetJsonTokenList(string data, string token)
        {
            dynamic jData = JsonConvert.DeserializeObject(data);
            var f = jData[token];
            List<string> tbl = new List<string>();
            foreach (var content in f)
                tbl.Add(JsonConvert.SerializeObject(content));

            LuaTable ltbl = new LuaTable();
            tbl.ForEach(n => ltbl.Add(n));
            return ltbl;
        }

        private static string Unescape(string data)
        {
            return Regex.Unescape(data);
        }

        private static string HtmlNormalize(string html)
        {
            return System.Web.HttpUtility.HtmlDecode(html);
        }

        private static void Print(object[] texts)
        {
            foreach (object o in texts)
                Console.Write(o);
            Console.WriteLine();
        } // proc Print

        private static LuaTable Split(string source, string delim)
        {
            //in case we've got lua syntax
            Regex regEx = new Regex(@"\[,(.?)\]");
            if (regEx.IsMatch(delim))
                delim = regEx.Replace(delim, regEx.Match(delim).Groups[1].Value);
            LuaTable tbl = new LuaTable();
            source.Split(new string[] { delim }, StringSplitOptions.RemoveEmptyEntries).ForEach(n => tbl.Add(n));
            return tbl;
        } // func Split	

        public string ModName { get; set; } = "";
        public List<string> Pictures { get; set; } = new List<string>();
        public string Content { get; set; } = "";
        public List<string> DownloadLink { get; set; } = new List<string>();
        public List<string> Replaces { get; set; } = new List<string>();
        public string ExternalSite { get; set; } = "";

        public LuaScraper()
        {
        }

        public bool Execute(string luafile, string html)
        {
            LogWindow.Message($"[Scraper] Using Lua reader");
            using (Lua lua = new Lua())
            {
                string text = File.ReadAllText(luafile);
                dynamic env = lua.CreateEnvironment<LuaGlobal>();
                env.getJsonToken = new Func<string, string, string>(GetJsonToken);
                env.getJsonTokenList = new Func<string, string, LuaTable>(GetJsonTokenList);
                env.unescape = new Func<string, string>(Unescape);
                env.normalizeHtml = new Func<string, string>(HtmlNormalize);
                env.print = new Action<object[]>(Print);
                env.split = new Func<string, string, LuaTable>(Split);
                env.HtmlData = html;
                try
                {
                    var chunk = lua.CompileChunk(text, "test.lua", new LuaCompileOptions() { DebugEngine = LuaStackTraceDebugger.Default });
                    env.dochunk(chunk);
                    ModName = env.ModName;
                    Content = env.Content;
                    ExternalSite = env.ExternalSite;
                    foreach (var x in env.Downloads as LuaTable)
                        DownloadLink.Add((string)x.Value);
                    foreach (var x in env.Images as LuaTable)
                        Pictures.Add((string)x.Value);
                    foreach (var x in env.Replaces as LuaTable)
                        Replaces.Add((string)x.Value);
                }
                catch (Exception e)
                {
                    Console.WriteLine("Expception: {0}", e.Message);
                    var d = LuaExceptionData.GetData(e); // get stack trace
                    Console.WriteLine("StackTrace: {0}", d.FormatStackTrace(0, false));
                    return false;
                }
                lua.Dispose();
                return true;
            }
        }

        public void Test(string luafile, string html)
        {
            using (Lua lua = new Lua())
            {
                Console.WriteLine("\r\nStarting Testbench");
                string text = File.ReadAllText(luafile);
                dynamic env = lua.CreateEnvironment<LuaGlobal>(); // Create a environment
                env.getJsonToken = new Func<string, string, string>(GetJsonToken);
                env.getJsonTokenList = new Func<string, string, LuaTable>(GetJsonTokenList);
                env.unescape = new Func<string, string>(Unescape);
                env.normalizeHtml = new Func<string, string>(HtmlNormalize);
                env.print = new Action<object[]>(Print);
                env.split = new Func<string, string, LuaTable>(Split);
                env.HtmlData = File.ReadAllText(html);
                try
                {
                    // compile the script with debug informations, that is needed for a complete stack trace
                    var chunk = lua.CompileChunk(text, "test.lua", new LuaCompileOptions() { DebugEngine = LuaStackTraceDebugger.Default });
                    // execute the chunk                    
                    env.dochunk(chunk);
                    Console.WriteLine("------------- ModName -------------");
                    Console.WriteLine(env.ModName);
                    Console.WriteLine("---------- Found content ----------");
                    Console.WriteLine(env.Content); // Access a variable in C#
                    Console.WriteLine("------- Found download link -------");
                    foreach (var x in env.Downloads as LuaTable)
                        Console.WriteLine((string)x.Value);
                    Console.WriteLine("------- Found picture links -------");
                    foreach (var x in env.Images as LuaTable)
                        Console.WriteLine((string)x.Value);
                    Console.WriteLine("------- Found  Replacements -------");
                    foreach (var x in env.Replaces as LuaTable)
                        Console.WriteLine((string)x.Value);
                    Console.WriteLine("------- Found External Site --------");
                        Console.WriteLine(env.ExternalSite);
                }
                catch (Exception e)
                {
                    Console.WriteLine("Expception: {0}", e.Message);
                    var d = LuaExceptionData.GetData(e); // get stack trace
                    Console.WriteLine("StackTrace: {0}", d.FormatStackTrace(0, false));
                }
                lua.Dispose();
                Console.WriteLine("Finished...");
            }
        }

        public void Dispose()
        {
            Pictures.Clear();
        }
    }
}

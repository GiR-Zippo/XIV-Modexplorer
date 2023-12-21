/*
* Copyright(c) 2023 GiR-Zippo
* Licensed under the Mozilla Public License Version 2.0. See https://github.com/GiR-Zippo/XIV-Modexplorer/blob/main/LICENSE for full license information.
*/

using Neo.IronLua;
using SharpCompress;
using System;
using System.Collections.Generic;
using System.IO;

namespace XIVModExplorer.Scraping
{
    public class LuaScraper : IDisposable
    {
        private static void Print(object[] texts)
        {
            foreach (object o in texts)
                Console.Write(o);
            Console.WriteLine();
        } // proc Print

        private static LuaTable Split(string source, string delim)
        {
            LuaTable tbl = new LuaTable();
            source.Split(new string[] { delim }, StringSplitOptions.RemoveEmptyEntries).ForEach(n => tbl.Add(n));
            return tbl;
        } // func Split	

        public List<string> Pictures { get; set; } = new List<string>();
        public string Content { get; set; } = "";
        public string DownloadLink { get; set; } = "";

        public LuaScraper()
        {
        }

        public bool Execute(string luafile, string html)
        {
            using (Lua lua = new Lua())
            {
                string text = File.ReadAllText(luafile);
                dynamic env = lua.CreateEnvironment<LuaGlobal>();
                env.print = new Action<object[]>(Print);
                env.split = new Func<string, string, LuaTable>(Split);
                env.HtmlData = html;
                try
                {
                    var chunk = lua.CompileChunk(text, "test.lua", new LuaCompileOptions() { DebugEngine = LuaStackTraceDebugger.Default });
                    env.dochunk(chunk);
                    Content = env.Content;
                    DownloadLink = env.Download;
                    foreach (var x in env.Images as LuaTable)
                        Pictures.Add((string)x.Value);
                }
                catch (Exception)
                {
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
                env.print = new Action<object[]>(Print);
                env.split = new Func<string, string, LuaTable>(Split);
                env.HtmlData = File.ReadAllText(html);
                try
                {
                    // compile the script with debug informations, that is needed for a complete stack trace
                    var chunk = lua.CompileChunk(text, "test.lua", new LuaCompileOptions() { DebugEngine = LuaStackTraceDebugger.Default });
                    // execute the chunk                    
                    env.dochunk(chunk);

                    Console.WriteLine("---------- Found content ----------");
                    Console.WriteLine(env.Content); // Access a variable in C#
                    Console.WriteLine("------- Found download link -------");
                    Console.WriteLine(env.Download);
                    Console.WriteLine("------- Found picture links -------");
                    foreach (var x in env.Images as LuaTable)
                        Console.WriteLine((string)x.Value);
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

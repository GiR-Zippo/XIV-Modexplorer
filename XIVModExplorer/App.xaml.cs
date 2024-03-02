/*
* Copyright(c) 2024 GiR-Zippo
* Licensed under the Mozilla Public License Version 2.0. See https://github.com/GiR-Zippo/XIV-Modexplorer/blob/main/LICENSE for full license information.
*/

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using XIVModExplorer.Caching;
using XIVModExplorer.Scraping;

namespace XIVModExplorer
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static string TempPath = Path.GetTempPath() + "XIVModExplorer\\";

        [DllImport("Kernel32.dll")]
        public static extern bool AttachConsole(int processId);

        private void Test(string[] arguments)
        {
            string luaFile = "";
            string sampleFile = "";
            foreach (var s in arguments)
            {
                if (s.StartsWith("lua="))
                    luaFile = s.Replace("lua=", "");
                if (s.StartsWith("sample="))
                    sampleFile = s.Replace("sample=", "");
            }
            if (luaFile == "" || sampleFile == "")
                return;

            LuaScraper l = new LuaScraper();
            l.Test(luaFile, sampleFile);
            l.Dispose();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            AttachConsole(-1);
            string[] arguments = Environment.GetCommandLineArgs();
            if (arguments.Count() >1)
            {
                Test(arguments);
                Environment.Exit(1);
            }

            Console.WriteLine("Starting...");
            ConfigureLanguage(System.Threading.Thread.CurrentThread.CurrentUICulture.ToString());
            Configuration.ReadConfig(); //read the config
            TrashRemover.Start(); //start the temp dir watchdog
            WebService.Initialize(); //inititalize the WebService

            string archivePath = Configuration.GetValue("ModArchivePath");
            if (archivePath != null)
                if (Configuration.GetBoolValue("UseDatabase"))
                    Database.Initialize(archivePath+"Database.db");
            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            base.OnExit(e);
            if (Configuration.GetBoolValue("UseDatabase"))
                Database.Instance.Dispose();
            WebService.Instance.Dispose();
            TrashRemover.Stop();
        }

        internal static void ConfigureLanguage(string langCode = null)
        {
            try
            {
                Locales.Language.Culture = new CultureInfo(langCode);
            }
            catch (Exception)
            {
                Locales.Language.Culture = CultureInfo.DefaultThreadCurrentUICulture;
            }
        }
    }
}

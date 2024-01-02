/*
* Copyright(c) 2023 GiR-Zippo
* Licensed under the Mozilla Public License Version 2.0. See https://github.com/GiR-Zippo/XIV-Modexplorer/blob/main/LICENSE for full license information.
*/

using System;
using System.IO;
using System.Timers;
using XIVModExplorer.Caching;

namespace XIVModExplorer
{
    public class TrashRemover
    {
        public static void Start()
        {
            remover = new TrashRemover();
        }

        public static void Stop()
        {
            remover.halt();

            //remove temp
            if (!Directory.Exists(App.TempPath))
                return;
            try
            {
                var d = Path.GetDirectoryName(App.TempPath);
                Directory.Delete(Path.GetDirectoryName(App.TempPath), true);
            }
            catch (IOException)
            {}
        }

        private static TrashRemover remover { get; set; } = null;

        public Timer cleanupoTimer { get; set; } = null;
        public TrashRemover()
        { 
            cleanupoTimer = new Timer(15000);
            cleanupoTimer.Elapsed += OnTimedEvent;
            cleanupoTimer.AutoReset = true;
            cleanupoTimer.Enabled = true;
        }

        public void halt()
        {
            cleanupoTimer.Elapsed -= OnTimedEvent;
            cleanupoTimer.AutoReset = false;
            cleanupoTimer.Enabled = false;
        }
        private void OnTimedEvent(object sender, ElapsedEventArgs e)
        {
            if (!Directory.Exists(App.TempPath))
                return;

            var d = Directory.GetDirectories(App.TempPath);
            foreach (var t in d)
            {
                double f = DateTimeOffset.Now.ToUnixTimeSeconds() - 300;
                double fa = Metadata.ConvertToUnixTimestamp(Directory.GetCreationTime(t));
                if (fa < f)
                {
                    try
                    {
                        Directory.Delete(t, true);
                    }
                    catch (IOException)
                    { }
                }
            }
        }
    }
}

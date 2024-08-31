/*
* Copyright(c) 2024 GiR-Zippo
* Licensed under the Mozilla Public License Version 2.0. See https://github.com/GiR-Zippo/XIV-Modexplorer/blob/main/LICENSE for full license information.
*/

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Timers;
using XIVModExplorer.Caching;

namespace XIVModExplorer
{
    /// <summary>
    /// Trash removal service
    /// </summary>
    public class TrashRemover
    {
        /// <summary>
        /// Start the timer
        /// </summary>
        public static void Start()
        {
            remover = new TrashRemover();
        }

        /// <summary>
        /// Stop the timer
        /// </summary>
        public static void Stop()
        {
            remover.halt();

            //lösch die alten dirs, welche noch in use waren
            while (RemoveDirectoryList.TryDequeue(out var directoryName))
            {
                try
                {
                    Directory.Delete(directoryName, true);
                }
                catch (IOException)
                {}
            }

            //remove temp directory
            if (!Directory.Exists(App.TempPath))
                return;
            try
            {
                Directory.Delete(Path.GetDirectoryName(App.TempPath), true);
            }
            catch (IOException)
            {}
        }

        private static TrashRemover remover { get; set; } = null;

        public static ConcurrentQueue<string> RemoveDirectoryList { get; set; } = new ConcurrentQueue<string>();

        public Timer cleanupTimer { get; set; } = null;

        /// <summary>
        /// Init everything
        /// </summary>
        public TrashRemover()
        {
            cleanupTimer = new Timer(15000);
            cleanupTimer.Elapsed += OnTimedEvent;
            cleanupTimer.AutoReset = true;
            cleanupTimer.Enabled = true;
        }

        /// <summary>
        /// Halt the remover
        /// </summary>
        public void halt()
        {
            cleanupTimer.Elapsed -= OnTimedEvent;
            cleanupTimer.AutoReset = false;
            cleanupTimer.Enabled = false;
        }

        /// <summary>
        /// On tick cleanup
        /// </summary>
        private void OnTimedEvent(object sender, ElapsedEventArgs e)
        {
            GC.Collect(); //remove at the moment when M$ doesn't suck

            //lösch die alten dirs, welche noch in use waren
            while (RemoveDirectoryList.TryDequeue(out var directoryName))
            {
                try
                {
                    Directory.Delete(directoryName, true);
                }
                catch (IOException)
                {
                    RemoveDirectoryList.Enqueue(directoryName);
                }
            }


            if (!Directory.Exists(App.TempPath))
                return;

            //delete unused temp files
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

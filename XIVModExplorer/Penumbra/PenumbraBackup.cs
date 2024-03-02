/*
* Copyright(c) 2024 GiR-Zippo
* Licensed under the Mozilla Public License Version 2.0. See https://github.com/GiR-Zippo/XIV-Modexplorer/blob/main/LICENSE for full license information.
*/

using SharpCompress.Archives;
using SharpCompress.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using XIVModExplorer.HelperWindows;


namespace XIVModExplorer.Penumbra
{
    public static class PenumbraBackup
    {
        public static void BackupMod(string ModName, string ModArchivePath, string penumbraModPath)
        {
            DateTime utcDate = DateTime.UtcNow;
            string BackupName = "Backup-" + utcDate.Year + "-" + utcDate.Month + "-" + utcDate.Day + "-" + utcDate.Hour + "." + utcDate.Minute+ "_" + ModName + ".pmp";
            string backupPath = Path.GetDirectoryName(ModArchivePath)+"\\Backup\\"+ ModName;
            Directory.CreateDirectory(backupPath);

            //archive the directory
            var carchive = ArchiveFactory.Create(ArchiveType.Zip);
            carchive.AddAllFromDirectory(penumbraModPath);
            string g = backupPath + "\\"+BackupName;
            carchive.SaveTo(g, CompressionType.Deflate);
            carchive.Dispose();

            MessageWindow.Show(Locales.Language.Word_Finished);
        }

        public static void InstallModBackup(string ModName, string ModArchivePath)
        {
            string backupPath = Path.GetDirectoryName(ModArchivePath) + "\\Backup\\" + ModName;
            if (!Directory.Exists(backupPath))
                return;

            DirectoryInfo d = new DirectoryInfo(backupPath); //Assuming Test is your Folder
            FileInfo[] Files = d.GetFiles("*.pmp");
            Dictionary<string, string> fileDict = new Dictionary<string, string>();
            foreach (var fentry in Files)
                fileDict.Add(fentry.Name, fentry.Name);

            DataGridInput li = new DataGridInput(fileDict, "Select file");
            var f = li.ShowDialog();
            if (f.Count() != 0)
            {
                PenumbraApi penumbra = new PenumbraApi();
                penumbra.Install(backupPath + "\\" + f.First());
                penumbra.Dispose();
            }
        }
    }
}
/*
* Copyright(c) 2024 GiR-Zippo
* Licensed under the Mozilla Public License Version 2.0. See https://github.com/GiR-Zippo/XIV-Modexplorer/blob/main/LICENSE for full license information.
*/

using FontAwesome.Sharp;
using Newtonsoft.Json;
using SharpCompress.Archives;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using XIVModExplorer.HelperWindows;
using XIVModExplorer.Penumbra;

namespace XIVModExplorer.Caching
{
    public static partial class Extentions
    {
        public static async Task<byte[]> ComputeHashAsync(this SHA1 sha1, Stream inputStream)
        {
            LogWindow.Message("[Metadata-Extentions] Calculating hash");
            const int BufferSize = 4096;

            sha1.Initialize();

            var buffer = new byte[BufferSize];
            var streamLength = inputStream.Length;
            while (true)
            {
                var read = await inputStream.ReadAsync(buffer, 0, BufferSize).ConfigureAwait(false);
                if (inputStream.Position == streamLength)
                {
                    sha1.TransformFinalBlock(buffer, 0, read);
                    break;
                }
                sha1.TransformBlock(buffer, 0, read, default(byte[]), default(int));
            }
            LogWindow.Message("[Metadata-Extentions] Calculating hash done");
            return sha1.Hash;
        }
    }

    /// <summary>
    /// Interaktionslogik für Metadata.xaml
    /// </summary>
    public partial class Metadata : Window
    {
        private ModEntry modentry { get; set; } = new ModEntry();
        List<byte[]> pictures { get; set; } = new List<byte[]>();
        private PenumbraApi penumbra { get; set; } = null;

        public Metadata(string filename)
        {
            if (filename == "" || !Configuration.GetBoolValue("UseDatabase"))
            {
                MessageWindow.Show(Locales.Language.Misc_Cache_Disabled, Locales.Language.Word_Error);
                this.Close();
                return;
            }
            if (!filename.Contains(Configuration.GetValue("ModArchivePath")))
            {
                this.Close();
                return;
            }
            InitializeComponent();

            //enable only if penumbra path was set and exists
            if (Configuration.GetValue("PenumbraPath") != "")
            {
                if (Directory.Exists(Configuration.GetValue("PenumbraPath")))
                    TabPenumbra.IsEnabled = true;
            }

            this.Show();
            this.Visibility = Visibility.Visible;

            modentry = Database.Instance.FindData(Configuration.GetRelativeModPath(filename));
            if (modentry == null)
                TryGetMetaDataFromArchive(filename);
            else
            {
                //If we have no hash or the mod date has changed, recalc the hash
                if (modentry.HashSha1 == null || ConvertToUnixTimestamp(modentry.ModificationDate) != ConvertToUnixTimestamp(File.GetLastWriteTime(filename)))
                    RecalculateHash(filename);
                DisplayModInfo();
            }

            penumbra = new PenumbraApi();
            penumbra.OnModRequestFinished += Instance_ModRequest;
        }

        private void Instance_ModRequest(object sender, object e)
        {
            this.Dispatcher.BeginInvoke((Action)(() =>
            {
                LeftTab.SelectedIndex = 0;
                var l = e as List<Entry>;
                Dictionary<string, string> tempDict = new Dictionary<string, string>();

                foreach (var ent in l)
                {
                    var ret = Utils.StringOperations.CalculateSimilarity(modentry.ModName, ent.Item1);
                    tempDict.Add(ent.Item1, ret.ToString());

                }
                tempDict = tempDict.OrderByDescending(obj => obj.Value).ToDictionary(obj => obj.Key, obj => obj.Key);
                DataGridInput li = new DataGridInput(tempDict, "Select mod");
                var f = li.ShowDialog();
                if (f.Count() != 0)
                {
                    modentry.PenumbraName = f[0];
                    modentry.FoundInPenumbra = true;

                    //try get the path
                    var x = Utils.Util.SearchForDirectory(Configuration.GetValue("PenumbraPath"), modentry.PenumbraName);
                    if (x.Count > 1)
                    {
                        tempDict.Clear();
                        foreach (var res in x)
                            tempDict.Add(res, res);

                        DataGridInput dirGrid = new DataGridInput(tempDict, "Select path");
                        var dirRes = dirGrid.ShowDialog();
                        if (dirRes.Count() != 0)
                            modentry.PenumbraPath = x.First();
                    }
                    else
                        modentry.PenumbraPath = x.First();
                    Database.Instance.SaveData(modentry);
                    DisplayModInfo(); //update displayed data
                }
            }));
        }

        #region WindowEvents
        private void OnTitleBarMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        private void OnCloseClick(object sender, RoutedEventArgs e)
        {
            this.penumbra.OnModRequestFinished -= Instance_ModRequest;
            this.penumbra.Dispose();
            this.Close();
        }
        #endregion

        /// <summary>
        /// Try to get the infos from the archive
        /// </summary>
        /// <param name="filename"></param>
        /// <param name="reload"></param>
        private async void TryGetMetaDataFromArchive(string filename, bool reload = false)
        {
            if (!reload)
            {
                modentry = new ModEntry();

                Save_Button.IsEnabled = false; //disable the save button
                TitleText.Text += " - Building Hash";
                SHA1Managed managed = new SHA1Managed();
                using (FileStream stream = File.OpenRead(filename))
                {
                    modentry.HashSha1 = await Task.Run(() => managed.ComputeHashAsync(stream));
                    Hash.Text = GetHashString(modentry.HashSha1);
                }
                Save_Button.IsEnabled = true;
                TitleText.Text = TitleText.Text.Replace(" - Building Hash", "");

                //Check if the hash is already in DB
                var tEntry = Database.Instance.DoesHashExists(modentry.HashSha1);
                if (tEntry != null)
                {
                    var result = MessageBox.Show(Locales.Language.Metadata_SameHashFound, Locales.Language.Word_Warning, MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (result == MessageBoxResult.Yes) //use the data in DB and update hash
                    {
                        modentry = tEntry;
                        modentry.Filename = Configuration.GetRelativeModPath(filename);
                        Database.Instance.SaveData(modentry);
                        DisplayModInfo();
                        return;
                    }
                }                  
            }

            LogWindow.Message("[Metadata] Get MetaData from archive");
            var archive = ArchiveFactory.Open(filename);
            modentry.ModName = Path.GetFileNameWithoutExtension(filename);
            modentry.Filename = Configuration.GetRelativeModPath(filename);
            modentry.CreationDate = File.GetCreationTime(filename);
            modentry.ModificationDate = File.GetLastWriteTime(filename);
            pictures.Clear();
            foreach (var entry in archive.Entries)
            {
                if (!entry.IsDirectory)
                {
                    if (entry.Key.EndsWith(".png") || entry.Key.EndsWith(".jpg") || entry.Key.EndsWith(".jpeg"))
                    {
                        MemoryStream stream = new MemoryStream();
                        entry.WriteTo(stream);
                        stream.Position = 0;
                        pictures.Add(stream.GetBuffer());
                        stream.Close();
                        stream.Dispose();
                    }
                    if (entry.Key.EndsWith("description.txt") || entry.Key.EndsWith("[1] Mod Description.txt"))
                    {
                        MemoryStream stream = new MemoryStream();
                        entry.WriteTo(stream);
                        stream.Position = 0;
                        TextReader tr = new StreamReader(stream);
                        modentry.Description = tr.ReadToEnd();
                        tr.Close();
                        stream.Close();
                        stream.Dispose();
                    }
                    else if (entry.Key.EndsWith("description.md"))
                    {
                        MemoryStream stream = new MemoryStream();
                        entry.WriteTo(stream);
                        stream.Position = 0;
                        TextReader tr = new StreamReader(stream);
                        modentry.Description = tr.ReadToEnd();
                        tr.Close();
                        stream.Close();
                        stream.Dispose();
                    }
                }
            }
            archive.Dispose();
            imageListBox.ItemsSource = pictures.Select(n => (BitmapSource)new ImageSourceConverter().ConvertFrom(n));
            DisplayModInfo();
            LogWindow.Message("[Metadata] Get MetaData from archive done");
        }

        /// <summary>
        /// Display the read data
        /// </summary>
        private void DisplayModInfo()
        {
            this.ModName.Text = modentry.ModName;
            this.Description.Text = modentry.Description;
            this.Filename.Text = modentry.Filename;
            this.ModUrl.Text = modentry.Url;
            this.PenumbraIndicator.Icon = modentry.FoundInPenumbra ? IconChar.Check : IconChar.Times;

            C_Weapon.IsChecked = (modentry.ModTypeFlag & (UInt32)Type.WEAPON) == (UInt32)Type.WEAPON;
            C_Head.IsChecked = (modentry.ModTypeFlag & (UInt32)Type.HEAD) == (UInt32)Type.HEAD;
            C_Top.IsChecked = (modentry.ModTypeFlag & (UInt32)Type.TOP) == (UInt32)Type.TOP;
            C_Bottom.IsChecked = (modentry.ModTypeFlag & (UInt32)Type.BOTTOM) == (UInt32)Type.BOTTOM;
            C_Shoe.IsChecked = (modentry.ModTypeFlag & (UInt32)Type.SHOE) == (UInt32)Type.SHOE;
            C_Ear.IsChecked = (modentry.ModTypeFlag & (UInt32)Type.EAR) == (UInt32)Type.EAR;
            C_Neck.IsChecked = (modentry.ModTypeFlag & (UInt32)Type.NECK) == (UInt32)Type.NECK;
            C_Wrist.IsChecked = (modentry.ModTypeFlag & (UInt32)Type.ARM) == (UInt32)Type.ARM;
            C_Finger.IsChecked = (modentry.ModTypeFlag & (UInt32)Type.FINGER) == (UInt32)Type.FINGER;
            C_Minion.IsChecked = (modentry.ModTypeFlag & (UInt32)Type.MINION) == (UInt32)Type.MINION;
            C_Mount.IsChecked = (modentry.ModTypeFlag & (UInt32)Type.MOUNT) == (UInt32)Type.MOUNT;
            C_ACCS.IsChecked = (modentry.ModTypeFlag & (UInt32)Type.ONACC) == (UInt32)Type.ONACC;

            C_BodyReplacement.IsChecked = (modentry.ModTypeFlag & (UInt32)Type.BREPLAC) == (UInt32)Type.BREPLAC;
            C_Hair.IsChecked = (modentry.ModTypeFlag & (UInt32)Type.HAIR) == (UInt32)Type.HAIR;
            C_Face.IsChecked = (modentry.ModTypeFlag & (UInt32)Type.FACE) == (UInt32)Type.FACE;
            C_Skin.IsChecked = (modentry.ModTypeFlag & (UInt32)Type.SKIN) == (UInt32)Type.SKIN;
            C_Housing.IsChecked = (modentry.ModTypeFlag & (UInt32)Type.HOUSING) == (UInt32)Type.HOUSING;
            C_Animation.IsChecked = (modentry.ModTypeFlag & (UInt32)Type.ANIMATION) == (UInt32)Type.ANIMATION;
            C_Vfx.IsChecked = (modentry.ModTypeFlag & (UInt32)Type.VFX) == (UInt32)Type.VFX;

            if (C_ACCS.IsChecked.Value)
            {
                CA_Weapon.IsChecked = (modentry.AccModTypeFlag & (UInt32)Type.WEAPON) == (UInt32)Type.WEAPON;
                CA_Head.IsChecked = (modentry.AccModTypeFlag & (UInt32)Type.HEAD) == (UInt32)Type.HEAD;
                CA_Top.IsChecked = (modentry.AccModTypeFlag & (UInt32)Type.TOP) == (UInt32)Type.TOP;
                CA_Bottom.IsChecked = (modentry.AccModTypeFlag & (UInt32)Type.BOTTOM) == (UInt32)Type.BOTTOM;
                CA_Shoe.IsChecked = (modentry.AccModTypeFlag & (UInt32)Type.SHOE) == (UInt32)Type.SHOE;
                CA_Ear.IsChecked = (modentry.AccModTypeFlag & (UInt32)Type.EAR) == (UInt32)Type.EAR;
                CA_Neck.IsChecked = (modentry.AccModTypeFlag & (UInt32)Type.NECK) == (UInt32)Type.NECK;
                CA_Wrist.IsChecked = (modentry.AccModTypeFlag & (UInt32)Type.ARM) == (UInt32)Type.ARM;
                CA_Finger.IsChecked = (modentry.AccModTypeFlag & (UInt32)Type.FINGER) == (UInt32)Type.FINGER;
            }

            if (modentry.HashSha1 != null)
                Hash.Text = GetHashString(modentry.HashSha1);

            if (modentry.PenumbraName != null)
                Pen_ModName.Text = modentry.PenumbraName;
            if (modentry.PenumbraPath != null)
                if (modentry.PenumbraPath.Length > 1 && Directory.Exists(Configuration.GetValue("PenumbraPath") + "\\" + modentry.PenumbraPath))
                    Pen_ModPath.Text = Configuration.GetValue("PenumbraPath")+"\\"+modentry.PenumbraPath;
        }

        /// <summary>
        /// Save to db
        /// </summary>
        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (modentry.picture == null)
            {
                MessageWindow.Show(Locales.Language.Msg_No_Pic_Selected, Locales.Language.Word_Error);
                return;
            }

            modentry.ModName = ModName.Text;
            modentry.Description = Description.Text;

            modentry.ModTypeFlag = (UInt32)((C_Weapon.IsChecked.Value ? Type.WEAPON : Type.NONE) |
                             (C_Head.IsChecked.Value ? Type.HEAD : Type.NONE) |
                             (C_Top.IsChecked.Value ? Type.TOP : Type.NONE) |
                             (C_Hands.IsChecked.Value ? Type.HANDS : Type.NONE) |
                             (C_Bottom.IsChecked.Value ? Type.BOTTOM : Type.NONE) |
                             (C_Shoe.IsChecked.Value ? Type.SHOE : Type.NONE) |
                             (C_Ear.IsChecked.Value ? Type.EAR : Type.NONE) |
                             (C_Neck.IsChecked.Value ? Type.NECK : Type.NONE) |
                             (C_Wrist.IsChecked.Value ? Type.ARM : Type.NONE) |
                             (C_Finger.IsChecked.Value ? Type.FINGER : Type.NONE) |
                             (C_Minion.IsChecked.Value ? Type.MINION : Type.NONE) |
                             (C_Mount.IsChecked.Value ? Type.MOUNT : Type.NONE) |
                             (C_ACCS.IsChecked.Value ? Type.ONACC : Type.NONE) |
                             (C_BodyReplacement.IsChecked.Value ? Type.BREPLAC : Type.NONE) |
                             (C_Hair.IsChecked.Value ? Type.HAIR : Type.NONE) |
                             (C_Face.IsChecked.Value ? Type.FACE : Type.NONE) |
                             (C_Skin.IsChecked.Value ? Type.SKIN : Type.NONE) |
                             (C_Housing.IsChecked.Value ? Type.HOUSING : Type.NONE) |
                             (C_Animation.IsChecked.Value ? Type.ANIMATION : Type.NONE) |
                             (C_Vfx.IsChecked.Value ? Type.VFX : Type.NONE));

            if (C_ACCS.IsChecked.Value)
            {
                modentry.AccModTypeFlag = (UInt32)((CA_Weapon.IsChecked.Value ? Type.WEAPON : Type.NONE) |
                                 (CA_Head.IsChecked.Value ? Type.HEAD : Type.NONE) |
                                 (CA_Top.IsChecked.Value ? Type.TOP : Type.NONE) |
                                 (CA_Hands.IsChecked.Value ? Type.HANDS : Type.NONE) |
                                 (CA_Bottom.IsChecked.Value ? Type.BOTTOM : Type.NONE) |
                                 (CA_Shoe.IsChecked.Value ? Type.SHOE : Type.NONE) |
                                 (CA_Ear.IsChecked.Value ? Type.EAR : Type.NONE) |
                                 (CA_Neck.IsChecked.Value ? Type.NECK : Type.NONE) |
                                 (CA_Wrist.IsChecked.Value ? Type.ARM : Type.NONE) |
                                 (CA_Finger.IsChecked.Value ? Type.FINGER : Type.NONE));
            }

            modentry.Url = ModUrl.Text;
            Database.Instance.SaveData(modentry);
        }

        /// <summary>
        /// Reread the whole archive
        /// </summary>
        private void Reread_Click(object sender, RoutedEventArgs e)
        {
            if (modentry == null)
                return;
            TryGetMetaDataFromArchive(Configuration.GetAbsoluteModPath(modentry.Filename), true);
        }

        /// <summary>
        /// Trigger the penumbra path jobby
        /// </summary>
        private void PenumbraButton_Click(object sender, RoutedEventArgs e)
        {
            penumbra.GetMods();
        }

        /// <summary>
        /// Triggers the penumbra backup
        /// </summary>
        private void PenumbraBackupButton_Click(object sender, RoutedEventArgs e)
        {
            Task.Run(() =>
            {
                if (modentry.PenumbraPath != null)
                    if (modentry.PenumbraPath.Length > 1 && Directory.Exists(Configuration.GetValue("PenumbraPath") + "\\" + modentry.PenumbraPath))
                        PenumbraBackup.BackupMod(modentry.PenumbraName, Configuration.GetAbsoluteModPath(modentry.Filename), Configuration.GetValue("PenumbraPath") + "\\" + modentry.PenumbraPath);
            });
        }

        /// <summary>
        /// Triggers the backup installation
        /// </summary>
        private void PenumbraBackupInstallButton_Click(object sender, RoutedEventArgs e)
        {
            PenumbraBackup.InstallModBackup(modentry.PenumbraName, Configuration.GetAbsoluteModPath(modentry.Filename));
            DisplayModInfo();
        }

        /// <summary>
        /// Set the preview image
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Image_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            var x = (sender as System.Windows.Controls.Image).Source as BitmapSource;
            var jpegEncoder = new JpegBitmapEncoder();
            jpegEncoder.Frames.Add(BitmapFrame.Create(x));
            using (var fx = new MemoryStream())
            {
                jpegEncoder.Save(fx);
                modentry.picture = fx.GetBuffer();
            }
        }

        /// <summary>
        /// Export info as Json
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new FolderPicker();
            dlg.InputPath = @"C:\";
            if (dlg.ShowDialog(this) == true)
            {
                string fname = Path.GetFileNameWithoutExtension(modentry.Filename);
                string output = JsonConvert.SerializeObject(modentry);
                File.WriteAllText(dlg.ResultPath+"\\"+ fname+".json", output);
            }
        }

        /// <summary>
        /// Calc the Sha1 from the file and save the col
        /// </summary>
        /// <param name="filename"></param>
        private async void RecalculateHash(string filename, bool save = true)
        {
            Save_Button.IsEnabled = false; //disable the save button
            TitleText.Text += " - Rebuilding Hash";
            SHA1Managed managed = new SHA1Managed();
            using (FileStream stream = File.OpenRead(filename))
            {
                modentry.HashSha1 = await Task.Run(() => managed.ComputeHashAsync(stream));
                modentry.ModificationDate = File.GetLastWriteTime(filename);
                if (save)
                    Database.Instance.SaveData(modentry);

                Hash.Text = GetHashString(modentry.HashSha1);
            }
            Save_Button.IsEnabled = true;
            TitleText.Text = TitleText.Text.Replace(" - Rebuilding Hash", "");
        }

        /// <summary>
        /// Gets the Hash as string
        /// </summary>
        /// <param name="hash"></param>
        private string GetHashString(byte[] hash)
        {
            StringBuilder formatted = new StringBuilder(2 * hash.Length);
            foreach (byte b in hash)
                formatted.AppendFormat("{0:X2}", b);
            return formatted.ToString();
        }

        public static double ConvertToUnixTimestamp(DateTime date)
        {
            DateTime origin = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            TimeSpan diff = date.ToUniversalTime() - origin;
            return Math.Floor(diff.TotalSeconds);
        }
    }
}

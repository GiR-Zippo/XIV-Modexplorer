/*
* Copyright(c) 2023 GiR-Zippo
* Licensed under the Mozilla Public License Version 2.0. See https://github.com/GiR-Zippo/XIV-Modexplorer/blob/main/LICENSE for full license information.
*/

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

namespace XIVModExplorer.Caching
{
    public static partial class Extentions
    {
        public static async Task<byte[]> ComputeHashAsync(this SHA1 sha1, Stream inputStream)
        {
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

        public Metadata(string filename)
        {
            if (filename == "" || !Configuration.GetBoolValue("UseDatabase"))
            {
                this.Close();
                return;
            }
            if (!filename.Contains(Configuration.GetValue("ModArchivePath")))
            {
                this.Close();
                return;
            }
            InitializeComponent();

            this.Show();
            this.Visibility = Visibility.Visible;

            modentry = Database.Instance.FindData(Configuration.GetRelativeModPath(filename));
            if (modentry ==null)
                TryGetMetaDataFromArchive(filename);
            if (modentry.HashSha1 == null)
                RecalculateHash(filename);

            DisplayModInfo();
        }

        #region WindowEvents
        private void OnTitleBarMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }

        private void OnCloseClick(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
        #endregion

        private void TryGetMetaDataFromArchive(string archivename, bool reload = false)
        {
            if (!reload)
                modentry = new ModEntry();

            var archive = ArchiveFactory.Open(archivename);
            modentry.ModName = Path.GetFileNameWithoutExtension(archivename);
            modentry.Filename = Configuration.GetRelativeModPath(archivename);
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
        }

        private void DisplayModInfo()
        {
            this.ModName.Text = modentry.ModName;
            this.Description.Text = modentry.Description;
            this.Filename.Text = modentry.Filename;
            this.ModUrl.Text = modentry.Url;
            C_Weapon.IsChecked = (modentry.ModTypeFlag & (UInt16)Type.WEAPON) == (UInt16)Type.WEAPON;
            C_Head.IsChecked = (modentry.ModTypeFlag & (UInt16)Type.HEAD) == (UInt16)Type.HEAD;
            C_Top.IsChecked = (modentry.ModTypeFlag & (UInt16)Type.TOP) == (UInt16)Type.TOP;
            C_Bottom.IsChecked = (modentry.ModTypeFlag & (UInt16)Type.BOTTOM) == (UInt16)Type.BOTTOM;
            C_Shoe.IsChecked = (modentry.ModTypeFlag & (UInt16)Type.SHOE) == (UInt16)Type.SHOE;
            C_Ear.IsChecked = (modentry.ModTypeFlag & (UInt16)Type.EAR) == (UInt16)Type.EAR;
            C_Neck.IsChecked = (modentry.ModTypeFlag & (UInt16)Type.NECK) == (UInt16)Type.NECK;
            C_Wrist.IsChecked = (modentry.ModTypeFlag & (UInt16)Type.ARM) == (UInt16)Type.ARM;
            C_Finger.IsChecked = (modentry.ModTypeFlag & (UInt16)Type.FINGER) == (UInt16)Type.FINGER;

            if (modentry.HashSha1 != null)
                Hash.Text = GetHashString(modentry.HashSha1);
        }

        /// <summary>
        /// Save to db
        /// </summary>
        private void Save_Click(object sender, RoutedEventArgs e)
        {
            modentry.ModName = ModName.Text;
            modentry.Description = Description.Text;

            modentry.ModTypeFlag = (UInt16)((C_Weapon.IsChecked.Value ? Type.WEAPON : Type.NONE) |
                             (C_Head.IsChecked.Value ? Type.HEAD : Type.NONE) |
                             (C_Top.IsChecked.Value ? Type.TOP : Type.NONE) |
                             (C_Hands.IsChecked.Value ? Type.HANDS : Type.NONE) |
                             (C_Bottom.IsChecked.Value ? Type.BOTTOM : Type.NONE) |
                             (C_Shoe.IsChecked.Value ? Type.SHOE : Type.NONE) |
                             (C_Ear.IsChecked.Value ? Type.EAR : Type.NONE) |
                             (C_Neck.IsChecked.Value ? Type.NECK : Type.NONE) |
                             (C_Wrist.IsChecked.Value ? Type.ARM : Type.NONE) |
                             (C_Finger.IsChecked.Value ? Type.FINGER : Type.NONE));
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
        /// Set the preview image
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Image_PreviewMouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
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
        /// Calc the Sha1 from the file and save the col
        /// </summary>
        /// <param name="filename"></param>
        private async void RecalculateHash(string filename)
        {
            Save_Button.IsEnabled = false; //disable the save button
            string x = TitleText.Text;
            TitleText.Text += " - Rebuilding Hash";
            SHA1Managed managed = new SHA1Managed();
            using (FileStream stream = File.OpenRead(filename))
            {
                modentry.HashSha1 = await Task.Run(() => managed.ComputeHashAsync(stream));
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
    }
}

/*
* Copyright(c) 2023 GiR-Zippo
* Licensed under the Mozilla Public License Version 2.0. See https://github.com/GiR-Zippo/XIV-Modexplorer/blob/main/LICENSE for full license information.
*/

using SharpCompress.Archives;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace XIVModExplorer.Database
{
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

            this.Show();
            this.Visibility = Visibility.Visible;

            InitializeComponent();

            modentry = Database.Instance.FindData(Configuration.GetRelativeModPath(filename));
            if (modentry ==null)
                TryGetMetaDataFromArchive(filename);
            DisplayModInfo();
        }

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
        }

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

        private void Reread_Click(object sender, RoutedEventArgs e)
        {
            if (modentry == null)
                return;
            TryGetMetaDataFromArchive(Configuration.GetAbsoluteModPath(modentry.Filename), true);
        }

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
    }
}

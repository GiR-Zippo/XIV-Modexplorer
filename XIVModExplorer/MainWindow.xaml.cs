/*
* Copyright(c) 2023 GiR-Zippo
* Licensed under the Mozilla Public License Version 2.0. See https://github.com/GiR-Zippo/XIV-Modexplorer/blob/main/LICENSE for full license information.
*/

using SharpCompress.Archives;
using SharpCompress.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using XIVModExplorer;
using XIVModExplorer.Database;
using XIVModExplorer.Scraping;

namespace TreeViewFileExplorer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public string selected_dir = "";
        public string current_archive = "";
        public string current_preview = "";
        public string right_clicked_item = "";
        public List<BitmapFrame> pictures = new List<BitmapFrame>();
        public Timer slideTimer = null;
        public int SliderIndex { get; set; } = 0;

        public MainWindow()
        {
            InitializeComponent();

            if (Configuration.GetValue("ModArchivePath") != null)
                FileTree.UpdateTreeView(Configuration.GetValue("ModArchivePath"));

            if (Configuration.GetBoolValue("UseDatabase"))
                UseDatabase.IsChecked = true;
            FileTree.OnFileClicked += FileSelected;
            FileTree.OnRightClicked += RightSelected;
            FileTree.OnDirClicked += DirSelected;
            FileTree.OnArchiveClicked += ArchivePreview;
            //URL_ImgFind.DownloadMod("https://www.glamourdresser.com/mods/shiromaru-with-lala-chew-toy", "D:\\tmp\\tmp");
            //URL_ImgFind.ScrapeURLforData("https://www.nexusmods.com/finalfantasy14/mods/2083", "D:\\tmp\\tmp");
        }

        #region Events
        private void RightSelected(object sender, string e)
        {
            this.Dispatcher.BeginInvoke(new Action(() =>
            {
                right_clicked_item = e;
            }));
        }

        private void DirSelected(object sender, string e)
        {
            this.Dispatcher.BeginInvoke(new Action(() =>
            {
                selected_dir = e;
                SelectedDir.Content = selected_dir;
            }));
        }

        private void OnTimedEvent(Object source, ElapsedEventArgs e)
        {
            SliderIndex++;
            if (SliderIndex >= pictures.Count())
                SliderIndex = 0;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                Img.Source = null;
                Img.Source = pictures[SliderIndex];
            }));
        }

        private void FileSelected(object sender, string e)
        {
            this.Dispatcher.BeginInvoke(new Action(() =>
            {
                OpenArchive(e);
            }));
            return;
        }

        private void ArchivePreview(object sender, string e)
        {
            this.Dispatcher.BeginInvoke(new Action(() =>
            {
                PreviewArchive(e);
            }));
            return;
        }
        #endregion

        /// <summary>
        /// try to get the cached data
        /// </summary>
        /// <param name="archiveName"></param>
        public void PreviewArchive(string archiveName)
        {
            if (!Configuration.GetBoolValue("UseDatabase"))
                return;

            if (current_preview == archiveName)
                return;

            current_preview = archiveName;
            current_archive = "";
            ModEntry me = Database.Instance.FindData(Configuration.GetRelativeModPath(archiveName));

            if (slideTimer != null)
            {
                slideTimer.Elapsed -= OnTimedEvent;
                slideTimer.AutoReset = false;
                slideTimer.Enabled = false;
                SliderIndex = 0;
            }

            pictures.Clear();
            Description.Text = "";
            ModName.Content = "";
            ModUrl.Content = "";
            NormalTextScroll.Visibility = Visibility.Hidden;
            MarkdownScroll.Visibility = Visibility.Hidden;
            Img.Source = null;
            if (me == null)
                return;

            ModName.Content = me.ModName;
            ModUrl.Content = me.Url;
            MarkdownScroll.Visibility = Visibility.Visible;
            MarkdownContent.Text = me.Description;
            MemoryStream str = new MemoryStream(me.picture);
            JpegBitmapDecoder jpegDecoder = new JpegBitmapDecoder(str, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.None);

            Img.Source = jpegDecoder.Frames[0];
        }

        /// <summary>
        /// Open an archive and display the data
        /// </summary>
        /// <param name="archiveName"></param>
        public void OpenArchive(string archiveName)
        {
            if (current_archive == archiveName)
                return;
            current_archive = archiveName;

            if (slideTimer != null)
            {
                slideTimer.Elapsed -= OnTimedEvent;
                slideTimer.AutoReset = false;
                slideTimer.Enabled = false;
                SliderIndex = 0;
            }
            pictures.Clear();
            Description.Text = "";
            NormalTextScroll.Visibility = Visibility.Hidden;
            MarkdownScroll.Visibility = Visibility.Hidden;
            var archive = ArchiveFactory.Open(archiveName);
            ModName.Content = Path.GetFileNameWithoutExtension(archiveName);
            foreach (var entry in archive.Entries)
            {
                if (!entry.IsDirectory)
                {
                    if (entry.Key.EndsWith(".png") || entry.Key.EndsWith(".jpg") || entry.Key.EndsWith(".jpeg"))
                    {
                        MemoryStream stream = new MemoryStream();
                        entry.WriteTo(stream);
                        stream.Position = 0;
                        BitmapFrame source = BitmapFrame.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                        pictures.Add(source);
                        stream.Close();
                        stream.Dispose();
                    }
                    if (entry.Key.EndsWith("description.txt") || entry.Key.EndsWith("[1] Mod Description.txt"))
                    {
                        MemoryStream stream = new MemoryStream();
                        entry.WriteTo(stream);
                        stream.Position = 0;
                        TextReader tr = new StreamReader(stream);
                        NormalTextScroll.Visibility = Visibility.Visible;
                        Description.Text = tr.ReadToEnd();
                        tr.Close();
                        stream.Close();
                    }
                    else if (entry.Key.EndsWith("description.md"))
                    {
                        MemoryStream stream = new MemoryStream();
                        entry.WriteTo(stream);
                        stream.Position = 0;
                        TextReader tr = new StreamReader(stream);
                        MarkdownScroll.Visibility = Visibility.Visible;
                        MarkdownContent.Text = tr.ReadToEnd();
                        tr.Close();
                        stream.Close();
                    }
                    Console.WriteLine(entry.Key);
                }
            }
            if (pictures.Count() != 0)
            {
                Img.Source = pictures.First();
                slideTimer = new Timer(5000);
                slideTimer.Elapsed += OnTimedEvent;
                slideTimer.AutoReset = true;
                slideTimer.Enabled = true;
            }
            archive.Dispose();
        }

        private void BackupPenumbra(object sender, RoutedEventArgs e)
        {

        }

        private void SelectDatabase_Click(object sender, RoutedEventArgs e)
        {
            Database.Initialize("I:\\XIVModsArchive\\Database.db");
        }

        private void EditMetadata_Click(object sender, RoutedEventArgs e)
        {
            Metadata mdata = new Metadata(right_clicked_item);
        }


        #region Picture controls
        private void Prev_Click(object sender, RoutedEventArgs e)
        {
            SliderIndex--;
            if (SliderIndex < 0)
                SliderIndex = pictures.Count() - 1;

            Img.Source = null;
            Img.Source = pictures[SliderIndex];
        }

        private void Next_Click(object sender, RoutedEventArgs e)
        {
            if (pictures.Count() == 0)
                return;
            SliderIndex++;
            if (SliderIndex >= pictures.Count())
                SliderIndex = 0;

            Img.Source = null;
            Img.Source = pictures[SliderIndex];
        }
        #endregion

        #region Mainmenu
        /// <summary>
        /// Compress a folder as an archive
        /// </summary>
        private void File_Compress_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new FolderPicker();
            dlg.InputPath = @"C:\";
            if (dlg.ShowDialog(this) == true)
            {
                var dirName = new DirectoryInfo(dlg.ResultName).Name;
                string result = new DirectoryInfo(dlg.ResultName).Parent.FullName;
                Debug.WriteLine(dirName);
                var archive = ArchiveFactory.Create(ArchiveType.Zip);
                archive.AddAllFromDirectory(dlg.ResultName);

                string g = selected_dir + "\\" + dirName + ".zip";
                archive.SaveTo(g, CompressionType.Deflate);
                archive.Dispose();
                FileTree.UpdateTreeView(selected_dir + "\\");
                MessageBox.Show("Finished");
            }

        }

        /// <summary>
        /// Scrape the mod data
        /// </summary>
        private void File_Scrape_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new FolderPicker();
            dlg.InputPath = @"C:\";
            if (dlg.ShowDialog(this) == true)
            {
                InputBox input = new InputBox("URL");
                string data = input.ShowDialog();
                if (data != "")
                {
                    if (URL_ImgFind.ScrapeURLforData(data, dlg.ResultName))
                        MessageBox.Show("Finished");
                    else
                        MessageBox.Show("Error while fetching data.", "ERROR");
                }
            }
        }

        /// <summary>
        /// Download a mod from a website
        /// </summary>
        private void DownloadMenu_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new FolderPicker();
            dlg.InputPath = @"C:\";
            if (dlg.ShowDialog(this) == true)
            {
                InputBox input = new InputBox("URL");
                string data = input.ShowDialog();
                if (data != "")
                {
                    if (URL_ImgFind.DownloadMod(data, dlg.ResultName, DLArchive.IsChecked.Value, DLRDir.IsChecked.Value))
                    {
                        FileTree.UpdateTreeView(selected_dir + "\\");
                        MessageBox.Show("Finished");
                    }
                    else
                        MessageBox.Show("Error while fetching data.", "ERROR");
                }
            }
        }

        /// <summary>
        /// Backup a penumbra mod to a pmp file
        /// </summary>
        private void BackupPenumbraModMenu_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new FolderPicker();
            dlg.InputPath = Configuration.GetValue("PenumbraPath");
            if (dlg.ShowDialog(this) == true)
            {
                var dirName = new DirectoryInfo(dlg.ResultName).Name;
                string result = new DirectoryInfo(dlg.ResultName).Parent.FullName;
                var archive = ArchiveFactory.Create(ArchiveType.Zip);
                archive.AddAllFromDirectory(dlg.ResultName);

                string g = selected_dir + "\\" + dirName + ".pmp";
                archive.SaveTo(g, CompressionType.Deflate);
                archive.Dispose();
                FileTree.UpdateTreeView(selected_dir + "\\");
                MessageBox.Show("Finished");
            }
        }

        /// <summary>
        /// Set the penumbra folder
        /// </summary>
        private void SetPenumbraMenu_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new FolderPicker();
            dlg.InputPath = "C:\\";
            if (dlg.ShowDialog(this) == true)
                Configuration.SetValue("PenumbraPath", dlg.ResultPath);
        }

        /// <summary>
        /// Set the mod archive folder
        /// </summary>
        private void SetModArchiveMenu_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new FolderPicker();
            dlg.InputPath = "C:\\";
            if (dlg.ShowDialog(this) == true)
            {
                Configuration.SetValue("ModArchivePath", dlg.ResultPath + "\\");
                FileTree.UpdateTreeView(Configuration.GetValue("ModArchivePath"));
                Database.Initialize(Configuration.GetValue("ModArchivePath") + "Database.db");
            }
        }
        #endregion

        #region ContextMenu
        /// <summary>
        /// Coompress the selected folder to an archive
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ContextMenu_Compress_Click(object sender, RoutedEventArgs e)
        {
            var dirName = new DirectoryInfo(selected_dir).Name;
            string result = new DirectoryInfo(selected_dir).Parent.FullName;
            var archive = ArchiveFactory.Create(ArchiveType.Zip);
            archive.AddAllFromDirectory(selected_dir);

            string g = result + "\\" + dirName + ".zip";
            archive.SaveTo(g, CompressionType.Deflate);
            FileTree.UpdateTreeView(selected_dir + "\\");
            MessageBox.Show("Finished");
        }

        /// <summary>
        /// Scrape data and save them to the selected folder
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ContextMenu_Scrape_Click(object sender, RoutedEventArgs e)
        {
            InputBox input = new InputBox("URL");
            string data = input.ShowDialog();
            if (data != "")
            {
                if (URL_ImgFind.ScrapeURLforData(data, selected_dir))
                    MessageBox.Show("Finished");
                else
                    MessageBox.Show("Error while fetching data.", "ERROR");
            }
        }

        /// <summary>
        /// Scrape data and compress the selected folder
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ContextMenu_ScrapeCompress_Click(object sender, RoutedEventArgs e)
        {
            InputBox input = new InputBox("URL");
            string data = input.ShowDialog();
            if (data != "")
            {
                if (URL_ImgFind.ScrapeURLforData(data, selected_dir))
                {
                    var dirName = new DirectoryInfo(selected_dir).Name;
                    string result = new DirectoryInfo(selected_dir).Parent.FullName;
                    var archive = ArchiveFactory.Create(ArchiveType.Zip);
                    archive.AddAllFromDirectory(selected_dir);

                    string g = result + "\\" + dirName + ".zip";
                    archive.SaveTo(g, CompressionType.Deflate);
                    FileTree.UpdateTreeView(selected_dir + "\\");
                    MessageBox.Show("Finished");
                }
                else
                    MessageBox.Show("Error while fetching data.", "ERROR");
            }
        }

        /// <summary>
        /// Download a mod the selected folder
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ContextMenu_Download_Click(object sender, RoutedEventArgs e)
        {
            InputBox input = new InputBox("URL");
            string data = input.ShowDialog();
            if (data != "")
            {
                if (URL_ImgFind.DownloadMod(data, selected_dir, DLArchive.IsChecked.Value, DLRDir.IsChecked.Value))
                {
                    FileTree.UpdateTreeView(selected_dir + "\\");
                    MessageBox.Show("Finished");
                }
                else
                    MessageBox.Show("Error while fetching data.", "ERROR");
            }

        }

        /// <summary>
        /// Try to delete the selected folder
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ContextMenu_Delete_Click(object sender, RoutedEventArgs e)
        {
            if (!Directory.Exists(selected_dir))
                return;
            var Result = MessageBox.Show(selected_dir, "Delete folder?", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (Result == MessageBoxResult.Yes)
            {
                //delete the dir
                try
                {
                    Directory.Delete(selected_dir, true);
                    FileTree.UpdateTreeView(Directory.GetParent(selected_dir) + "\\");
                    Directory.GetParent(selected_dir);
                }
                catch (IOException)
                {
                }
            }
        }

        #endregion

        private void OnTitleBarMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                Application.Current.MainWindow.DragMove();
            }
        }

        private void OnCloseClick(object sender, RoutedEventArgs e)
        {
            Application.Current.MainWindow.Close();
        }

        private void ModUrl_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            string url = (string)ModUrl.Content;
            System.Diagnostics.Process.Start(url);
        }

        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            var c = sender as CheckBox;
            Configuration.SetValue("UseDatabase", c.IsChecked.Value.ToString());
        }
    }

    public class InputBox
    {
        Window Box = new Window();//window for the inputbox
        FontFamily font = new FontFamily("Avenir");//font for the whole inputbox
        int FontSize = 14;//fontsize for the input
        StackPanel sp1 = new StackPanel();// items container
        string title = "Dica s.l.";//title as heading
        string boxcontent;//title
        string defaulttext = "";//default textbox content
        string errormessage = "Datos no válidos";//error messagebox content
        string errortitle = "Error";//error messagebox heading title
        string okbuttontext = "OK";//Ok button content
        string CancelButtonText = "Cancelar";
        Brush BoxBackgroundColor = Brushes.WhiteSmoke;// Window Background
        Brush InputBackgroundColor = Brushes.Ivory;// Textbox Background
        bool clickedOk = false;
        TextBox input = new TextBox();
        Button ok = new Button();
        Button cancel = new Button();
        bool inputreset = false;


        public InputBox(string content)
        {
            try
            {
                boxcontent = content;
            }
            catch { boxcontent = "Error!"; }
            windowdef();
        }

        public InputBox(string content, string Htitle, string DefaultText)
        {
            try
            {
                boxcontent = content;
            }
            catch { boxcontent = "Error!"; }
            try
            {
                title = Htitle;
            }
            catch
            {
                title = "Error!";
            }
            try
            {
                defaulttext = DefaultText;
            }
            catch
            {
                DefaultText = "Error!";
            }
            windowdef();
        }

        public InputBox(string content, string Htitle, string Font, int Fontsize)
        {
            try
            {
                boxcontent = content;
            }
            catch { boxcontent = "Error!"; }
            try
            {
                font = new FontFamily(Font);
            }
            catch { font = new FontFamily("Tahoma"); }
            try
            {
                title = Htitle;
            }
            catch
            {
                title = "Error!";
            }
            if (Fontsize >= 1)
                FontSize = Fontsize;
            windowdef();
        }

        private void windowdef()// window building - check only for window size
        {
            Box.Height = 100;// Box Height
            Box.Width = 450;// Box Width
            Box.Background = BoxBackgroundColor;
            Box.Title = title;
            Box.Content = sp1;
            Box.Closing += Box_Closing;
            Box.WindowStyle = WindowStyle.None;
            Box.WindowStartupLocation = WindowStartupLocation.CenterScreen;

            TextBlock content = new TextBlock();
            content.TextWrapping = TextWrapping.Wrap;
            content.Background = null;
            content.HorizontalAlignment = HorizontalAlignment.Center;
            content.Text = boxcontent;
            content.FontFamily = font;
            content.FontSize = FontSize;
            sp1.Children.Add(content);

            input.Background = InputBackgroundColor;
            input.FontFamily = font;
            input.FontSize = FontSize;
            input.HorizontalAlignment = HorizontalAlignment.Center;
            input.Text = defaulttext;
            input.MinWidth = 200;
            input.MouseEnter += input_MouseDown;
            input.KeyDown += input_KeyDown;

            sp1.Children.Add(input);

            ok.Width = 70;
            ok.Height = 30;
            ok.Click += ok_Click;
            ok.Content = okbuttontext;

            cancel.Width = 70;
            cancel.Height = 30;
            cancel.Click += cancel_Click;
            cancel.Content = CancelButtonText;

            WrapPanel gboxContent = new WrapPanel();
            gboxContent.HorizontalAlignment = HorizontalAlignment.Center;

            sp1.Children.Add(gboxContent);
            gboxContent.Children.Add(ok);
            gboxContent.Children.Add(cancel);

            input.Focus();
        }

        void Box_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            //validation
        }

        private void input_MouseDown(object sender, MouseEventArgs e)
        {
            if ((sender as TextBox).Text == defaulttext && inputreset == false)
            {
                (sender as TextBox).Text = null;
                inputreset = true;
            }
        }

        private void input_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && clickedOk == false)
            {
                e.Handled = true;
                ok_Click(input, null);
            }

            if (e.Key == Key.Escape)
            {
                cancel_Click(input, null);
            }
        }

        void ok_Click(object sender, RoutedEventArgs e)
        {
            clickedOk = true;
            if (input.Text == defaulttext || input.Text == "")
                MessageBox.Show(errormessage, errortitle, MessageBoxButton.OK, MessageBoxImage.Error);
            else
            {
                Box.Close();
            }
            clickedOk = false;
        }

        void cancel_Click(object sender, RoutedEventArgs e)
        {
            Box.Close();
        }

        public string ShowDialog()
        {
            Box.ShowDialog();
            return input.Text;
        }
    }
}

/*
* Copyright(c) 2024 GiR-Zippo
* Licensed under the Mozilla Public License Version 2.0. See https://github.com/GiR-Zippo/XIV-Modexplorer/blob/main/LICENSE for full license information.
*/

using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using XIVModExplorer.Caching;
using XIVModExplorer.HelperWindows;
using XIVModExplorer.Penumbra;
using XIVModExplorer.Scraping;
using XIVModExplorer.Utils;

namespace XIVModExplorer
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
        public System.Timers.Timer slideTimer = null;
        public int SliderIndex { get; set; } = 0;

        private Scraper scraper { get; set; } = null;
        private PenumbraApi penumbra {get;set;} = null;
        private LogWindow logwindow { get; set; } = null;
        private Thread logwindowThread { get; set; } = null;


        public MainWindow()
        {
            InitializeComponent();

            if (Configuration.GetValue("ModArchivePath") != null)
                FileTree.UpdateTreeView(Configuration.GetValue("ModArchivePath"));

            UseDatabase.IsChecked = Configuration.GetBoolValue("UseDatabase");

            FileTree.OnFileClicked += FileSelected;
            FileTree.OnRightClicked += RightSelected;
            FileTree.OnDirClicked += DirSelected;
            FileTree.OnArchiveClicked += ArchivePreview;

            scraper = new Scraper();
            penumbra = new PenumbraApi();

            //LogWindow to it's own thread
            logwindowThread = new Thread(delegate ()
            {
                logwindow = new LogWindow();
                System.Windows.Threading.Dispatcher.Run();
            });

            logwindowThread.SetApartmentState(ApartmentState.STA); // needs to be STA or throws exception
            logwindowThread.Start();
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
                selected_dir = Path.GetDirectoryName(e);
                SelectedDir.Content = selected_dir;
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

            LogWindow.Message($"[MainWindow] Previewing archive {archiveName}");
            current_preview = archiveName;
            current_archive = "";
            ModEntry me = Database.Instance.GetModByFilename(Configuration.GetRelativeModPath(archiveName));
            if (me == null)
            {
                IsForDT.Icon = FontAwesome.Sharp.IconChar.ThumbTack;
                return;
            }

            IsForDT.Icon = me.IsForDT ? FontAwesome.Sharp.IconChar.ThumbsUp : FontAwesome.Sharp.IconChar.ThumbsDown;

            resetAffectIcons();
            ResetSlideTimer();
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
            setAffectIcons(me);
            MarkdownScroll.Visibility = Visibility.Visible;
            MarkdownContent.Text = me.Description;
            if (me.PreviewPicture == "")
                return;

            JpegBitmapDecoder jpegDecoder = new JpegBitmapDecoder(Database.Instance.LoadPictureStream(me.PreviewPicture), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.None);
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

            LogWindow.Message($"[MainWindow] Opening archive {archiveName}");
            current_archive = archiveName;

            Task.Run(() =>
            {
                //Check if this archive was moved
                if (Configuration.GetBoolValue("UseDatabase"))
                {
                    string cmodpath = current_archive.Replace(Configuration.GetValue("ModArchivePath"), "");
                    cmodpath = cmodpath.Replace("\\", "/");
                    if (Database.Instance.GetModByFilename(cmodpath) != null)
                        return;

                    byte[] hash = Util.GetSHA1FromFile(current_archive);
                    var me = Database.Instance.GetModByHash(hash);
                    if (me != null)
                    {
                        if (MessageBox.Show("Update old location: " + me.Filename + "\r\nTo new location: " + cmodpath, "File moved?", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                        {
                            me.Filename = cmodpath;
                            Database.Instance.SaveData(me);
                        }
                    }
                }
            });

            ResetSlideTimer();
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
                slideTimer = new System.Timers.Timer(5000);
                slideTimer.Elapsed += OnTimedEvent;
                slideTimer.AutoReset = true;
                slideTimer.Enabled = true;
            }
            archive.Dispose();
        }

        #region Picture controls
        private void Prev_Click(object sender, RoutedEventArgs e)
        {
            if (pictures.Count() == 0)
                return;
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

        private void ResetSlideTimer()
        {
            if (slideTimer != null)
            {
                slideTimer.Elapsed -= OnTimedEvent;
                slideTimer.AutoReset = false;
                slideTimer.Enabled = false;
                SliderIndex = 0;
            }
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
                compressArchive(dlg.ResultName, selected_dir);
        }

        /// <summary>
        /// Scrape the mod data
        /// </summary>
        private async void File_Scrape_Click(object sender, RoutedEventArgs e)
        {
            SetRemoveTitleStatus(" - Collecting", true);
            toggleDownloadContext(false);

            var dlg = new FolderPicker();
            dlg.InputPath = @"C:\";
            if (dlg.ShowDialog(this) == true)
            {
                InputBox input = new InputBox("Website Url:", "Input Url");
                string data = input.ShowDialog();
                if (data != "")
                {
                    if (await scraper.ScrapeURLforData(data, dlg.ResultName))
                        MessageWindow.Show(Locales.Language.Word_Finished);
                    else
                        MessageWindow.Show(Locales.Language.Msg_Error_Fetch, Locales.Language.Word_Error);
                }
            }
            toggleDownloadContext(true);
            SetRemoveTitleStatus(" - Collecting", false);
        }

        /// <summary>
        /// Enter search mode
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Menu_Search_Click(object sender, RoutedEventArgs e)
        {
            Search search = new Search(this);
            search.Show();
        }

        /// <summary>
        /// Download a mod from a website
        /// </summary>
        private async void DownloadMenu_Click(object sender, RoutedEventArgs e)
        {
            if (DLDTUp.IsChecked.Value && Configuration.GetValue("TextToolsPath").Count() < 1)
            {
                MessageWindow.Show("No Textools Path set", "Error");
                return;
            }

            SetRemoveTitleStatus(" - Downloading", true);
            toggleDownloadContext(false);
            var dlg = new FolderPicker();
            dlg.InputPath = @"C:\";
            if (dlg.ShowDialog(this) == true)
            {
                InputBox input = new InputBox("Website Url:", "Input Url");
                string data = input.ShowDialog();
                if (data != "")
                {
                    if (await scraper.DownloadMod(data, dlg.ResultName, DLArchive.IsChecked.Value, DLRDir.IsChecked.Value, DLDTUp.IsChecked.Value))
                    {
                        FileTree.UpdateTreeView(selected_dir + "\\");
                        MessageWindow.Show(Locales.Language.Word_Finished);
                    }
                    else
                        MessageWindow.Show(Locales.Language.Msg_Error_Fetch, Locales.Language.Word_Error);
                }
            }
            toggleDownloadContext(true);
            SetRemoveTitleStatus(" - Downloading", false);
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
                LogWindow.Message($"[MainWindow] Backup penumbra mod {dirName}");
                var archive = ArchiveFactory.Create(ArchiveType.Zip);
                archive.AddAllFromDirectory(dlg.ResultName);

                string g = selected_dir + "\\" + dirName + ".pmp";
                archive.SaveTo(g, CompressionType.Deflate);
                archive.Dispose();
                FileTree.UpdateTreeView(selected_dir + "\\");
                LogWindow.Message($"[MainWindow] Backup penumbra mod {dirName} done");
                MessageWindow.Show(Locales.Language.Word_Finished);
            }
        }

        private void RedrawSelfMenuItem_Click(object sender, RoutedEventArgs e)
        {
            new PenumbraApi().Redraw(0);
        }

        private void RedrawAllMenuItem_Click(object sender, RoutedEventArgs e)
        {
            new PenumbraApi().Redraw(-1);
        }

        /// <summary>
        /// Use Database (for previews)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CacheBox_Checked(object sender, RoutedEventArgs e)
        {
            var c = sender as CheckBox;
            Configuration.SetValue("UseDatabase", c.IsChecked.Value.ToString());
        }

        private void RebuildDBMenu_Click(object sender, RoutedEventArgs e)
        {
            Database.Instance.Optimize();
        }

        private void RebuildDBFullMenu_Click(object sender, RoutedEventArgs e)
        {
            Database.Instance.Optimize(true);
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
        /// Set the textools folder
        /// </summary>
        private void SetTexToolsMenu_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new FolderPicker();
            dlg.InputPath = "C:\\";
            if (dlg.ShowDialog(this) == true)
                Configuration.SetValue("TextToolsPath", dlg.ResultPath);
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

        /// <summary>
        /// Display the LodWindow
        /// </summary>
        private void ShowLogWindowMenu_Click(object sender, RoutedEventArgs e)
        {
            logwindow.ToggleVisibility();
        }

        /// <summary>
        /// Set the mod archive folder
        /// </summary>
        private void OrphanedModsMenu_Click(object sender, RoutedEventArgs e)
        {
            Database.Instance.CheckOrphanedEntries();
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
            if (!IsModdir(selected_dir))
                return;

            string result = new DirectoryInfo(selected_dir).Parent.FullName;
            compressArchive(selected_dir, result);
        }

        /// <summary>
        /// Scrape data and save them to the selected folder
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void ContextMenu_Scrape_Click(object sender, RoutedEventArgs e)
        {
            if (!IsModdir(selected_dir))
                return;

            InputBox input = new InputBox("Website Url:", "Input Url");
            string data = input.ShowDialog();
            if (data != "")
            {
                SetRemoveTitleStatus(" - Collecting", true);
                toggleDownloadContext(false);

                if (await scraper.ScrapeURLforData(data, selected_dir))
                    MessageWindow.Show(Locales.Language.Word_Finished);
                else
                    MessageWindow.Show(Locales.Language.Msg_Error_Fetch, Locales.Language.Word_Error);

                toggleDownloadContext(true);
                SetRemoveTitleStatus(" - Collecting", false);
            }
        }

        /// <summary>
        /// Scrape data and compress the selected folder
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void ContextMenu_ScrapeCompress_Click(object sender, RoutedEventArgs e)
        {
            if (!IsModdir(selected_dir))
                return;

            InputBox input = new InputBox("Website Url:", "Input Url");
            string data = input.ShowDialog();
            if (data != "")
            {
                SetRemoveTitleStatus(" - Collecting", true);
                toggleDownloadContext(false);

                if (await scraper.ScrapeURLforData(data, selected_dir, DLArchive.IsChecked.Value, DLRDir.IsChecked.Value))
                    MessageWindow.Show(Locales.Language.Word_Finished);
                else
                    MessageWindow.Show(Locales.Language.Msg_Error_Fetch, Locales.Language.Word_Error);

                toggleDownloadContext(true);
                SetRemoveTitleStatus(" - Collecting", false);
            }
        }

        /// <summary>
        /// Download a mod the selected folder
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void ContextMenu_Download_Click(object sender, RoutedEventArgs e)
        {
            if (DLDTUp.IsChecked.Value && Configuration.GetValue("TextToolsPath").Count() < 1)
            {
                MessageWindow.Show("No Textools Path set", "Error");
                return;
            }

            SetRemoveTitleStatus(" - Downloading", true);
            toggleDownloadContext(false);

            InputBox input = new InputBox("Website Url:", "Input Url");
            string data = input.ShowDialog();
            if (data != "")
            {
                if (Database.Instance.GetModByUrl(data) != null)
                {
                    if (MessageBox.Show("Mod already downloaded, proceed?", "Warning", MessageBoxButton.YesNo) == MessageBoxResult.No)
                    {
                        toggleDownloadContext(true);
                        SetRemoveTitleStatus(" - Downloading", false);
                        return;
                    }
                }

                if (await scraper.DownloadMod(data, selected_dir, DLArchive.IsChecked.Value, DLRDir.IsChecked.Value, DLDTUp.IsChecked.Value))
                    MessageWindow.Show(Locales.Language.Word_Finished);
                else
                    MessageWindow.Show(Locales.Language.Msg_Error_Fetch, Locales.Language.Word_Error);

                FileTree.UpdateTreeView(selected_dir + "\\");
            }

            toggleDownloadContext(true);
            SetRemoveTitleStatus(" - Downloading", false);
        }

        /// <summary>
        /// Opens the Meta edit
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void EditMetadata_Click(object sender, RoutedEventArgs e)
        {
            if (!File.GetAttributes(right_clicked_item).HasFlag(FileAttributes.Directory))
                new Metadata(right_clicked_item);
        }

        /// <summary>
        /// Install selected mod
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void ContextMenu_InstallMod_Click(object sender, RoutedEventArgs e)
        {
            if (current_preview == "")
                return;

            List<string> foundFiles = new List<string>();

            string x = App.TempPath + Path.GetFileNameWithoutExtension(current_preview);
            Directory.CreateDirectory(x);
            await Task.Run(() =>
            {
                LogWindow.Message($"[MainWindow] Installing mod decompressing {current_preview}");
                using (Stream stream = File.OpenRead(current_preview))
                {
                    var reader = ReaderFactory.Open(stream);
                    while (reader.MoveToNextEntry())
                    {
                        //Extract normal ttmp2 and pmb
                        if (!reader.Entry.IsDirectory)
                        {
                            if (reader.Entry.Key.EndsWith(".ttmp") || reader.Entry.Key.EndsWith(".ttmp2") || reader.Entry.Key.EndsWith(".pmp"))
                            {
                                try
                                {
                                    reader.WriteEntryToDirectory(x, new ExtractionOptions() { ExtractFullPath = true, Overwrite = true });
                                    foundFiles.Add(reader.Entry.Key);
                                }
                                catch (ArgumentException)
                                {
                                    string newName = x + "\\" + Utils.Util.MakeValidFileName(reader.Entry.Key);
                                    using (var entryStream = reader.OpenEntryStream())
                                    {
                                        var fs = new FileStream(newName, FileMode.Create);
                                        entryStream.CopyTo(fs);
                                        fs.Close();
                                    }
                                    foundFiles.Add(Utils.Util.MakeValidFileName(reader.Entry.Key));
                                }
                            }
                            //Extract archives and mod
                            if (reader.Entry.Key.EndsWith(".zip") || reader.Entry.Key.EndsWith(".rar"))
                            {
                                reader.WriteEntryToDirectory(x, new ExtractionOptions() { ExtractFullPath = true, Overwrite = true });
                                using (Stream innerStream = File.OpenRead(x + "\\" + reader.Entry.Key))
                                {
                                    var innerReader = ReaderFactory.Open(innerStream);
                                    while (innerReader.MoveToNextEntry())
                                    {
                                        if (innerReader.Entry.Key.EndsWith(".ttmp2") || innerReader.Entry.Key.EndsWith(".pmp"))
                                        {
                                            innerReader.WriteEntryToDirectory(x, new ExtractionOptions() { ExtractFullPath = true, Overwrite = true });
                                            foundFiles.Add(innerReader.Entry.Key);
                                        }
                                    }
                                    innerReader.Dispose();
                                }
                            }
                            if (reader.Entry.Key.EndsWith(".7z"))
                            {
                                reader.WriteEntryToDirectory(x, new ExtractionOptions() { ExtractFullPath = true, Overwrite = true });
                                var innerReader = ArchiveFactory.Open(x + "\\" + reader.Entry.Key).ExtractAllEntries();
                                while (innerReader.MoveToNextEntry())
                                {
                                    if (!innerReader.Entry.IsDirectory && (innerReader.Entry.Key.EndsWith(".ttmp2") || innerReader.Entry.Key.EndsWith(".pmp")))
                                    {
                                        innerReader.WriteEntryToDirectory(x, new ExtractionOptions() { ExtractFullPath = true, Overwrite = true });
                                        foundFiles.Add(innerReader.Entry.Key);
                                    }
                                }
                                innerReader.Dispose();
                            }
                        }
                    }
                    reader.Dispose();
                }
            });
            LogWindow.Message($"[MainWindow] Installing mod decompressing {current_preview} finished");

            //Generate the list and ask
            Dictionary<string, string> d = new Dictionary<string, string>();
            foreach (var it in foundFiles)
                d.Add(x + "\\" + it, it);
            List<string> retval = new DataGridInput(d, "Select mods").ShowDialog();
            d.Clear();
            foundFiles.Clear();

            if (retval.Count() <= 0)
                return;

            foreach (string mod in retval)
                penumbra.Install(mod);
        }

        /// <summary>
        /// Backup selected mod
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void ContextMenu_BackupMod_Click(object sender, RoutedEventArgs e)
        {
            if (!Configuration.GetBoolValue("UseDatabase"))
                return;

            ModEntry me = Database.Instance.GetModByFilename(Configuration.GetRelativeModPath(current_preview));
            if (me == null)
                return;

            await Task.Run(() =>
            {
                if (me.PenumbraPath != null)
                    if (me.PenumbraPath.Length > 1 && Directory.Exists(Configuration.GetValue("PenumbraPath") + "\\" + me.PenumbraPath))
                        PenumbraBackup.BackupMod(me.PenumbraName, Configuration.GetAbsoluteModPath(me.Filename), Configuration.GetValue("PenumbraPath") + "\\" + me.PenumbraPath);
            });
        }

        /// <summary>
        /// Install backup from selected mod
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ContextMenu_InstallModBackup_Click(object sender, RoutedEventArgs e)
        {
            if (!Configuration.GetBoolValue("UseDatabase"))
                return;

            ModEntry me = Database.Instance.GetModByFilename(Configuration.GetRelativeModPath(current_preview));
            if (me == null)
                return;

            PenumbraBackup.InstallModBackup(me.PenumbraName, Configuration.GetAbsoluteModPath(me.Filename));
        }

        /// <summary>
        /// Updates the Mod to DT
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void ContextMenu_UpgradeToDT_Click(object sender, RoutedEventArgs e)
        {
            if (current_preview == "")
                return;

            if (Configuration.GetValue("TextToolsPath").Count() < 1)
            {
                MessageWindow.Show("No Textools Path set", "Error");
                return;
            }

            if (!Configuration.GetBoolValue("UseDatabase"))
                return;

            ModEntry mod = Database.Instance.GetModByFilename(Configuration.GetRelativeModPath(current_preview));
            if (mod == null)
                return;

            if (mod.IsForDT)
            {
                MessageWindow.Show("Already converted for DT", "Error");
                return;
            }

            string selectedItem = current_preview;
            string tempPath = App.TempPath + Path.GetFileNameWithoutExtension(selectedItem);
            Directory.CreateDirectory(tempPath);
            await Task.Run(() =>
            {
                LogWindow.Message($"[MainWindow] Upgrade to DT: mod decompressing {selectedItem}");
                var archive = ArchiveFactory.Open(selectedItem);
                try
                {
                    archive.ExtractAllEntries().WriteAllToDirectory(tempPath, new ExtractionOptions() { Overwrite = true, ExtractFullPath = true });
                }
                catch
                {
                    foreach (var entry in archive.Entries)
                    {
                        if (!entry.IsDirectory)
                        {
                                string entryName = string.Concat(entry.Key.Split(Path.GetInvalidFileNameChars()));
                                string dir = Path.GetDirectoryName(entry.Key.Replace('/', '\\'));
                                string filename = string.Concat(Path.GetFileName(entry.Key.Replace('/', '\\')).Split(Path.GetInvalidFileNameChars())); 
                                if (!Directory.Exists(tempPath + "\\" + dir))
                                    Directory.CreateDirectory(tempPath + "\\" + dir);

                                var str = entry.OpenEntryStream();
                                FileStream output = new FileStream(tempPath + "\\" + dir+ "\\" + filename, FileMode.Create);
                                str.CopyTo(output);
                                str.Close();
                                output.Close();
                        }
                    }
                }
                archive.Dispose();

                LogWindow.Message($"[MainWindow] Upgrade to DT: converting");
                Util.UpgradeDownloadModsToDT(tempPath);

                LogWindow.Message($"[MainWindow] Upgrade to DT: mod compressing {tempPath}");
                Util.CompressToArchive(tempPath);
                LogWindow.Message($"[MainWindow] Upgrade to DT: mod moving {tempPath}.zip to {selectedItem}");
                File.Delete(selectedItem);
                File.Move(tempPath + ".zip", selectedItem);

                LogWindow.Message($"[MainWindow] Upgrade to DT: update cache");
                mod.IsForDT = true;
                mod.HashSha1 = Util.GetSHA1FromFile(selectedItem);
                mod.ModificationDate = File.GetLastWriteTime(selectedItem);
                Database.Instance.SaveData(mod);

                LogWindow.Message($"[MainWindow] Upgrade to DT: Done");
                MessageWindow.Show("Conversion done.", "Info");
            });
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
            var Result = MessageBox.Show(selected_dir, Locales.Language.Msg_Delete_Folder_Confirm, MessageBoxButton.YesNo, MessageBoxImage.Question);
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

        #region Misc Controls
        private void OnTitleBarMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                Application.Current.MainWindow.DragMove();
            }
        }

        private void OnCloseClick(object sender, RoutedEventArgs e)
        {
            scraper.Dispose();
            penumbra.Dispose();
            logwindow.Shutdown();
            logwindowThread.Abort();            
            Application.Current.MainWindow.Close();
        }

        private void ModUrl_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            string url = (string)ModUrl.Content;
            Process.Start(url);
        }

        private void Refresh_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            FileTree.UpdateTreeView(selected_dir + "\\");
        }
        #endregion

        /// <summary>
        /// Compress folder to archive
        /// </summary>
        /// <param name="inputDirectory"></param>
        /// <param name="outputDirectory"></param>
        private async void compressArchive(string inputDirectory, string outputDirectory)
        {
            if (outputDirectory == "")
            {
                MessageWindow.Show(Locales.Language.Msg_No_Output_Dir);
                return;
            }
            await Task.Run(() =>
            {
                LogWindow.Message($"[MainWindow] Compressing {inputDirectory}");
                var dirName = new DirectoryInfo(inputDirectory).Name;
                var archive = ArchiveFactory.Create(ArchiveType.Zip);
                archive.AddAllFromDirectory(inputDirectory);

                string g = outputDirectory + "\\" + dirName + ".zip";
                archive.SaveTo(g, CompressionType.Deflate);
                archive.Dispose();
                LogWindow.Message($"[MainWindow] Compressing {inputDirectory} done");
            });
            FileTree.UpdateTreeView(outputDirectory + "\\");
            MessageWindow.Show(Locales.Language.Msg_Finished_Compressing);
        }

        private bool IsModdir(string path)
        {
            if (Directory.GetDirectories(selected_dir).Count() > 5 || Directory.GetFiles(selected_dir, "*.zip", SearchOption.TopDirectoryOnly).Count() > 2)
            {
                var Result = MessageBox.Show(Locales.Language.Msg_Wrong_ModDir + "\r\n" + selected_dir, Locales.Language.Word_Warning, MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (Result == MessageBoxResult.No)
                    return false;
            }
            return true;
        }

        public void SetRemoveTitleStatus(string status, bool add = true)
        {
            if (add)
                TitleText.Text += status;
            else
                TitleText.Text = TitleText.Text.Replace(status, "");
        }

        private void toggleDownloadContext(bool visible)
        {
            mainMenu.IsEnabled = visible;
            for (int i = 0; i != 4; i++)
            {
                MenuItem fi = FileTree.ContextMenu.Items.GetItemAt(i) as MenuItem;
                fi.IsEnabled = visible;
            }

        }

        private void resetAffectIcons()
        {
            PenumbraIcon.Visibility = Visibility.Hidden;
            HeadIcon.Visibility = Visibility.Hidden;
            TopIcon.Visibility = Visibility.Hidden;
            HandsIcon.Visibility = Visibility.Hidden;
            BottomIcon.Visibility = Visibility.Hidden;
            ShoesIcon.Visibility = Visibility.Hidden;
            NeckIcon.Visibility = Visibility.Hidden;
            WristIcon.Visibility = Visibility.Hidden;
            EarIcon.Visibility = Visibility.Hidden;
            RingIcon.Visibility = Visibility.Hidden;
        }

        private void setAffectIcons(ModEntry me)
        {
            PenumbraIcon.Visibility = me.PenumbraPath != null && me.PenumbraPath != "" ? Visibility.Visible : Visibility.Hidden;
            HeadIcon.Visibility = (me.ModTypeFlag & (UInt32)Caching.Type.HEAD) == (UInt32)Caching.Type.HEAD ? Visibility.Visible : Visibility.Hidden;
            TopIcon.Visibility = (me.ModTypeFlag & (UInt32)Caching.Type.TOP) == (UInt32)Caching.Type.TOP ? Visibility.Visible : Visibility.Hidden;
            HandsIcon.Visibility = (me.ModTypeFlag & (UInt32)Caching.Type.HANDS) == (UInt32)Caching.Type.HANDS ? Visibility.Visible : Visibility.Hidden;
            BottomIcon.Visibility = (me.ModTypeFlag & (UInt32)Caching.Type.BOTTOM) == (UInt32)Caching.Type.BOTTOM ? Visibility.Visible : Visibility.Hidden;
            ShoesIcon.Visibility = (me.ModTypeFlag & (UInt32)Caching.Type.SHOE) == (UInt32)Caching.Type.SHOE ? Visibility.Visible : Visibility.Hidden;
            NeckIcon.Visibility = (me.ModTypeFlag & (UInt32)Caching.Type.NECK) == (UInt32)Caching.Type.NECK ? Visibility.Visible : Visibility.Hidden;
            WristIcon.Visibility = (me.ModTypeFlag & (UInt32)Caching.Type.ARM) == (UInt32)Caching.Type.ARM ? Visibility.Visible : Visibility.Hidden;
            EarIcon.Visibility = (me.ModTypeFlag & (UInt32)Caching.Type.EAR) == (UInt32)Caching.Type.EAR ? Visibility.Visible : Visibility.Hidden;
            RingIcon.Visibility = (me.ModTypeFlag & (UInt32)Caching.Type.FINGER) == (UInt32)Caching.Type.FINGER ? Visibility.Visible : Visibility.Hidden;
        }

        #region SearchMode
        private void Search_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var lI = sender as ListViewItem;
            if (lI == null)
                return;

            var item = lI.Content as ModEntry;
            if (item == null)
                return;

            string fullpath = Configuration.GetValue("ModArchivePath") + item.Filename;
            if (!File.Exists(fullpath))
                return;

            if (e.ClickCount == 2)
            {
                if (File.Exists(fullpath))
                {
                    current_preview = fullpath;
                    OpenArchive(current_preview);
                }
                return;
            }
            else
            {
                if (current_preview == fullpath)
                    return;

                current_preview = fullpath;
                current_archive = "";

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

                ModName.Content = item.ModName;
                ModUrl.Content = item.Url;
                MarkdownScroll.Visibility = Visibility.Visible;
                MarkdownContent.Text = item.Description;

                if (item.PreviewPicture == "")
                    return;

                JpegBitmapDecoder jpegDecoder = new JpegBitmapDecoder(Database.Instance.LoadPictureStream(item.PreviewPicture), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.None);
                Img.Source = jpegDecoder.Frames[0];
            }
        }

        private void Search_ContextMenu_InstallMod_Click(object sender, RoutedEventArgs e)
        {
            var p = SearchList.SelectedItem as ModEntry;
            if (p == null)
                return;

            string fullpath = Configuration.GetValue("ModArchivePath") + p.Filename;
            if (File.Exists(fullpath))
            {
                current_preview = fullpath;
                ContextMenu_InstallMod_Click(null, null);
            }
        }
        #endregion
    }
}

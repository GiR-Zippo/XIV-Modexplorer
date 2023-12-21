using System.IO;
using System;
using System.Windows.Controls;
using System.Windows.Input;
using XIVModExplorer.Controls.TreeViewFileExplorer.ShellClasses;
using System.Linq;

namespace XIVModExplorer.UserCtrl
{
    /// <summary>
    /// Interaktionslogik für TreeViewFileExplorer.xaml
    /// </summary>
    public partial class TreeViewFileExplorer : UserControl
    {
        public EventHandler<string> OnFileClicked;
        public EventHandler<string> OnRightClicked;
        public EventHandler<string> OnDirClicked;
        public EventHandler<string> OnArchiveClicked;
        public TreeViewFileExplorer()
        {
            InitializeComponent();
            InitializeFileSystemObjects();
        }

        #region Events

        private void FileSystemObject_AfterExplore(object sender, System.EventArgs e)
        {
            Cursor = Cursors.Arrow;
        }

        private void FileSystemObject_BeforeExplore(object sender, System.EventArgs e)
        {
            Cursor = Cursors.Wait;
        }

        #endregion

        #region Methods

        private void InitializeFileSystemObjects()
        {
            var drives = DriveInfo.GetDrives();
            DriveInfo
                .GetDrives()
                .ToList()
                .ForEach(drive =>
                {
                    var fileSystemObject = new FileSystemObjectInfo(drive);
                    fileSystemObject.BeforeExplore += FileSystemObject_BeforeExplore;
                    fileSystemObject.AfterExplore += FileSystemObject_AfterExplore;
                    treeView.Items.Add(fileSystemObject);
                });
            PreSelect(Environment.GetFolderPath(Environment.SpecialFolder.Desktop));
        }

        public void UpdateTreeView(string dir)
        {
            var drives = DriveInfo.GetDrives();
            DriveInfo
                .GetDrives()
                .ToList()
                .ForEach(drive =>
                {
                    var fileSystemObject = new FileSystemObjectInfo(drive);
                    fileSystemObject.BeforeExplore += FileSystemObject_BeforeExplore;
                    fileSystemObject.AfterExplore += FileSystemObject_AfterExplore;
                    treeView.Items.Clear();
                    treeView.Items.Add(fileSystemObject);
                });
            PreSelect(dir);
        }

        private void PreSelect(string path)
        {
            if (!Directory.Exists(path))
            {
                return;
            }
            var driveFileSystemObjectInfo = GetDriveFileSystemObjectInfo(path);
            driveFileSystemObjectInfo.IsExpanded = true;
            PreSelect(driveFileSystemObjectInfo, path);
        }

        private void PreSelect(FileSystemObjectInfo fileSystemObjectInfo,
            string path)
        {
            foreach (var childFileSystemObjectInfo in fileSystemObjectInfo.Children)
            {
                var isParentPath = IsParentPath(path, childFileSystemObjectInfo.FileSystemInfo.FullName);
                if (isParentPath)
                {
                    if (string.Equals(childFileSystemObjectInfo.FileSystemInfo.FullName, path))
                    {
                        /* We found the item for pre-selection */
                    }
                    else
                    {
                        childFileSystemObjectInfo.IsExpanded = true;
                        PreSelect(childFileSystemObjectInfo, path);
                    }
                }
            }
        }

        #endregion

        #region Helpers

        private FileSystemObjectInfo GetDriveFileSystemObjectInfo(string path)
        {
            var directory = new DirectoryInfo(path);
            var drive = DriveInfo
                .GetDrives()
                .Where(d => d.RootDirectory.FullName == directory.Root.FullName)
                .FirstOrDefault();
            return GetDriveFileSystemObjectInfo(drive);
        }

        private FileSystemObjectInfo GetDriveFileSystemObjectInfo(DriveInfo drive)
        {
            foreach (var fso in treeView.Items.OfType<FileSystemObjectInfo>())
            {
                if (fso.FileSystemInfo.FullName == drive.RootDirectory.FullName)
                {
                    return fso;
                }
            }
            return null;
        }

        private bool IsParentPath(string path,
            string targetPath)
        {
            return path.StartsWith(targetPath);
        }

        #endregion

        private void TreeViewItem_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var node = treeView.SelectedItem as FileSystemObjectInfo;
            if (node == null)
                return;
            var t = node.FileSystemInfo;
            string filename = t.FullName;
            if (filename == "")
                return;
            if (!(filename.EndsWith(".7z") || filename.EndsWith(".zip") || filename.EndsWith(".rar")))
                return;
            OnFileClicked?.Invoke(this, filename);
        }

        private void TreeViewItem_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            var node = treeView.SelectedItem as FileSystemObjectInfo;
            if (node == null)
                return;

            var t = node.FileSystemInfo;
            if (t.FullName == "")
                    return;
            OnRightClicked?.Invoke(this, t.FullName);
        }

        private void TreeViewItem_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            var node = treeView.SelectedItem as FileSystemObjectInfo;
            if (node == null)
                return;

            var t = node.FileSystemInfo;
            if (t.Attributes == FileAttributes.Directory)
            {
                if (t.FullName == "")
                    return;
                OnDirClicked?.Invoke(this, t.FullName);
            }
            else if (t.Attributes == FileAttributes.Archive)
            {
                if (t.FullName == "")
                    return;
                OnArchiveClicked?.Invoke(this, t.FullName);
            }
        }

    }
}

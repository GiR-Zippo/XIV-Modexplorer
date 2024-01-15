/*
* Copyright(c) 2024 GiR-Zippo
* Licensed under the Mozilla Public License Version 2.0. See https://github.com/GiR-Zippo/XIV-Modexplorer/blob/main/LICENSE for full license information.
*/

using System;
using System.Windows;
using System.Windows.Input;

namespace XIVModExplorer.Caching
{
    /// <summary>
    /// Interaktionslogik für Search.xaml
    /// </summary>
    public partial class Search : Window
    {
        private MainWindow window { get; set; } = null;

        public Search(MainWindow mainWnd)
        {
            InitializeComponent();
            window = mainWnd;
            window.FileTree.Visibility = Visibility.Hidden;
            window.SearchList.Visibility = Visibility.Visible;
            window.SetRemoveTitleStatus("- Search mode", true);
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
            window.SearchList.Items.Clear();
            window.FileTree.Visibility = Visibility.Visible;
            window.SearchList.Visibility = Visibility.Hidden;
            window.SetRemoveTitleStatus("- Search mode", false);
            this.Close();
        }
        #endregion

        private async void Search_Text_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var result = await Database.Instance.FindModsAsync(Search_Text.Text, Search_Description.Text, GetModType(), GetAccsType(), "");
                window.SearchList.Items.Clear();
                foreach (var x in result)
                    window.SearchList.Items.Add(x);
                result.Clear();
            }
        }

        UInt16 GetModType()
        {
            return (UInt16)((C_Weapon.IsChecked.Value ? Type.WEAPON : Type.NONE) |
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
                                         (C_Finger.IsChecked.Value ? Type.MOUNT : Type.NONE) |
                                         (C_Animation.IsChecked.Value ? Type.ANIMATION : Type.NONE) |
                                         (C_Vfx.IsChecked.Value ? Type.VFX : Type.NONE) |
                                         (C_ACCS.IsChecked.Value ? Type.ONACC : Type.NONE));
        }

        UInt16 GetAccsType()
        {
            if (C_ACCS.IsChecked.Value)
            {
                return (UInt16)((CA_Weapon.IsChecked.Value ? Type.WEAPON : Type.NONE) |
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
            return 0;
        }
    }
}

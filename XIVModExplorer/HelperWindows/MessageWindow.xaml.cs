/*
* Copyright(c) 2023 GiR-Zippo
* Licensed under the Mozilla Public License Version 2.0. See https://github.com/GiR-Zippo/XIV-Modexplorer/blob/main/LICENSE for full license information.
*/

using System.Windows;
using System.Windows.Input;

namespace XIVModExplorer.HelperWindows
{
    /// <summary>
    /// Interaktionslogik für MessageWindow.xaml
    /// </summary>
    public partial class MessageWindow : Window
    {

        public static void Show(string content = "", string Htitle = "")
        {
            new MessageWindow(content, Htitle);
        }

        public MessageWindow()
        { }


        private MessageWindow(string content = "", string Htitle = "")
        {
            InitializeComponent();

            if (content != "")
                tContent.Text = content;

            if (Htitle != "")
                TitleText.Text = Htitle;
            this.Visibility = Visibility.Visible;
        }

        #region WindowEvents
        private void OnTitleBarMouseDown(object sender, MouseButtonEventArgs e)
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

        private void ok_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

    }
}

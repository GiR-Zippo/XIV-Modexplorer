/*
* Copyright(c) 2023 GiR-Zippo
* Licensed under the Mozilla Public License Version 2.0. See https://github.com/GiR-Zippo/XIV-Modexplorer/blob/main/LICENSE for full license information.
*/

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace XIVModExplorer.HelperWindows
{
    /// <summary>
    /// Interaktionslogik für InputBox.xaml
    /// </summary>
    public partial class InputBox : Window
    {
        public string defaulttext = "";//default textbox content
        public string errormessage = "Data not valid";//error messagebox content
        public string errortitle = "Error";//error messagebox heading title
        bool clickedOk = false;
        bool inputreset = false;

        public InputBox(string content="", string Htitle="", string DefaultText = "")
        {
            InitializeComponent();
            input.Focus();
            try
            {
                if (content != "")
                    tContent.Text = content;
            }
            catch { tContent.Text = "Error!"; }
            try
            {
                if (Htitle != "")
                    TitleText.Text = Htitle;
            }
            catch
            {
                TitleText.Text = "Error!";
            }
            try
            {
                if (DefaultText != "")
                {
                    defaulttext = DefaultText;
                    input.Text = DefaultText;
                }
            }
            catch
            {
                TitleText.Text = "Error!";
            }
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
                System.Windows.MessageBox.Show(errormessage, errortitle, MessageBoxButton.OK, MessageBoxImage.Error);
            else
            {
                this.Close();
            }
            clickedOk = false;
        }

        void cancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        #pragma warning disable CS0108
        public string ShowDialog()
        {
            base.ShowDialog();
            return input.Text;
        }
        #pragma warning restore CS0108
    }
}

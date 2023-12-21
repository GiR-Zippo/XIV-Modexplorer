/*
* Copyright(c) 2023 GiR-Zippo
* Licensed under the Mozilla Public License Version 2.0. See https://github.com/GiR-Zippo/XIV-Modexplorer/blob/main/LICENSE for full license information.
*/

using System.Windows;

namespace XIVModExplorer
{
    /// <summary>
    /// Interaktionslogik für TextInput.xaml
    /// </summary>
    public partial class TextInput : Window
    {
        public string Text { get; set; } = "";
        public TextInput()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            this.Text = UiTxt.Text;
            this.Visibility = Visibility.Hidden;
            this.Close();
        }
    }
}

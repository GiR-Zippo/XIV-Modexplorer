using System.ComponentModel;
using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using System.Runtime.CompilerServices;

namespace XIVModExplorer.HelperWindows
{
    /// <summary>
    /// Interaktionslogik für MessageWindow.xaml
    /// </summary>
    public partial class ProgressWindow : Window
    {
        public static Dispatcher WindowDispatcher { get; set; } = null;

        public static void Show(string content = "", string Htitle = "")
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                new ProgressWindow(content, Htitle);
            });
        }

        public static ProgressWindow win;

        public ProgressWindow(string content, string Htitle)
        {
            InitializeComponent();
            WindowDispatcher = this.Dispatcher;
            win = this;
            this.Visibility = Visibility.Visible;
            this.Title = Htitle;
            tContent.Text = content;
        }

        public static void Update(double val)
        {
            if (WindowDispatcher == null)
                return;
            WindowDispatcher.Invoke(new Action(() =>
            {
                win.pgProcessing.Value = val;
            }));
        }

        public static void WndClose()
        {
            if (WindowDispatcher == null)
                return;
            WindowDispatcher.Invoke(new Action(() =>
            {
                win.Close();
            }));
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
    }
}
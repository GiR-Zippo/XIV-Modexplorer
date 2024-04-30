using OpenQA.Selenium.DevTools.V120.DOM;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace XIVModExplorer.HelperWindows
{
    public class PropertyChangedBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            Application.Current.Dispatcher.BeginInvoke((Action)(() =>
            {
                PropertyChangedEventHandler handler = PropertyChanged;
                if (handler != null)
                    handler(this, new PropertyChangedEventArgs(propertyName));
            }));
        }
    }

    public class LogEntry : PropertyChangedBase
    {
        public DateTime DateTime { get; set; }
        public string Message { get; set; }
    }

    /// <summary>
    /// Interaktionslogik für LogWindow.xaml
    /// </summary>
    public partial class LogWindow : Window
    {
        public static ObservableCollection<LogEntry> LogEntries { get; set; } = new ObservableCollection<LogEntry>();
        public static Dispatcher WindowDispatcher { get; set; } = null;
        public LogWindow()
        {
            InitializeComponent();
            DataContext = LogEntries;
            WindowDispatcher = this.Dispatcher;
            LogEntries.Add(new LogEntry { DateTime = DateTime.Now, Message = "Starting logging..." });
        }

        public static void Message(string message)
        {
            if (WindowDispatcher == null)
                return;
            WindowDispatcher.Invoke(new Action(() =>
            {
                LogEntries.Add(new LogEntry { DateTime = DateTime.Now, Message = message });
            }));
        }

        public void Shutdown()
        {
            Dispatcher.Invoke(new Action(() =>
                {
                    LogEntries.Clear();
                    this.Close();
                }));
        }

        public void ToggleVisibility()
        {
            Dispatcher.Invoke(new Action(() =>
                this.Visibility = this.IsVisible ? Visibility.Hidden : Visibility.Visible
            ));
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
            this.Visibility = Visibility.Hidden;
        }
        #endregion


        private bool AutoScroll = true;
        private void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            // User scroll event : set or unset autoscroll mode
            if (e.ExtentHeightChange == 0)
            {   // Content unchanged : user scroll event
                if ((e.Source as ScrollViewer).VerticalOffset == (e.Source as ScrollViewer).ScrollableHeight)
                {   // Scroll bar is in bottom
                    // Set autoscroll mode
                    AutoScroll = true;
                }
                else
                {   // Scroll bar isn't in bottom
                    // Unset autoscroll mode
                    AutoScroll = false;
                }
            }

            // Content scroll event : autoscroll eventually
            if (AutoScroll && e.ExtentHeightChange != 0)
            {   // Content changed and autoscroll mode set
                // Autoscroll
                (e.Source as ScrollViewer).ScrollToVerticalOffset((e.Source as ScrollViewer).ExtentHeight);
            }
        }
    }
}

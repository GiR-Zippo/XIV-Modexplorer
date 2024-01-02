/*
* Copyright(c) 2023 GiR-Zippo
* Licensed under the Mozilla Public License Version 2.0. See https://github.com/GiR-Zippo/XIV-Modexplorer/blob/main/LICENSE for full license information.
*/

using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace XIVModExplorer.HelperWindows
{
    /// <summary>
    /// Interaktionslogik für DataGridInput.xaml
    /// </summary>
    public partial class DataGridInput : Window
    {

        public class DataGridItem
        {
            public int Index { get; set; } = 0;
            public bool Checked { get; set; } = false;
            public string Short { get; set; } = "";
            public string Long { get; set; } = "";
        }

        public List<DataGridItem> itemList { get; set; } = new List<DataGridItem>();
        public List<string> output { get; set; } = new List<string>();

        public DataGridInput(Dictionary<string, string> data, string hTitle = "")
        {
            int idx = 0;
            foreach (var d in data)
            {
                itemList.Add(new DataGridItem { Index=idx, Short = d.Value, Long = d.Key });
                idx++;
            }

            InitializeComponent();
            ItemList.ItemsSource = itemList;
            if (hTitle != "")
                TitleText.Text = hTitle;
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

        #region Drag&Drop
        bool bnb = false;
        private void ListViewItem_MouseMove(object sender, MouseEventArgs e)
        {
            if (bnb)
            {
                e.Handled = true;
                return;
            }
            if (e.LeftButton == MouseButtonState.Pressed && !bnb)
            {
                if (sender is ListViewItem celltext)
                {
                    DragDrop.DoDragDrop(ItemList, celltext, DragDropEffects.Move);
                    e.Handled = true;
                }
                bnb = false;
            }
        }

        private void ListViewItem_Drop(object sender, DragEventArgs e)
        {
            ListViewItem draggedObject = e.Data.GetData(typeof(ListViewItem)) as ListViewItem;
            ListViewItem targetObject = ((ListViewItem)(sender));

            var drag = draggedObject.Content as DataGridItem;
            var drop = targetObject.Content as DataGridItem;

            if (drag == drop)
                return;

            SortedDictionary<int, DataGridItem> newItems = new SortedDictionary<int, DataGridItem>();
            int index = 0;
            foreach (var p in itemList)
            {
                if (p == drag)
                    continue;

                if (p == drop)
                {
                    if (drop.Index < drag.Index)
                    {
                        newItems.Add(index, drag); index++;
                        newItems.Add(index, drop); index++;
                    }
                    else if (drop.Index > drag.Index)
                    {
                        newItems.Add(index, drop); index++;
                        newItems.Add(index, drag); index++;
                    }
                }
                else
                {
                    newItems.Add(index, p);
                    index++;
                }
            }

            index = 0;
            foreach (var p in newItems)
            {
                p.Value.Index = index;
                index++;
            }

            itemList.Clear();
            foreach (var oT in newItems)
                itemList.Add(oT.Value);

            ItemList.ItemsSource = itemList;
            ItemList.Items.Refresh();
            newItems.Clear();
        }

        private void CheckBox_MouseDown(object sender, MouseButtonEventArgs e)
        {
            bnb = true;
        }

        private void CheckBox_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            bnb = false;
        }
        #endregion


        private void OkayButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (DataGridItem d in ItemList.Items)
            {
                if (d.Checked)
                    output.Add(d.Long);
            }
            this.Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            output.Clear();
            this.Close();
        }

        #pragma warning disable CS0108
        public List<string> ShowDialog()
        {
            base.ShowDialog();
            return output;
        }
        #pragma warning restore CS0108

    }
}

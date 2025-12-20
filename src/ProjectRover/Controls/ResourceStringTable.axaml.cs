using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace ICSharpCode.ILSpy.Controls
{
    public partial class ResourceStringTable : UserControl
    {
        private IEnumerable strings;
        private string filter;
        private ObservableCollection<KeyValuePair<string,string>> filtered = new();

        public ResourceStringTable()
        {
            InitializeComponent();
        }

        public ResourceStringTable(IEnumerable strings, Control container) : this()
        {
            this.strings = strings;

            if (container != null)
            {
                container.SizeChanged += (s, e) => {
                    if (!double.IsNaN(e.NewSize.Width))
                        Width = Math.Max(e.NewSize.Width - 45, 0);
                    MaxHeight = e.NewSize.Height;
                };
                if (!double.IsNaN(container.Bounds.Width))
                    Width = Math.Max(container.Bounds.Width - 45, 0);
                MaxHeight = container.Bounds.Height;
            }

            foreach (var obj in strings)
            {
                if (obj is KeyValuePair<string,string> kv)
                    filtered.Add(kv);
            }

            var list = this.FindControl<ListBox>("resourceListView");
            list.ItemsSource = filtered;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void OnFilterTextChanged(object sender, RoutedEventArgs e)
        {
            if (this.FindControl<TextBox>("resourceFilterBox") is TextBox tb)
            {
                filter = tb.Text;
                filtered.Clear();
                foreach (var obj in strings)
                {
                    if (obj is KeyValuePair<string,string> kv)
                    {
                        if (string.IsNullOrEmpty(filter) || kv.Key.Contains(filter, StringComparison.OrdinalIgnoreCase) || kv.Value.Contains(filter, StringComparison.OrdinalIgnoreCase))
                            filtered.Add(kv);
                    }
                }
            }
        }

        private void ExecuteCopy(object sender, RoutedEventArgs e)
        {
            var sb = new StringBuilder();
            var list = this.FindControl<ListBox>("resourceListView");
            foreach (var item in list.SelectedItems)
            {
                if (item is KeyValuePair<string,string> kv)
                    sb.AppendLine($"{kv.Key}\t{kv.Value}");
                else
                    sb.AppendLine(item?.ToString());
            }
            Clipboard.SetText(sb.ToString());
        }
    }
}

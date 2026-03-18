// Copyright (c) 2025-2026 LeXtudio Inc.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy of this
// software and associated documentation files (the "Software"), to deal in the Software
// without restriction, including without limitation the rights to use, copy, modify, merge,
// publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons
// to whom the Software is furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all copies or
// substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
// INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR
// PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE
// FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
// OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.

using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Markup.Xaml;

namespace ICSharpCode.ILSpy.Controls
{
    public partial class ResourceObjectTable : UserControl
    {
        private IEnumerable resources;
        private string filter;

        public ResourceObjectTable()
        {
            InitializeComponent();
        }

        public ResourceObjectTable(IEnumerable resources, Control container) : this()
        {
            this.resources = resources;

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

            var list = this.FindControl<ListBox>("resourceListView");
            list.ItemsSource = resources;
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
                if (this.FindControl<ListBox>("resourceListView") is ListBox list)
                {
                    // Naive refresh: reset Items to filtered view
                    if (string.IsNullOrEmpty(filter))
                    {
                        list.ItemsSource = resources;
                    }
                    else
                    {
                        var filtered = new ObservableCollection<object>();
                        foreach (var obj in resources)
                        {
                            if (obj is TreeNodes.ResourcesFileTreeNode.SerializedObjectRepresentation so)
                            {
                                if ((so.Key?.Contains(filter, StringComparison.OrdinalIgnoreCase) == true) ||
                                    (so.Value?.Contains(filter, StringComparison.OrdinalIgnoreCase) == true))
                                {
                                    filtered.Add(so);
                                }
                            }
                        }
                        list.ItemsSource = filtered;
                    }
                }
            }
        }

        private void ExecuteCopy(object sender, RoutedEventArgs e)
        {
            var sb = new StringBuilder();
            if (this.FindControl<ListBox>("resourceListView") is ListBox list)
            {
                foreach (var item in list.SelectedItems)
                {
                    if (item is TreeNodes.ResourcesFileTreeNode.SerializedObjectRepresentation so)
                    {
                        sb.AppendLine($"{so.Key}\t{so.Value}\t{so.Type}");
                        continue;
                    }
                    sb.AppendLine(item?.ToString());
                }
            }
            
            Clipboard.SetText(sb.ToString());
        }
    }
}

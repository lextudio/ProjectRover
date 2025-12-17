using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ICSharpCode.ILSpy
{
    public partial class OpenFromGacDialog : Window
    {
        private List<string> items = new List<string>();

        public OpenFromGacDialog()
        {
            InitializeComponent();

            // If DataContext provides an enumerable of strings, use that as the source.
            if (DataContext is IEnumerable<string> ctxStrings)
            {
                items = ctxStrings.ToList();
                if (listView.Items != null)
                {
                    listView.Items.Clear();
                    foreach (var it in items)
                        listView.Items.Add(it);
                }
            }

            // Wire up filter box changes and selection changes defensively.
            try
            {
                if (filterTextBox != null)
                    filterTextBox.TextChanged += (s, e) => FilterTextChanged();
            }
            catch
            {
                // Ignore if control isn't present yet or subscription fails during design-time.
            }

            listView.SelectionChanged += ListSelectionChanged;
            UpdateOkButton();
        }

        private void FilterTextChanged()
        {
            var filter = filterTextBox?.Text ?? string.Empty;
            if (string.IsNullOrWhiteSpace(filter))
            {
                if (listView.Items != null)
                {
                    listView.Items.Clear();
                    foreach (var it in items)
                        listView.Items.Add(it);
                }
            }
            else
            {
                var filtered = items.Where(s => s.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
                if (listView.Items != null)
                {
                    listView.Items.Clear();
                    foreach (var it in filtered)
                        listView.Items.Add(it);
                }
            }
        }

        private void ListSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            UpdateOkButton();
        }

        private void UpdateOkButton()
        {
            if (okButton != null)
                okButton.IsEnabled = listView.SelectedItem != null;
        }

        private void OKButton_Click(object? sender, RoutedEventArgs e)
        {
            Close(true);
        }

        public string[] SelectedFileNames {
			get {
				return listView.SelectedItems.OfType<GacEntry>().Select(e => e.FileName).ToArray();
			}
		}
    }
}

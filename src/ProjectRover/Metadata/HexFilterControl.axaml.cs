using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using System.Reflection;
using Avalonia.VisualTree;

namespace ICSharpCode.ILSpy.Metadata
{
    public partial class HexFilterControl : UserControl
    {
        public static readonly StyledProperty<IContentFilter?> FilterProperty =
            AvaloniaProperty.Register<HexFilterControl, IContentFilter?>(nameof(Filter), defaultValue: null);

        public IContentFilter? Filter
        {
            get => GetValue(FilterProperty);
            set => SetValue(FilterProperty, value);
        }

        public static readonly StyledProperty<string?> FilterValueProperty =
            AvaloniaProperty.Register<HexFilterControl, string?>(nameof(FilterValue),
                defaultValue: null,
                inherits: false);

        public static readonly StyledProperty<string?> UserTextProperty =
            AvaloniaProperty.Register<HexFilterControl, string?>(nameof(UserText),
                defaultValue: null,
                inherits: false);

        public string? FilterValue
        {
            get => GetValue(FilterValueProperty);
            set => SetValue(FilterValueProperty, value);
        }

        public string? UserText
        {
            get => GetValue(UserTextProperty);
            set => SetValue(UserTextProperty, value);
        }

        public HexFilterControl()
        {
            AvaloniaXamlLoader.Load(this);
            this.PropertyChanged += (s, e) =>
            {
                if (e.Property == FilterProperty)
                {
                    var tb = this.textBox;
                    if (tb != null)
                        tb.Text = (this.Filter as ContentFilter)?.Value ?? string.Empty;
                }
                else if (e.Property == DataContextProperty)
                {
                    // When DataContext becomes a column, set initial state
                    TrySetColumnFromDataContext();
                }
                else if (e.Property == FilterValueProperty)
                {
                    // When FilterValue binding changes externally, update the textbox and column
                    if (textBox != null)
                        textBox.Text = this.FilterValue ?? string.Empty;
                    UpdateColumnFilter(this.FilterValue);
                }
            };
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            
            // Try to find textBox using e.NameScope
            textBox = e.NameScope.Find<TextBox>("textBox");
            
            if (textBox == null)
            {
                // Fallback: search the visual tree for a TextBox
                textBox = FindTextBoxInVisualTree(this);
                if (textBox != null)
                {
                    // Found in visual tree
                }
                else
                {
                    // Last resort: defer the search to after layout
                    Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        textBox = FindTextBoxInVisualTree(this);
                        if (textBox != null)
                        {
                            AttachTextBoxHandler();
                        }
                    }, DispatcherPriority.Render);
                    return;
                }
            }
            
            AttachTextBoxHandler();
        }

        private void AttachTextBoxHandler()
        {
            if (textBox == null)
            {
                return;
            }
            
            textBox.PropertyChanged += (s, args) =>
            {
                if (args.Property == TextBox.TextProperty)
                {
                    TextBox_TextChanged(textBox, EventArgs.Empty);
                }
            };
        }

        private void TrySetColumnFromDataContext()
        {
            // This method can be implemented as needed to sync DataContext with column properties
        }

        private TextBox? FindTextBoxInVisualTree(Visual visual)
        {
            foreach (var child in visual.GetVisualChildren())
            {
                if (child is TextBox tb)
                {
                    return tb;
                }
                var found = FindTextBoxInVisualTree(child);
                if (found != null)
                    return found;
            }
            return null;
        }

        private void TextBox_TextChanged(object? sender, EventArgs e)
        {
            var text = (sender as TextBox)?.Text;
            Filter = new ContentFilter(text);
            try
            {
                UpdateColumnFilter(text);
            }
            catch (Exception ex)
            {
                // Silently handle errors
            }
        }

        private void UpdateColumnFilter(string? text)
        {
            try
            {
                var dc = this.DataContext;
                if (dc != null)
                {
                    var colType = dc.GetType();
                    try {
                        if (dc is AvaloniaObject ao)
                        {
                            ao.SetValue(Avalonia.Controls.DataGridColumn.ContentFilterProperty, Filter);
                            ao.SetValue(Avalonia.Controls.DataGridColumn.FilterValueProperty, text);
                        }
                        else
                        {
                            // fallback to reflection if not an AvaloniaObject
                            var propContent = colType.GetProperty("ContentFilter", BindingFlags.Public | BindingFlags.Instance);
                            if (propContent != null)
                            {
                                propContent.SetValue(dc, Filter);
                            }
                            var propFilterValue = colType.GetProperty("FilterValue", BindingFlags.Public | BindingFlags.Instance);
                            if (propFilterValue != null)
                            {
                                propFilterValue.SetValue(dc, text);
                            }
                        }
                    }
                    catch (Exception ex2)
                    {
                        // Silently handle errors
                    }
                }
            }
            catch (Exception ex)
            {
                // Silently handle errors
            }
        }
    }
}

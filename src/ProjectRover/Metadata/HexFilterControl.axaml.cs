using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace ICSharpCode.ILSpy.Metadata
{
    public partial class HexFilterControl : UserControl
    {

        public static readonly StyledProperty<IContentFilter?> FilterProperty =
            AvaloniaProperty.Register<HexFilterControl, IContentFilter?>(nameof(Filter));

        public IContentFilter? Filter
        {
            get => GetValue(FilterProperty);
            set => SetValue(FilterProperty, value);
        }

        public HexFilterControl()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            if (textBox != null)
            {
                // Use KeyUp to detect user edits (avoids reactive subscription requirement)
                textBox.KeyUp += (_, _) => OnTextChanged();
            }
            UpdateInputPanelOpacity();
        }

        private void OnTextChanged()
        {
            var txt = textBox?.Text;
            if (string.IsNullOrEmpty(txt))
            {
                Filter = null;
            }
            else
            {
                if (int.TryParse(txt, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
                {
                    Filter = new HexContentFilter(value);
                }
                else
                {
                    Filter = null;
                }
            }
            UpdateInputPanelOpacity();
        }

        private void UpdateInputPanelOpacity()
        {
            if (inputPanel == null || textBox == null)
                return;
            inputPanel.Opacity = string.IsNullOrEmpty(textBox.Text) ? 0 : 1;
        }
    }

    public class HexContentFilter : IContentFilter
    {
        private readonly int value;
        public HexContentFilter(int value)
        {
            this.value = value;
        }

        public bool IsMatch(object? v)
        {
            if (v == null) return true;
            try
            {
                var iv = (int)v;
                return iv == value;
            }
            catch
            {
                return false;
            }
        }
    }
}

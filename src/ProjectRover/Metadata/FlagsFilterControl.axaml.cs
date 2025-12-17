using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;

namespace ICSharpCode.ILSpy.Metadata
{
    public partial class FlagsFilterControl : UserControl
    {
        private ListBox? listBox;

        public static readonly StyledProperty<FlagsContentFilter?> FilterProperty =
            AvaloniaProperty.Register<FlagsFilterControl, FlagsContentFilter?>(nameof(Filter));

        public FlagsContentFilter? Filter
        {
            get => GetValue(FilterProperty);
            set => SetValue(FilterProperty, value);
        }

        public Type? FlagsType { get; set; }

        public FlagsFilterControl()
        {
            InitializeComponent();
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            listBox = this.FindControl<ListBox>("ListBox");
            if (listBox != null)
            {
                listBox.SelectionMode = SelectionMode.Multiple;
                listBox.SelectionChanged += ListBox_SelectionChanged;
                listBox.ItemsSource = FlagGroup.GetFlags(FlagsType ?? typeof(int), mask: -1, selectedValues: 0, neutralItem: "<All>");
            }

            var filter = Filter;
            if (filter == null || filter.Mask == -1)
            {
                listBox?.SelectAll();
            }
        }


        private void ListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            // If removed neutral (-1) -> select none
            if (e.RemovedItems?.OfType<Flag>().Any(f => f.Value == -1) == true)
            {
                Filter = new FlagsContentFilter(0);
                listBox?.UnselectAll();
                return;
            }
            // If added neutral (-1) -> select all
            if (e.AddedItems?.OfType<Flag>().Any(f => f.Value == -1) == true)
            {
                Filter = new FlagsContentFilter(-1);
                listBox?.SelectAll();
                return;
            }

            bool deselectAny = e.RemovedItems?.OfType<Flag>().Any(f => f.Value != -1) == true;

            int mask = 0;
            if (listBox != null)
            {
                foreach (var item in listBox.SelectedItems.Cast<Flag>())
                {
                    if (deselectAny && item.Value == -1)
                        continue;
                    mask |= item.Value;
                }
            }

            Filter = new FlagsContentFilter(mask);
        }
    }

    public class FlagsContentFilter : IContentFilter
    {
        public int Mask { get; }

        public FlagsContentFilter(int mask)
        {
            this.Mask = mask;
        }

        public bool IsMatch(object? value)
        {
            if (value == null)
                return true;

            return Mask == -1 || (Mask & (int)value) != 0;
        }
    }
}


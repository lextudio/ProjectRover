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
}

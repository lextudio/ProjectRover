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
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Avalonia.Media;

namespace ICSharpCode.ILSpy.Controls
{
    public class ThemeAware
    {
        public static readonly AttachedProperty<object?> IconProperty =
            AvaloniaProperty.RegisterAttached<ThemeAware, Image, object?>("Icon");

        public static object? GetIcon(Image element) => element.GetValue(IconProperty);
        public static void SetIcon(Image element, object? value) => element.SetValue(IconProperty, value);

        static ThemeAware()
        {
            IconProperty.Changed.AddClassHandler<Image>((image, e) => OnIconChanged(image, e));
        }

        private static void OnIconChanged(Image image, AvaloniaPropertyChangedEventArgs e)
        {
            void Apply()
            {
                try
                {
                    var icon = image.GetValue(IconProperty);
                    var src = ICSharpCode.ILSpy.Images.LoadImage(icon);
                    image.Source = (IImage?)src;
                }
                catch { }
            }

            Apply();

            // Manage subscription lifecycle when the control is attached/detached

            void OnAttached(object? s, VisualTreeAttachmentEventArgs args)
            {
                try
                {
                    var app = Avalonia.Application.Current;
                    if (app != null)
                    {
                        // Subscribe to PropertyChanged so we detect theme changes across Avalonia versions
                        app.PropertyChanged -= OnAppPropertyChanged;
                        app.PropertyChanged += OnAppPropertyChanged;
                    }
                }
                catch { }
            }

            void OnDetached(object? s, VisualTreeAttachmentEventArgs args)
            {
                try
                {
                    var app = Avalonia.Application.Current;
                    if (app != null)
                    {
                        app.PropertyChanged -= OnAppPropertyChanged;
                    }
                    image.AttachedToVisualTree -= OnAttached;
                    image.DetachedFromVisualTree -= OnDetached;
                }
                catch { }
            }

            void OnAppPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs args)
            {
                // Theme or other application-level changes may affect which image resource is selected.
                // Simply re-apply the image when the application reports property changes.
                Apply();
            }

            // Reattach handlers to ensure single subscription
            image.AttachedToVisualTree -= OnAttached;
            image.DetachedFromVisualTree -= OnDetached;
            image.AttachedToVisualTree += OnAttached;
            image.DetachedFromVisualTree += OnDetached;
        }
    }
}

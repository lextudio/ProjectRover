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

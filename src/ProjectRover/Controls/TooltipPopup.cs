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
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;

using ICSharpCode.ILSpy.Themes;

namespace ICSharpCode.ILSpy.Controls
{
    class Popup : Avalonia.Controls.Primitives.Popup
    {
        const double DefaultMaxWidth = 800;
        static readonly IBrush DefaultBackground = Brushes.LightYellow;
        static readonly IBrush DefaultBorderBrush = Brushes.Gray;
        static readonly IBrush DefaultForeground = Brushes.Black;
        readonly Border border;
        bool isPointerInside;
        bool staysOpen = true;
        public event Action<bool>? PointerInsideChanged;

        public Popup()
        {
            border = new Border
            {
                BorderThickness = new Thickness(1),
                MaxHeight = 400
            };
            ApplyTooltipTheme(border);
            Child = border;
            TakesFocusFromNativeControl = false;
            StaysOpen = true;
            Opened += OnPopupOpened;
            // Track pointer in/out for close logic.
            border.PointerEntered += OnPointerEnterHandler;
            border.PointerExited += OnPointerLeaveHandler;
        }

        internal bool IsPointerInside => isPointerInside;

        public bool StaysOpen
        {
            get => staysOpen;
            set
            {
                staysOpen = value;
                IsLightDismissEnabled = !value;
            }
        }

        internal virtual bool CloseWhenMouseMovesAway => !IsKeyboardFocusWithin;

        protected Border BorderRoot => border;

        internal void SetContent(Control documentControl, double maxWidth)
        {
            var safeMaxWidth = GetSafeMaxWidth(maxWidth);
            documentControl.MaxWidth = safeMaxWidth;
            border.Child = documentControl;
        }

        protected virtual void OnPointerInsideChanged(bool isInside)
        {
        }

        void OnPopupOpened(object? sender, EventArgs e)
        {
            if (PlacementTarget is IInputElement inputElement)
                OverlayInputPassThroughElement = inputElement;
        }

        // PointerLeave override is not available for Popup in all Avalonia versions; use event subscription instead
        void OnPointerEnterHandler(object? sender, PointerEventArgs e)
        {
            isPointerInside = true;
            PointerInsideChanged?.Invoke(true);
            OnPointerInsideChanged(true);
        }

        void OnPointerLeaveHandler(object? sender, PointerEventArgs e)
        {
            isPointerInside = false;
            PointerInsideChanged?.Invoke(false);
            OnPointerInsideChanged(false);
        }

        static double GetSafeMaxWidth(double maxWidth)
        {
            if (double.IsNaN(maxWidth) || double.IsInfinity(maxWidth) || maxWidth <= 0)
                return DefaultMaxWidth;
            return maxWidth;
        }

        static void ApplyTooltipTheme(Border target)
        {
            target.Background = TryFindBrush("ToolTipBackgroundBrush") ?? DefaultBackground;
            target.BorderBrush = TryFindBrush("ToolTipBorderBrush") ?? DefaultBorderBrush;
            var foreground = TryFindBrush(ResourceKeys.ToolTipForegroundBrush)
                ?? TryFindBrush(ResourceKeys.TextForegroundBrush)
                ?? (ThemeManager.Current.IsDarkTheme ? Brushes.White : DefaultForeground);
            target.SetValue(TextElement.ForegroundProperty, foreground);
        }

        static IBrush? TryFindBrush(string key)
        {
            return ThemeManager.Current.TryGetThemeResource(key, out var resource) ? resource as IBrush : null;
        }
    }

    sealed class Tooltip : Popup
    {
        public void SetContent(object content, double fontSize, double maxWidth)
        {
            if (content is string text)
            {
                SetContent(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, FontSize = fontSize }, maxWidth);
                return;
            }

            if (content is Control control)
            {
                SetContent(control, maxWidth);
                return;
            }

            SetContent(new TextBlock { Text = content?.ToString() ?? string.Empty, TextWrapping = TextWrapping.Wrap, FontSize = fontSize }, maxWidth);
        }
    }
}

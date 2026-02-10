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

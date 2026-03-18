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
using Avalonia.Input;
using Avalonia.Media;

namespace ProjectRover.Controls
{
    public partial class ZoomScrollViewer : UserControl
    {
        private ScrollViewer? scrollViewer;
        private LayoutTransformControl? layoutTransformControl;

        public static readonly StyledProperty<double> ZoomProperty = AvaloniaProperty.Register<ZoomScrollViewer, double>(
            nameof(Zoom), defaultValue: 1.0);

        public double Zoom
        {
            get => GetValue(ZoomProperty);
            set => SetValue(ZoomProperty, value);
        }

        public ZoomScrollViewer()
        {
            this.InitializeComponent();
            this.AttachedToVisualTree += OnAttachedToVisualTree;
        }

        private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            scrollViewer = this.FindControl<ScrollViewer>("PART_ScrollViewer");
            layoutTransformControl = this.FindControl<LayoutTransformControl>("PART_Content");

            this.AddHandler(InputElement.PointerWheelChangedEvent, OnPointerWheelChanged, handledEventsToo: true);
        }

        private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            var modifiers = e.KeyModifiers;
            if ((modifiers & KeyModifiers.Control) == 0)
                return;

            var delta = e.Delta.Y;
            var old = Zoom;
            var factor = Math.Exp(delta * 0.0015);
            var newZoom = Math.Clamp(old * factor, 0.1, 10.0);
            // TODO: Calculate and adjust scroll offsets so the point under the mouse remains under the mouse after zoom.
            // This requires mapping the pointer position to content coordinates before/after zoom and updating scrollViewer offsets.
            // TODO: Support pinch-to-zoom (touch gestures) and expose MinZoom/MaxZoom properties.
            Zoom = newZoom;

            e.Handled = true;
        }
    }
}

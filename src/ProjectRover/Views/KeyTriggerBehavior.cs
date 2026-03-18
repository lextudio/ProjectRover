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
using Avalonia.Input;
using Avalonia.Xaml.Interactivity;
using Avalonia.Controls;
using Avalonia.Interactivity;
using System.Windows.Input;

namespace ICSharpCode.ILSpy
{
    public class KeyTriggerBehavior : Behavior<Control>
    {
        public static readonly StyledProperty<Key> KeyProperty =
            AvaloniaProperty.Register<KeyTriggerBehavior, Key>(nameof(Key));

        public Key Key
        {
            get => GetValue(KeyProperty);
            set => SetValue(KeyProperty, value);
        }

        public static readonly StyledProperty<ICommand?> CommandProperty =
            AvaloniaProperty.Register<KeyTriggerBehavior, ICommand?>(nameof(Command));

        public ICommand? Command
        {
            get => GetValue(CommandProperty);
            set => SetValue(CommandProperty, value);
        }

        private Control? associated;

        protected override void OnAttached()
        {
            base.OnAttached();
            associated = AssociatedObject;
            if (associated != null)
                associated.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        }

        protected override void OnDetaching()
        {
            if (associated != null)
                associated.RemoveHandler(InputElement.KeyDownEvent, OnKeyDown);
            associated = null;
            base.OnDetaching();
        }

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            var cmd = Command;
            if (e.Key == Key && cmd != null && cmd.CanExecute(null))
            {
                cmd.Execute(null);
                e.Handled = true;
            }
        }
    }
}

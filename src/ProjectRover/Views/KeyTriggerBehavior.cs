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

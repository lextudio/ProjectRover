using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;

namespace ICSharpCode.ILSpy.Controls
{
    public class SearchBox : TextBox
    {
        public static readonly StyledProperty<string> WatermarkTextProperty =
            AvaloniaProperty.Register<SearchBox, string>(nameof(WatermarkText));

        public static readonly StyledProperty<IBrush> WatermarkColorProperty =
            AvaloniaProperty.Register<SearchBox, IBrush>(nameof(WatermarkColor));

        public static readonly StyledProperty<bool> HasTextProperty =
            AvaloniaProperty.Register<SearchBox, bool>(nameof(HasText));

        public static readonly StyledProperty<TimeSpan> UpdateDelayProperty =
            AvaloniaProperty.Register<SearchBox, TimeSpan>(nameof(UpdateDelay), TimeSpan.FromMilliseconds(200));

        DispatcherTimer timer;

        public string WatermarkText
        {
            get => GetValue(WatermarkTextProperty);
            set => SetValue(WatermarkTextProperty, value);
        }

        public IBrush WatermarkColor
        {
            get => GetValue(WatermarkColorProperty);
            set => SetValue(WatermarkColorProperty, value);
        }

        public bool HasText
        {
            get => GetValue(HasTextProperty);
            private set => SetValue(HasTextProperty, value);
        }

        public TimeSpan UpdateDelay
        {
            get => GetValue(UpdateDelayProperty);
            set => SetValue(UpdateDelayProperty, value);
        }

        public SearchBox()
        {
        }

        void OnTextChanged()
        {
            HasText = !string.IsNullOrEmpty(this.Text);
            if (timer == null)
            {
                timer = new DispatcherTimer();
                timer.Interval = UpdateDelay;
                timer.Tick += Timer_Tick;
            }
            timer.Stop();
            timer.Interval = UpdateDelay;
            timer.Start();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == TextProperty)
            {
                OnTextChanged();
            }
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            timer.Stop();
            timer = null;
            // For Avalonia, binding updates are usually immediate; keep this as a hook for delayed updates if needed.
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Escape && !string.IsNullOrEmpty(this.Text))
            {
                this.Text = string.Empty;
                e.Handled = true;
            }
            else
            {
                base.OnKeyDown(e);
            }
        }
    }
}

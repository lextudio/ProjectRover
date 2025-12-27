using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Reactive;
using Avalonia.Threading;
using Avalonia.Styling;

namespace ICSharpCode.ILSpy.Controls
{
    public partial class SearchBox : UserControl
    {
        public event EventHandler<KeyEventArgs>? PreviewKeyDown;
        public event EventHandler<RoutedEventArgs>? TextChanged;
        public static readonly StyledProperty<string?> TextProperty = AvaloniaProperty.Register<SearchBox, string?>(nameof(Text));
        public static readonly StyledProperty<string?> WatermarkTextProperty = AvaloniaProperty.Register<SearchBox, string?>(nameof(WatermarkText));
        public static readonly StyledProperty<IBrush?> WatermarkColorProperty = AvaloniaProperty.Register<SearchBox, IBrush?>(nameof(WatermarkColor), Brushes.Gray);
        public static readonly StyledProperty<TimeSpan> UpdateDelayProperty = AvaloniaProperty.Register<SearchBox, TimeSpan>(nameof(UpdateDelay), TimeSpan.FromMilliseconds(200));

        private DispatcherTimer? timer;
        private IDisposable? textSubscription;
        private Button? iconButton;
        private PathIcon? iconGlyph;
        private TextBox? textBox;
        private Label? watermarkLabel;

        public SearchBox()
        {
            InitializeComponent();
            this.AttachedToVisualTree += (_, _) => {
                iconButton = this.FindControl<Button>("IconButton");
                if (iconButton != null)
                    iconButton.Click += Clear_Click;

                textBox = this.FindControl<TextBox>("PART_TextBox");
                if (textBox != null)
                {
                    textSubscription = textBox.GetObservable(TextBox.TextProperty).Subscribe(new ActionObserver<string>(_ => OnLocalTextChanged()));
                    // forward text events
                    textBox.GetObservable(TextBox.TextProperty).Subscribe(new ActionObserver<string>(_ => {
                        RaiseTextChanged();
                        TextChanged?.Invoke(this, new RoutedEventArgs());
                    }));
                    // forward key events
                    textBox.KeyDown += TextBox_KeyDown;
                }

                watermarkLabel = this.FindControl<Label>("WatermarkLabel");
                iconGlyph = this.FindControl<PathIcon>("IconGlyph");
                UpdateWatermarkLabel();
            };

            this.DetachedFromVisualTree += (_, _) => {
                if (iconButton != null)
                    iconButton.Click -= Clear_Click;
                textSubscription?.Dispose();
                textSubscription = null;
                if (textBox != null)
                    textBox.KeyDown -= TextBox_KeyDown;
                timer?.Stop();
                timer = null;
            };
        }

        private void TextBox_KeyDown(object? sender, KeyEventArgs e)
        {
            PreviewKeyDown?.Invoke(this, e);
        }

        private void RaiseTextChanged()
        {
            // Raise a classic RoutedEvent-like TextChanged by using a RoutedEventArgs
            var args = new RoutedEventArgs();
            RaiseEvent(args);
        }

        private void OnLocalTextChanged()
        {
            HasText = !string.IsNullOrEmpty(this.Text);

            if (timer == null)
            {
                timer = new DispatcherTimer { Interval = UpdateDelay };
                timer.Tick += Timer_Tick;
            }
            timer.Stop();
            timer.Interval = UpdateDelay;
            timer.Start();
            UpdateWatermarkLabel();
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            timer?.Stop();
            timer = null;
            DebouncedText = this.Text;
        }

        private void Clear_Click(object? sender, RoutedEventArgs e)
        {
            this.Text = string.Empty;
            DebouncedText = string.Empty;
            var tb = textBox ?? this.FindControl<TextBox>("PART_TextBox");
            tb?.Focus();
        }

        private void UpdateWatermarkLabel()
        {
            if (watermarkLabel != null)
            {
                watermarkLabel.IsVisible = !HasText;
            }
            UpdateIconState();
        }

        private void UpdateIconState()
        {
            if (iconGlyph == null)
                return;

            var geometry = HasText ? TryGetGeometry("CloseIconGeometry") : TryGetGeometry("SearchIconGeometry");
            iconGlyph.Data = geometry;
            ToolTip.SetTip(iconGlyph, HasText ? "Clear" : "Search");
        }

        private Geometry? TryGetGeometry(string key)
        {
            if (Application.Current?.TryFindResource(key, Application.Current.ActualThemeVariant ?? ThemeVariant.Light, out var resource) == true)
            {
                return resource as Geometry;
            }

            if (Application.Current?.TryFindResource(key, out var fallback) == true)
            {
                return fallback as Geometry;
            }

            return null;
        }

        public string? Text
        {
            get => GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public string? WatermarkText
        {
            get => GetValue(WatermarkTextProperty);
            set => SetValue(WatermarkTextProperty, value);
        }

        public IBrush? WatermarkColor
        {
            get => GetValue(WatermarkColorProperty);
            set => SetValue(WatermarkColorProperty, value);
        }

        public TimeSpan UpdateDelay
        {
            get => GetValue(UpdateDelayProperty);
            set => SetValue(UpdateDelayProperty, value);
        }

        public static readonly StyledProperty<bool> HasTextProperty = AvaloniaProperty.Register<SearchBox, bool>(nameof(HasText));
        public bool HasText
        {
            get => GetValue(HasTextProperty);
            private set => SetValue(HasTextProperty, value);
        }

        public static readonly StyledProperty<string?> DebouncedTextProperty = AvaloniaProperty.Register<SearchBox, string?>(nameof(DebouncedText));
        public string? DebouncedText
        {
            get => GetValue(DebouncedTextProperty);
            private set => SetValue(DebouncedTextProperty, value);
        }

        private sealed class ActionObserver<T> : IObserver<T>
        {
            private readonly Action<T> onNext;

            public ActionObserver(Action<T> onNext)
            {
                this.onNext = onNext;
            }

            public void OnNext(T value) => onNext(value);
            public void OnError(Exception error) { }
            public void OnCompleted() { }
        }
    }
}

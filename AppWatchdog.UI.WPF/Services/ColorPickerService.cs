using AppWatchdog.UI.WPF.Converters;
using AppWatchdog.UI.WPF.Localization;
using System;
using System.ComponentModel;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Wpf.Ui;
using Wpf.Ui.Controls;
using Wpf.Ui.Extensions;
using UiTextBlock = Wpf.Ui.Controls.TextBlock;
using UiTextBox = Wpf.Ui.Controls.TextBox;

namespace AppWatchdog.UI.WPF.Services;

public sealed class ColorPickerService
{
    private readonly IContentDialogService _dialogService;

    public ColorPickerService(IContentDialogService dialogService)
    {
        _dialogService = dialogService;
    }

    public async Task<string?> PickAsync(string? initialHex = null)
    {
        var model = new ColorPickerModel(initialHex);
        var content = BuildContent(model);

        var result = await _dialogService.ShowSimpleDialogAsync(
            new SimpleContentDialogCreateOptions
            {
                Title = "Pick color",
                Content = content,
                PrimaryButtonText = AppStrings.ok,
                CloseButtonText = AppStrings.cancel
            },
            CancellationToken.None);

        return result == ContentDialogResult.Primary
            ? model.Hex
            : null;
    }

    private static UIElement BuildContent(ColorPickerModel model)
    {
        var converter = new HexToBrushConverter();

        var root = new StackPanel
        {
            Margin = new Thickness(12),
            DataContext = model
        };

        var previewCard = new Card
        {
            Padding = new Thickness(12),
            Margin = new Thickness(0, 0, 0, 12)
        };

        var preview = new Border
        {
            Height = 56,
            CornerRadius = new CornerRadius(10),
            BorderThickness = new Thickness(1),
            BorderBrush = System.Windows.Media.Brushes.Black
        };
        preview.SetBinding(Border.BackgroundProperty, new Binding(nameof(ColorPickerModel.Hex))
        {
            Converter = converter
        });
        previewCard.Content = preview;
        root.Children.Add(previewCard);

        var hexCard = new Card
        {
            Padding = new Thickness(12),
            Margin = new Thickness(0, 0, 0, 12)
        };

        var hexRow = new Grid();
        hexRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
        hexRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        hexRow.Children.Add(new UiTextBlock
        {
            Text = "Hex",
            VerticalAlignment = VerticalAlignment.Center
        });
        var hexBox = new UiTextBox
        {
            PlaceholderText = "#RRGGBB"
        };
        hexBox.SetBinding(UiTextBox.TextProperty, new Binding(nameof(ColorPickerModel.Hex))
        {
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
        });
        Grid.SetColumn(hexBox, 1);
        hexRow.Children.Add(hexBox);
        hexCard.Content = hexRow;
        root.Children.Add(hexCard);

        var rgbCard = new Card
        {
            Padding = new Thickness(12)
        };

        var rgbStack = new StackPanel();
        rgbStack.Children.Add(BuildSliderRow("R", nameof(ColorPickerModel.R)));
        rgbStack.Children.Add(BuildSliderRow("G", nameof(ColorPickerModel.G)));
        rgbStack.Children.Add(BuildSliderRow("B", nameof(ColorPickerModel.B)));
        rgbCard.Content = rgbStack;
        root.Children.Add(rgbCard);

        return root;
    }

    private static UIElement BuildSliderRow(string label, string bindingPath)
    {
        var row = new Grid { Margin = new Thickness(0, 6, 0, 0) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });

        var text = new UiTextBlock
        {
            Text = label,
            VerticalAlignment = VerticalAlignment.Center
        };
        row.Children.Add(text);

        var slider = new System.Windows.Controls.Slider
        {
            Minimum = 0,
            Maximum = 255,
            VerticalAlignment = VerticalAlignment.Center
        };
        slider.SetBinding(System.Windows.Controls.Slider.ValueProperty, new Binding(bindingPath)
        {
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
        });
        Grid.SetColumn(slider, 1);
        row.Children.Add(slider);

        var numberBox = new NumberBox
        {
            Minimum = 0,
            Maximum = 255,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        numberBox.SetBinding(NumberBox.ValueProperty, new Binding(bindingPath)
        {
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
        });
        Grid.SetColumn(numberBox, 2);
        row.Children.Add(numberBox);

        return row;
    }

    private sealed class ColorPickerModel : INotifyPropertyChanged
    {
        private int _r;
        private int _g;
        private int _b;
        private string _hex = "#000000";
        private bool _suppressUpdates;

        public event PropertyChangedEventHandler? PropertyChanged;

        public int R
        {
            get => _r;
            set
            {
                if (_r == value) return;
                _r = Clamp(value);
                OnPropertyChanged(nameof(R));
                UpdateHexFromRgb();
            }
        }

        public int G
        {
            get => _g;
            set
            {
                if (_g == value) return;
                _g = Clamp(value);
                OnPropertyChanged(nameof(G));
                UpdateHexFromRgb();
            }
        }

        public int B
        {
            get => _b;
            set
            {
                if (_b == value) return;
                _b = Clamp(value);
                OnPropertyChanged(nameof(B));
                UpdateHexFromRgb();
            }
        }

        public string Hex
        {
            get => _hex;
            set
            {
                if (_hex == value) return;
                _hex = NormalizeHex(value);
                OnPropertyChanged(nameof(Hex));
                UpdateRgbFromHex();
            }
        }

        public ColorPickerModel(string? initialHex)
        {
            if (!string.IsNullOrWhiteSpace(initialHex))
                Hex = initialHex;
            else
                UpdateHexFromRgb();
        }

        private void UpdateHexFromRgb()
        {
            if (_suppressUpdates)
                return;

            _suppressUpdates = true;
            Hex = $"#{_r:X2}{_g:X2}{_b:X2}";
            _suppressUpdates = false;
        }

        private void UpdateRgbFromHex()
        {
            if (_suppressUpdates)
                return;

            if (!TryParseHex(_hex, out var r, out var g, out var b))
                return;

            _suppressUpdates = true;
            R = r;
            G = g;
            B = b;
            _suppressUpdates = false;
        }

        private static bool TryParseHex(string value, out int r, out int g, out int b)
        {
            r = g = b = 0;
            if (string.IsNullOrWhiteSpace(value))
                return false;

            var hex = value.Trim();
            if (hex.StartsWith('#'))
                hex = hex[1..];

            if (hex.Length != 6)
                return false;

            return int.TryParse(hex.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out r)
                   && int.TryParse(hex.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out g)
                   && int.TryParse(hex.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out b);
        }

        private static string NormalizeHex(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "#000000";

            var hex = value.Trim();
            return hex.StartsWith('#') ? hex : "#" + hex;
        }

        private static int Clamp(int value) => Math.Max(0, Math.Min(255, value));

        private void OnPropertyChanged(string name)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

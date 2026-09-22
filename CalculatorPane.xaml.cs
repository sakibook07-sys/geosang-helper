using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Button = System.Windows.Controls.Button;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using UserControl = System.Windows.Controls.UserControl;

namespace GeosangHub;

public partial class CalculatorPane : UserControl
{
    private string _entry = "0";
    private decimal? _stored;
    private string? _operation;
    private bool _startNewEntry;

    public CalculatorPane() => InitializeComponent();

    private void Key_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string key }) Press(key);
    }

    private void Calculator_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        string? key = e.Key switch
        {
            >= Key.D0 and <= Key.D9 when Keyboard.Modifiers == ModifierKeys.None => ((int)e.Key - (int)Key.D0).ToString(),
            >= Key.NumPad0 and <= Key.NumPad9 => ((int)e.Key - (int)Key.NumPad0).ToString(),
            Key.Decimal or Key.OemPeriod => ".",
            Key.Add => "+",
            Key.Subtract or Key.OemMinus => "-",
            Key.Multiply => "*",
            Key.Divide => "/",
            Key.OemPlus when Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) => "+",
            Key.Enter or Key.Return => "=",
            Key.Escape => "C",
            Key.Back => "BACK",
            _ => null
        };
        if (key == null) return;
        Press(key);
        e.Handled = true;
    }

    private void Press(string key)
    {
        if (_entry == "오류" && key != "C") Clear();

        if (key.Length == 1 && char.IsAsciiDigit(key[0]))
        {
            if (_startNewEntry)
            {
                _entry = key;
                _startNewEntry = false;
            }
            else if (_entry.TrimStart('-').Replace(".", "").Length < 28)
                _entry = _entry == "0" ? key : _entry + key;
        }
        else switch (key)
        {
            case ".":
                if (_startNewEntry) { _entry = "0"; _startNewEntry = false; }
                if (!_entry.Contains('.')) _entry += ".";
                break;
            case "SIGN":
                if (_entry != "0") _entry = _entry.StartsWith('-') ? _entry[1..] : "-" + _entry;
                break;
            case "BACK":
                if (_startNewEntry) { _entry = "0"; _startNewEntry = false; }
                else _entry = _entry.Length <= 1 || (_entry.Length == 2 && _entry[0] == '-') ? "0" : _entry[..^1];
                break;
            case "CE":
                _entry = "0";
                _startNewEntry = false;
                break;
            case "C":
                Clear();
                break;
            case "+" or "-" or "*" or "/":
                SetOperation(key);
                break;
            case "=":
                EqualsPressed();
                break;
        }

        DisplayText.Text = _entry;
        DisplayText.ToolTip = _entry;
    }

    private void SetOperation(string operation)
    {
        if (!ReadEntry(out var value)) return;
        if (_operation != null && !_startNewEntry)
        {
            if (!TryCalculate(_stored ?? 0m, value, _operation, out value)) return;
            _entry = Format(value);
        }
        _stored = value;
        _operation = operation;
        _startNewEntry = true;
        ExpressionText.Text = $"{Format(value)} {Symbol(operation)}";
    }

    private void EqualsPressed()
    {
        if (_operation == null || _stored == null) return;
        if (!ReadEntry(out var value)) return;
        if (!TryCalculate(_stored.Value, value, _operation, out var result)) return;
        ExpressionText.Text = $"{Format(_stored.Value)} {Symbol(_operation)} {Format(value)} =";
        _entry = Format(result);
        _stored = null;
        _operation = null;
        _startNewEntry = true;
    }

    private static string Symbol(string operation) => operation switch { "*" => "×", "/" => "÷", "-" => "−", _ => operation };

    private static string Format(decimal value) => value.ToString("G29", CultureInfo.InvariantCulture);

    private bool ReadEntry(out decimal value)
    {
        if (decimal.TryParse(_entry, NumberStyles.Number, CultureInfo.InvariantCulture, out value)) return true;
        ShowError();
        return false;
    }

    private bool TryCalculate(decimal first, decimal second, string operation, out decimal result)
    {
        try
        {
            result = operation switch
            {
                "+" => first + second,
                "-" => first - second,
                "*" => first * second,
                "/" => first / second,
                _ => second
            };
            return true;
        }
        catch (Exception ex) when (ex is DivideByZeroException or OverflowException)
        {
            result = 0;
            ShowError();
            return false;
        }
    }

    private void ShowError()
    {
        _entry = "오류";
        _stored = null;
        _operation = null;
        _startNewEntry = true;
        ExpressionText.Text = "0으로 나누거나 계산 범위를 넘었어요";
    }

    private void Clear()
    {
        _entry = "0";
        _stored = null;
        _operation = null;
        _startNewEntry = false;
        ExpressionText.Text = string.Empty;
    }
}

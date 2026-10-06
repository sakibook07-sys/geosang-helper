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
    private static readonly JeonUnit[] JeonUnits =
    [
        new("천", 1_000), new("만", 10_000), new("십만", 100_000),
        new("백만", 1_000_000), new("천만", 10_000_000),
        new("억", 100_000_000), new("십억", 1_000_000_000)
    ];
    private string _entry = "0";
    private decimal? _stored;
    private string? _operation;
    private bool _startNewEntry;
    private bool _jeonReady;
    public event EventHandler<JeonSettingsChangedEventArgs>? JeonSettingsChanged;

    public CalculatorPane()
    {
        InitializeComponent();
        JeonUnitCombo.ItemsSource = JeonUnits;
        JeonUnitCombo.SelectedIndex = 5;
        _jeonReady = true;
        UpdateJeonConversion();
    }

    public void ConfigureJeonSettings(decimal rate, long unitMultiplier)
    {
        _jeonReady = false;
        JeonRateInput.Text = (rate > 0 ? rate : 3000m).ToString("G29", CultureInfo.InvariantCulture);
        JeonUnitCombo.SelectedItem = JeonUnits.FirstOrDefault(x => x.Multiplier == unitMultiplier) ?? JeonUnits[5];
        _jeonReady = true;
        UpdateJeonConversion();
    }

    private void Key_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string key }) Press(key);
    }

    private void Calculator_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.OriginalSource is System.Windows.Controls.TextBox or System.Windows.Controls.ComboBox) return;
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

        DisplayText.Text = FormatEntryForDisplay(_entry);
        DisplayText.ToolTip = DisplayText.Text;
        UpdateJeonConversion();
    }

    private void JeonSettingChanged(object sender, EventArgs e)
    {
        if (!_jeonReady) return;
        UpdateJeonConversion();
        if (TryReadJeonRate(out decimal rate) && JeonUnitCombo.SelectedItem is JeonUnit unit)
            JeonSettingsChanged?.Invoke(this, new JeonSettingsChangedEventArgs(rate, unit.Multiplier));
    }

    private void UpdateJeonConversion()
    {
        JeonValidationText.Text = string.Empty;
        if (!TryReadJeonRate(out decimal rate))
        {
            JeonAmountText.Text = JeonCashText.Text = "-";
            JeonValidationText.Text = "1억당 현금가를 0보다 큰 숫자로 입력해주세요.";
            return;
        }
        if (JeonUnitCombo.SelectedItem is not JeonUnit unit || !ReadEntryWithoutError(out decimal number) || number < 0)
        {
            JeonAmountText.Text = JeonCashText.Text = "-";
            JeonValidationText.Text = "계산기에 0 이상의 금액을 입력해주세요.";
            return;
        }
        decimal rawPrice;
        try { rawPrice = number * unit.Multiplier; }
        catch (OverflowException) { ShowJeonTooLarge(); return; }
        if (rawPrice > long.MaxValue) { ShowJeonTooLarge(); return; }
        long itemPrice = (long)decimal.Round(rawPrice, 0, MidpointRounding.AwayFromZero);
        decimal cashPrice = itemPrice / 100_000_000m * rate;
        JeonAmountText.Text = FormatKoreanPrice(itemPrice) + $"  ({itemPrice:N0}원)";
        JeonCashText.Text = cashPrice < 1m && cashPrice > 0m
            ? $"약 {cashPrice:N2}원"
            : $"약 {decimal.Round(cashPrice, 0, MidpointRounding.AwayFromZero):N0}원";
    }

    private void ShowJeonTooLarge()
    {
        JeonAmountText.Text = JeonCashText.Text = "-";
        JeonValidationText.Text = "환산할 금액이 너무 큽니다.";
    }

    private bool TryReadJeonRate(out decimal rate)
    {
        string text = JeonRateInput.Text.Replace(",", "").Trim();
        return decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out rate) && rate > 0;
    }

    private bool ReadEntryWithoutError(out decimal value) =>
        decimal.TryParse(_entry, NumberStyles.Number, CultureInfo.InvariantCulture, out value);

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
        ExpressionText.Text = $"{FormatNumber(value)} {Symbol(operation)}";
    }

    private void EqualsPressed()
    {
        if (_operation == null || _stored == null) return;
        if (!ReadEntry(out var value)) return;
        if (!TryCalculate(_stored.Value, value, _operation, out var result)) return;
        ExpressionText.Text = $"{FormatNumber(_stored.Value)} {Symbol(_operation)} {FormatNumber(value)} =";
        _entry = Format(result);
        _stored = null;
        _operation = null;
        _startNewEntry = true;
    }

    private static string Symbol(string operation) => operation switch { "*" => "×", "/" => "÷", "-" => "−", _ => operation };

    private static string Format(decimal value) => value.ToString("G29", CultureInfo.InvariantCulture);

    private static string FormatNumber(decimal value) =>
        value.ToString("#,0.############################", CultureInfo.GetCultureInfo("ko-KR"));

    private static string FormatEntryForDisplay(string entry)
    {
        if (entry == "오류") return entry;
        int decimalPoint = entry.IndexOf('.');
        string wholeText = decimalPoint >= 0 ? entry[..decimalPoint] : entry;
        string fraction = decimalPoint >= 0 ? entry[decimalPoint..] : string.Empty;
        if (!decimal.TryParse(wholeText, NumberStyles.Integer, CultureInfo.InvariantCulture, out decimal whole)) return entry;
        return whole.ToString("#,0", CultureInfo.GetCultureInfo("ko-KR")) + fraction;
    }

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

    private static string FormatKoreanPrice(long amount)
    {
        if (amount == 0) return "0원";
        var parts = new List<string>();
        long eok = amount / 100_000_000;
        if (eok > 0) parts.Add($"{eok:N0}억");
        AddKoreanPart(parts, amount, 10_000_000, "천만");
        AddKoreanPart(parts, amount, 1_000_000, "백만");
        AddKoreanPart(parts, amount, 100_000, "십만");
        AddKoreanPart(parts, amount, 10_000, "만");
        AddKoreanPart(parts, amount, 1_000, "천");
        AddKoreanPart(parts, amount, 100, "백");
        AddKoreanPart(parts, amount, 10, "십");
        long won = amount % 10;
        if (won > 0) parts.Add($"{won}원");
        else parts[^1] += "원";
        return string.Join(" ", parts);
    }

    private static void AddKoreanPart(List<string> parts, long amount, long unit, string name)
    {
        long value = amount / unit % 10;
        if (value > 0) parts.Add($"{value}{name}");
    }

    private sealed record JeonUnit(string Name, long Multiplier);
}

public sealed record JeonSettingsChangedEventArgs(decimal Rate, long UnitMultiplier);

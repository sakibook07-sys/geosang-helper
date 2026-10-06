using System.Globalization;
using System.Windows.Controls;
using UserControl = System.Windows.Controls.UserControl;

namespace GeosangHub;

public partial class JeonRateCalculatorPane : UserControl
{
    private static readonly PriceUnit[] Units =
    [
        new("천", 1_000m), new("만", 10_000m), new("십만", 100_000m),
        new("백만", 1_000_000m), new("천만", 10_000_000m),
        new("억", 100_000_000m), new("십억", 1_000_000_000m)
    ];
    private bool _ready;

    public JeonRateCalculatorPane()
    {
        InitializeComponent();
        UnitCombo.ItemsSource = Units;
        UnitCombo.SelectedIndex = 5;
        _ready = true;
        Recalculate();
    }

    private void InputChanged(object sender, EventArgs e)
    {
        if (_ready) Recalculate();
    }

    private void Recalculate()
    {
        ValidationText.Text = string.Empty;
        if (!TryReadDecimal(RateInput.Text, out decimal rate) || rate <= 0)
        {
            ShowInvalid("지전비율을 0보다 큰 숫자로 입력해주세요.");
            return;
        }
        if (!TryReadDecimal(PriceInput.Text, out decimal priceNumber) || priceNumber < 0 || UnitCombo.SelectedItem is not PriceUnit unit)
        {
            ShowInvalid("아이템 가격을 0 이상의 숫자로 입력해주세요.");
            return;
        }

        decimal rawPrice;
        try { rawPrice = priceNumber * unit.Multiplier; }
        catch (OverflowException) { ShowInvalid("입력한 아이템 가격이 너무 큽니다."); return; }
        if (rawPrice > long.MaxValue)
        {
            ShowInvalid("입력한 아이템 가격이 너무 큽니다.");
            return;
        }

        long itemPrice = (long)decimal.Round(rawPrice, 0, MidpointRounding.AwayFromZero);
        decimal cashPrice = itemPrice / 100_000_000m * rate;
        FormattedPriceText.Text = FormatKoreanPrice(itemPrice) + $"  ({itemPrice:N0}원)";
        CashPriceText.Text = cashPrice < 1m && cashPrice > 0m
            ? $"약 {cashPrice:N2}원"
            : $"약 {decimal.Round(cashPrice, 0, MidpointRounding.AwayFromZero):N0}원";
    }

    private void ShowInvalid(string message)
    {
        FormattedPriceText.Text = "-";
        CashPriceText.Text = "-";
        ValidationText.Text = message;
    }

    private static bool TryReadDecimal(string text, out decimal value) =>
        decimal.TryParse(text.Replace(",", "").Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out value)
        || decimal.TryParse(text.Trim(), NumberStyles.Number, CultureInfo.CurrentCulture, out value);

    public static string FormatKoreanPrice(long amount)
    {
        if (amount == 0) return "0원";
        if (amount < 0) return "-" + FormatKoreanPrice(-amount);
        var parts = new List<string>();
        long eok = amount / 100_000_000;
        if (eok > 0) parts.Add($"{eok:N0}억");
        AddPart(parts, amount, 10_000_000, "천만");
        AddPart(parts, amount, 1_000_000, "백만");
        AddPart(parts, amount, 100_000, "십만");
        AddPart(parts, amount, 10_000, "만");
        AddPart(parts, amount, 1_000, "천");
        AddPart(parts, amount, 100, "백");
        AddPart(parts, amount, 10, "십");
        long won = amount % 10;
        if (won > 0) parts.Add($"{won}원");
        else if (parts.Count > 0) parts[^1] += "원";
        return string.Join(" ", parts);
    }

    private static void AddPart(List<string> parts, long amount, long unit, string name)
    {
        long value = amount / unit % 10;
        if (value > 0) parts.Add($"{value}{name}");
    }

    private sealed record PriceUnit(string Name, decimal Multiplier);
}

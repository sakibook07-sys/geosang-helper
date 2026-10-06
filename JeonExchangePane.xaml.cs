using System.Globalization;
using System.Windows.Controls;
using UserControl = System.Windows.Controls.UserControl;

namespace GeosangHub;

public partial class JeonExchangePane : UserControl
{
    private bool _ready;
    private bool _updating;
    private InputSide _lastInput = InputSide.Jeon;
    public event EventHandler<decimal>? RatioChanged;

    public JeonExchangePane()
    {
        InitializeComponent();
        _ready = true;
        ConvertFromJeon();
    }

    public void ConfigureRatio(decimal eokPerTenThousand)
    {
        _updating = true;
        RateInput.Text = (eokPerTenThousand > 0 ? eokPerTenThousand : 10_000m / 3000m).ToString("0.########", CultureInfo.InvariantCulture);
        _updating = false;
        Recalculate();
    }

    private void RateInput_Changed(object sender, TextChangedEventArgs e)
    {
        if (!_ready || _updating) return;
        Recalculate();
        if (TryRead(RateInput.Text, out decimal ratio) && ratio > 0)
            RatioChanged?.Invoke(this, ratio);
    }

    private void JeonInput_Changed(object sender, TextChangedEventArgs e)
    {
        if (!_ready || _updating) return;
        _lastInput = InputSide.Jeon;
        ConvertFromJeon();
    }

    private void CashInput_Changed(object sender, TextChangedEventArgs e)
    {
        if (!_ready || _updating) return;
        _lastInput = InputSide.Cash;
        ConvertFromCash();
    }

    private void Recalculate()
    {
        if (_lastInput == InputSide.Jeon) ConvertFromJeon(); else ConvertFromCash();
    }

    private void ConvertFromJeon()
    {
        ValidationText.Text = string.Empty;
        if (!TryGetRatio(out decimal ratio) || !TryRead(JeonInput.Text, out decimal eok) || eok < 0)
        {
            ShowInvalid("지전과 비율을 0 이상의 숫자로 입력해주세요.");
            return;
        }
        decimal itemWon;
        decimal cash;
        try { itemWon = eok * 100_000_000m; cash = eok / ratio * 10_000m; }
        catch (OverflowException) { ShowInvalid("입력 금액이 너무 큽니다."); return; }
        if (itemWon > long.MaxValue) { ShowInvalid("입력 금액이 너무 큽니다."); return; }
        long roundedWon = (long)decimal.Round(itemWon, 0, MidpointRounding.AwayFromZero);
        _updating = true;
        CashInput.Text = FormatCashInput(cash);
        _updating = false;
        JeonDetailText.Text = FormatKoreanPrice(roundedWon) + $"  ({roundedWon:N0}원)";
        CashDetailText.Text = FormatCashLabel(cash);
    }

    private void ConvertFromCash()
    {
        ValidationText.Text = string.Empty;
        if (!TryGetRatio(out decimal ratio) || !TryRead(CashInput.Text, out decimal cash) || cash < 0)
        {
            ShowInvalid("현금과 비율을 0 이상의 숫자로 입력해주세요.");
            return;
        }
        decimal eok;
        decimal itemWon;
        try { eok = cash / 10_000m * ratio; itemWon = eok * 100_000_000m; }
        catch (Exception ex) when (ex is DivideByZeroException or OverflowException) { ShowInvalid("입력 금액이 너무 큽니다."); return; }
        if (itemWon > long.MaxValue) { ShowInvalid("입력 금액이 너무 큽니다."); return; }
        long roundedWon = (long)decimal.Round(itemWon, 0, MidpointRounding.AwayFromZero);
        _updating = true;
        JeonInput.Text = eok.ToString("0.########", CultureInfo.InvariantCulture);
        _updating = false;
        JeonDetailText.Text = FormatKoreanPrice(roundedWon) + $"  ({roundedWon:N0}원)";
        CashDetailText.Text = FormatCashLabel(cash);
    }

    private bool TryGetRatio(out decimal ratio) => TryRead(RateInput.Text, out ratio) && ratio > 0;

    private static bool TryRead(string text, out decimal value) =>
        decimal.TryParse(text.Replace(",", "").Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out value)
        || decimal.TryParse(text.Trim(), NumberStyles.Number, CultureInfo.CurrentCulture, out value);

    private static string FormatCashInput(decimal cash) =>
        decimal.Round(cash, 2, MidpointRounding.AwayFromZero).ToString("#,0.##", CultureInfo.GetCultureInfo("ko-KR"));

    private static string FormatCashLabel(decimal cash) => $"{FormatCashInput(cash)}원";

    private void ShowInvalid(string message)
    {
        ValidationText.Text = message;
        JeonDetailText.Text = CashDetailText.Text = "-";
    }

    private static string FormatKoreanPrice(long amount)
    {
        if (amount == 0) return "0원";
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
        if (won > 0) parts.Add($"{won}원"); else parts[^1] += "원";
        return string.Join(" ", parts);
    }

    private static void AddPart(List<string> parts, long amount, long unit, string name)
    {
        long value = amount / unit % 10;
        if (value > 0) parts.Add($"{value}{name}");
    }

    private enum InputSide { Jeon, Cash }
}

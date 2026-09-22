namespace GeotaMarketViewer;

public enum MarketSort
{
    Latest,
    PriceLow,
    PriceHigh
}

public static class MarketUrlBuilder
{
    private const string BaseUrl = "https://geota.co.kr/gersang/yukeuijeon";

    public static Uri Build(int serverId, string? itemName, MarketSort sort, int page)
    {
        serverId = Math.Clamp(serverId, 1, 13);
        page = Math.Max(1, page);

        var parameters = new List<string>
        {
            $"serverId={serverId}",
            $"page={page}"
        };

        var trimmedName = itemName?.Trim();
        if (!string.IsNullOrWhiteSpace(trimmedName))
        {
            parameters.Add($"itemName={Uri.EscapeDataString(trimmedName)}");
        }

        if (sort == MarketSort.PriceLow)
        {
            parameters.Add("orderDirection=asc");
        }
        else if (sort == MarketSort.PriceHigh)
        {
            parameters.Add("orderDirection=desc");
        }

        return new Uri($"{BaseUrl}?{string.Join('&', parameters)}");
    }
}

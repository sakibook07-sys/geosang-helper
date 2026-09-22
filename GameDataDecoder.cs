using System.Buffers.Binary;
using System.Text;

namespace GeosangHelper;

internal static class GameDataDecoder
{
    static GameDataDecoder() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    internal static IReadOnlyList<MarketListing> DecodeMarket(GameServerMessage message)
    {
        byte[] data = message.Data;
        if (message.Opcode != 0x1F || data.Length < 11 || BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(7, 2)) != 5) return Array.Empty<MarketListing>();
        int count = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(9, 2));
        if (count < 1 || count > 100 || data.Length < 11 + count * 48) return Array.Empty<MarketListing>();
        var result = new List<MarketListing>(count);
        var korean = Encoding.GetEncoding(949);
        for (int index = 0; index < count; index++)
        {
            int offset = 11 + index * 48;
            long listingId = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset, 4));
            int itemId = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset + 4, 4));
            int quantity = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset + 8, 4));
            long price = BinaryPrimitives.ReadInt64LittleEndian(data.AsSpan(offset + 16, 8));
            int nameLength = Array.IndexOf(data, (byte)0, offset + 24, 24);
            if (nameLength < 0) nameLength = offset + 48;
            string seller = korean.GetString(data, offset + 24, nameLength - offset - 24).Trim();
            if (listingId <= 0 || itemId <= 0 || quantity <= 0 || price <= 0) continue;
            result.Add(new MarketListing
            {
                ListingId = listingId, ItemId = itemId, ItemName = ItemName(itemId), Quantity = quantity,
                UnitPrice = price, Seller = seller, ObservedAtUnixMs = message.ReceivedAt.ToUnixTimeMilliseconds()
            });
        }
        return result;
    }

    internal static string ItemName(int itemId) => itemId switch { 6340 => "철괴리의호리병", _ => $"아이템 #{itemId}" };
}

using Google.Protobuf.WellKnownTypes;

namespace Wallet.Extensions;

public static class DateTimeExtensions
{
    private static readonly Timestamp DefaultTimestamp = new();

    public static Timestamp ToGrpcTimestamp(this DateTime? value)
    {
        if (value == null) return DefaultTimestamp;

        var utcValue = value.Value.Kind == DateTimeKind.Utc 
            ? value.Value 
            : value.Value.ToUniversalTime();
            
        return Timestamp.FromDateTime(utcValue);
    }

    public static Timestamp ToGrpcTimestamp(this DateTime value)
    {
        var utcValue = value.Kind == DateTimeKind.Utc 
            ? value 
            : value.ToUniversalTime();
            
        return Timestamp.FromDateTime(utcValue);
    }
}
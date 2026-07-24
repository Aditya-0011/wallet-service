using System.Globalization;
using GrpcDecimal = Google.Type.Decimal;

namespace Wallet.Extensions;

public static class DecimalExtensions
{
    public static GrpcDecimal ToGrpcDecimal(this decimal? value)
    {
        if (value is null or 0m)
        {
            return new GrpcDecimal { Value = "0" };
        }

        return new GrpcDecimal
        {
            Value = value.Value.ToString(CultureInfo.InvariantCulture)
        };
    }

    public static GrpcDecimal ToGrpcDecimal(this decimal value)
    {
        if (value == 0m)
        {
            return new GrpcDecimal { Value = "0" };
        }

        return new GrpcDecimal
        {
            Value = value.ToString(CultureInfo.InvariantCulture)
        };
    }
}

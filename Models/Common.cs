namespace Wallet.Models;

public sealed class DbFuncResponse(short outcode)
{
    public short Outcode { get; } = outcode;
}
namespace Wallet.Utils;

public static class Timeout
{
    public static readonly TimeSpan Duration = TimeSpan.FromSeconds(5);

    public static CancellationTokenSource WithDeadline(TimeSpan duration, CancellationToken token)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
        cts.CancelAfter(duration);
        return cts;
    }
}

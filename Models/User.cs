namespace Wallet.Models;

public sealed class UserSummary(
    decimal allIncome,
    decimal allExpense,
    decimal monthIncome,
    decimal monthExpense,
    decimal weekIncome,
    decimal weekExpense,
    DateTime startDate)
{
    public decimal AllIncome { get; } = allIncome;
    public decimal AllExpense { get; } = allExpense;
    public decimal MonthIncome { get; } = monthIncome;
    public decimal MonthExpense { get; } = monthExpense;
    public decimal WeekIncome { get; } = weekIncome;
    public decimal WeekExpense { get; } = weekExpense;
    public DateTime StartDate { get; } = startDate;
}

public sealed class InitiateUserResult(DateTime startDate, short outcode)
{
    public DateTime StartDate { get; } = startDate;
    public short Outcode { get; } = outcode;
}
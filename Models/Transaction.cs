namespace Wallet.Models;

public sealed class Transaction(
    int id,
    string description,
    decimal amount,
    int categoryId,
    string categoryName,
    string categoryIcon,
    short categoryType,
    DateTime createdAt,
    DateTime updatedAt)
{
    public int Id { get; } = id;
    public string Description { get; } = description;
    public decimal Amount { get; } = amount;
    public CategorySummary Category { get; } = new(categoryId, categoryName, categoryIcon, categoryType);
    public DateTime CreatedAt { get; } = createdAt;
    public DateTime UpdatedAt { get; } = updatedAt;
}

public sealed class TransactionsUpdateHistory(
    int id,
    int previousCategoryId,
    string previousCategoryName,
    string previousCategoryIcon,
    short previousCategoryType,
    int currentCategoryId,
    string currentCategoryName,
    string currentCategoryIcon,
    short currentCategoryType,
    string previousDescription,
    string currentDescription,
    decimal previousAmount,
    decimal currentAmount,
    DateTime createdAt)
{
    public int Id { get; } = id;

    public CategorySummary PreviousCategory { get; } = new(previousCategoryId, previousCategoryName,
        previousCategoryIcon, previousCategoryType);

    public CategorySummary CurrentCategory { get; } =
        new(currentCategoryId, currentCategoryName, currentCategoryIcon, currentCategoryType);

    public string PreviousDescription { get; } = previousDescription;
    public string CurrentDescription { get; } = currentDescription;
    public decimal PreviousAmount { get; } = previousAmount;
    public decimal CurrentAmount { get; } = currentAmount;
    public DateTime CreatedAt { get; } = createdAt;
}
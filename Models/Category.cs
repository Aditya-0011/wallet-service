using Contracts.Wallet;

namespace Wallet.Models;

public sealed class CategorySummary(
    int id,
    string name,
    string icon,
    CategoryType type)
{
    public int Id { get; } = id;
    public string Name { get; } = name;
    public string Icon { get; } = icon;
    public CategoryType Type { get; } = type;
}

public sealed class Category(
    int id,
    string name,
    string icon,
    CategoryType type,
    DateTime createdAt,
    DateTime updatedAt)
{
    public CategorySummary Summary { get; } = new(id, name, icon, type);
    public DateTime CreatedAt { get; } = createdAt;
    public DateTime UpdatedAt { get; } = updatedAt;
}
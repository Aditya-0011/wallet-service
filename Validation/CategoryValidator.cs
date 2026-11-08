using Contracts.Grpcx;
using Contracts.Wallet;
using Google.Protobuf;

namespace Wallet.Validation;

public sealed class CategoryValidator : IRequestValidator
{
    public bool CanValidate(IMessage message) => message is CreateCategoryRequest or UpdateCategoryRequest;

    public IEnumerable<string> Validate(IMessage message) => message switch
    {
        CreateCategoryRequest req => Common(req.UserId, req.Name, req.Icon, req.Type),
        UpdateCategoryRequest req => (req.Id <= 0 ? ["id must be greater than 0"] : Array.Empty<string>())
            .Concat(Common(req.UserId, req.Name, req.Icon, req.Type)),
        _ => []
    };

    private static IEnumerable<string> Common(int userId, string name, string icon, CategoryType type)
    {
        if (userId <= 0) yield return "user_id must be greater than 0";
        if (string.IsNullOrWhiteSpace(name) || name.Length > 255) yield return "name length must be between 1 and 255";
        if (string.IsNullOrWhiteSpace(icon) || icon.Length > 255) yield return "icon length must be between 1 and 255";
        else if (!IsValidIcon(icon)) yield return "icon does not match pattern";
        if (!Enum.IsDefined(type) || type == CategoryType.Unspecified) yield return "type must be a defined enum value";
    }

    private static bool IsValidIcon(string icon) =>
        icon.EnumerateRunes().All(rune => rune.Value is >= 0x1F300 and <= 0x1FAD6
            or >= 0x2600 and <= 0x26FF
            or >= 0x2700 and <= 0x27BF
            or >= 0x1F1E6 and <= 0x1F1FF
            or 0x200D or 0xFE0F);
}

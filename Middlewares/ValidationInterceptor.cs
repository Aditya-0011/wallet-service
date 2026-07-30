using Grpc.Core;
using Grpc.Core.Interceptors;
using Google.Protobuf;
using ProtoValidate;
using Contracts.Wallet;

namespace Wallet.Middlewares;

public sealed class ValidationInterceptor(Validator validator) : Interceptor
{
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        if (request is not IMessage message) return await continuation(request, context);

        try
        {
            switch (message)
            {
                case CreateCategoryRequest createReq:
                    ValidateCreateCategory(createReq);
                    break;
                case UpdateCategoryRequest updateReq:
                    ValidateUpdateCategory(updateReq);
                    break;
                default:
                    validator.Validate(message, failFast: true);
                    break;
            }
        }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, ex.Message));
        }

        return await continuation(request, context);
    }

    private static bool IsValidIcon(string icon)
    {
        return !string.IsNullOrEmpty(icon) && icon.EnumerateRunes().Select(rune => rune.Value).Select(v =>
            v is >= 0x1F300 and <= 0x1FAD6 or >= 0x2600 and <= 0x26FF or >= 0x2700 and <= 0x27BF
                or >= 0x1F1E6 and <= 0x1F1FF or 0x200D or 0xFE0F).All(valid => valid);
    }

    private static void ValidateCreateCategory(CreateCategoryRequest req)
    {
        if (req.UserId <= 0) throw new ArgumentException("user_id must be greater than 0");
        if (string.IsNullOrWhiteSpace(req.Name) || req.Name.Length > 255)
            throw new ArgumentException("name length must be between 1 and 255");
        if (string.IsNullOrWhiteSpace(req.Icon) || req.Icon.Length > 255)
            throw new ArgumentException("icon length must be between 1 and 255");
        if (!IsValidIcon(req.Icon))
            throw new ArgumentException("icon does not match pattern");
        if (!Enum.IsDefined(req.Type)) throw new ArgumentException("type must be a defined enum value");
    }

    private static void ValidateUpdateCategory(UpdateCategoryRequest req)
    {
        if (req.UserId <= 0) throw new ArgumentException("user_id must be greater than 0");
        if (req.Id <= 0) throw new ArgumentException("id must be greater than 0");
        if (string.IsNullOrWhiteSpace(req.Name) || req.Name.Length > 255)
            throw new ArgumentException("name length must be between 1 and 255");
        if (string.IsNullOrWhiteSpace(req.Icon) || req.Icon.Length > 255)
            throw new ArgumentException("icon length must be between 1 and 255");
        if (!IsValidIcon(req.Icon))
            throw new ArgumentException("icon does not match pattern");
        if (!Enum.IsDefined(req.Type)) throw new ArgumentException("type must be a defined enum value");
    }
}

using Grpc.Core;
using Grpc.Core.Interceptors;
using Google.Protobuf;
using ProtoValidate;

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
            validator.Validate(message, failFast: true);
        }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, ex.Message));
        }

        return await continuation(request, context);
    }
}

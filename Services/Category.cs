using Contracts.Wallet;
using Dapper;
using Grpc.Core;
using Npgsql;
using Wallet.Extensions;
using Wallet.Models;
using Contracts.Grpcx;

namespace Wallet.Services;

public sealed partial class Finance(NpgsqlDataSource dataSource, ServiceOptions options)
    : FinanceService.FinanceServiceBase
{
    public override async Task<GetCategoriesResponse> GetCategories(SimpleRequest request, ServerCallContext context)
    {
        using var cts = context.CreateTimeout(options);
        await using var connection = await dataSource.OpenConnectionAsync(cts.Token);

        const string query = """
                             select id, name, icon, type, createdAt, updatedAt
                             from wallet.category
                             where userId = @p_userId
                             order by updatedAt
                             """;

        var result = await connection.QueryAsync<Models.Category>(
            new CommandDefinition(query, new { p_userId = request.UserId }, cancellationToken: cts.Token));

        var response = new GetCategoriesResponse();
        response.Categories.AddRange(result.Select(x => new Contracts.Wallet.Category
        {
            Id = x.Summary.Id,
            Name = x.Summary.Name,
            Icon = x.Summary.Icon,
            Type = x.Summary.Type,
            CreatedAt = x.CreatedAt.ToGrpcTimestamp(),
            UpdatedAt = x.UpdatedAt.ToGrpcTimestamp()
        }));
        return response;
    }

    public override async Task<SimpleResponse> CreateCategory(CreateCategoryRequest request, ServerCallContext context)
    {
        var outcode = await CallFunction(context,
            "select outcode from wallet.edit_category(p_id := -1, p_userId := @p_userId, p_name := @p_name, p_icon := @p_icon, p_type := @p_type)",
            new { p_userId = request.UserId, p_name = request.Name, p_icon = request.Icon, p_type = (int)request.Type });

        return outcode switch
        {
            -1 => new SimpleResponse { Message = "Category created successfully!" },
            0 => throw new RpcException(new Status(StatusCode.AlreadyExists, "Category already exists")),
            _ => throw DatabaseError(outcode)
        };
    }

    public override async Task<SimpleResponse> UpdateCategory(UpdateCategoryRequest request, ServerCallContext context)
    {
        var outcode = await CallFunction(context,
            "select outcode from wallet.edit_category(p_id := @p_id, p_userId := @p_userId, p_name := @p_name, p_icon := @p_icon, p_type := @p_type)",
            new
            {
                p_id = request.Id, p_userId = request.UserId, p_name = request.Name, p_icon = request.Icon,
                p_type = (int)request.Type
            });

        return outcode switch
        {
            -1 => new SimpleResponse { Message = "Category updated successfully!" },
            0 => throw new RpcException(new Status(StatusCode.AlreadyExists, "Category already exists")),
            1 => throw new RpcException(new Status(StatusCode.NotFound, "Category not found")),
            3 => throw new RpcException(new Status(StatusCode.FailedPrecondition,
                "Unable to update category, please delete transactions related to this category")),
            _ => throw DatabaseError(outcode)
        };
    }

    public override async Task<SimpleResponse> DeleteCategory(DeleteRequest request, ServerCallContext context)
    {
        var outcode = await CallFunction(context,
            "select outcode from wallet.delete_category(p_id := @p_id, p_userId := @p_userId)",
            new { p_id = request.Id, p_userId = request.UserId });

        return outcode switch
        {
            -1 => new SimpleResponse { Message = "Category deleted successfully!" },
            1 => throw new RpcException(new Status(StatusCode.NotFound, "Category not found")),
            3 => throw new RpcException(new Status(StatusCode.FailedPrecondition,
                "Unable to delete category, please delete transactions related to this category")),
            _ => throw DatabaseError(outcode)
        };
    }

    private async Task<short> CallFunction(ServerCallContext context, string sql, object parameters)
    {
        using var cts = context.CreateTimeout(options);
        await using var connection = await dataSource.OpenConnectionAsync(cts.Token);
        var result = await connection.QuerySingleAsync<DbFuncResponse>(
            new CommandDefinition(sql, parameters, cancellationToken: cts.Token));
        return result.Outcode;
    }

    private static RpcException DatabaseError(short outcode) =>
        new(new Status(StatusCode.Internal, $"Database error (outcode {outcode})"));
}

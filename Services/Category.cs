using Grpc.Core;
using Npgsql;
using Dapper;
using Contracts.Wallet;
using Wallet.Extensions;
using Wallet.Models;
using Timeout = Wallet.Utils.Timeout;

namespace Wallet.Services;

public sealed class Category(ILogger<Category> logger, NpgsqlDataSource dataSource) : FinanceService.FinanceServiceBase
{
    public override async Task<GetCategoriesResponse> GetCategories(SimpleRequest request, ServerCallContext context)
    {
        try
        {
            using var ctx = Timeout.WithDeadline(Timeout.Duration, context.CancellationToken);
            await using var connection = await dataSource.OpenConnectionAsync(ctx.Token);

            const string query = """
                                 select
                                    id, 
                                    name,
                                    icon,
                                    type,
                                    createdAt,
                                    updatedAt
                                  from wallet.category
                                  where userId = @p_userId
                                  order by updatedAt
                                 """;

            var result = await connection.QueryAsync<Models.Category>(query, new { p_userId = request.UserId });

            GetCategoriesResponse response = new();
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
        catch (RpcException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error retrieving category list for {UserId}", request.UserId);
            throw new RpcException(new Status(StatusCode.Internal,
                "An internal error occurred processing the wallet request"));
        }
    }

    public override async Task<SimpleResponse> CreateCategory(CreateCategoryRequest request, ServerCallContext context)
    {
        try
        {
            using var ctx = Timeout.WithDeadline(Timeout.Duration, context.CancellationToken);
            await using var connection = await dataSource.OpenConnectionAsync(ctx.Token);

            var result = await connection.QuerySingleAsync<DbFuncResponse>(
                "select outcode from wallet.edit_category(p_id := -1, p_userId := @p_userId, p_name := @p_name, p_icon := @p_icon, p_type := @p_type)",
                new
                {
                    p_userId = request.UserId,
                    p_name = request.Name,
                    p_icon = request.Icon,
                    p_type = request.Type
                });

            return result.Outcode switch
            {
                -1 => new SimpleResponse { Message = "Category created successfully!" },
                0 => throw new RpcException(new Status(StatusCode.AlreadyExists, "Category already exists")),
                2 => throw new RpcException(new Status(StatusCode.Internal, "Database error")),
                _ => throw new RpcException(new Status(StatusCode.Internal, "Unexpected Server error"))
            };
        }
        catch (RpcException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error creating category for {UserId}", request.UserId);
            throw new RpcException(new Status(StatusCode.Internal,
                "An internal error occurred processing the wallet request"));
        }
    }

    public override async Task<SimpleResponse> UpdateCategory(UpdateCategoryRequest request, ServerCallContext context)
    {
        try
        {
            using var ctx = Timeout.WithDeadline(Timeout.Duration, context.CancellationToken);
            await using var connection = await dataSource.OpenConnectionAsync(ctx.Token);

            var result = await connection.QuerySingleAsync<DbFuncResponse>(
                "select outcode from wallet.edit_category(p_id := @p_id, p_userId := @p_userId, p_name := @p_name, p_icon := @p_icon, p_type := @p_type)",
                new
                {
                    p_id = request.Id,
                    p_userId = request.UserId,
                    p_name = request.Name,
                    p_icon = request.Icon,
                    p_type = request.Type
                });

            return result.Outcode switch
            {
                -1 => new SimpleResponse { Message = "Category updated successfully!" },
                0 => throw new RpcException(new Status(StatusCode.AlreadyExists, "Category already exists")),
                1 => throw new RpcException(new Status(StatusCode.NotFound, "Category not found")),
                2 => throw new RpcException(new Status(StatusCode.Internal, "Database error")),
                3 => throw new RpcException(new Status(StatusCode.FailedPrecondition,
                    "Unable to update category, please delete transactions related to this category")),
                _ => throw new RpcException(new Status(StatusCode.Internal, "Unexpected Server error"))
            };
        }
        catch (RpcException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error updating category {CategoryId} for user {UserId}", request.Id,
                request.UserId);
            throw new RpcException(new Status(StatusCode.Internal,
                "An internal error occurred processing the wallet request"));
        }
    }

    public override async Task<SimpleResponse> DeleteCategory(DeleteRequest request, ServerCallContext context)
    {
        try
        {
            using var ctx = Timeout.WithDeadline(Timeout.Duration, context.CancellationToken);
            await using var connection = await dataSource.OpenConnectionAsync(ctx.Token);

            var result = await connection.QuerySingleAsync<DbFuncResponse>(
                "select outcode from wallet.delete_category(p_id := @p_id, p_userId := @p_userId)",
                new
                {
                    p_id = request.Id,
                    p_userId = request.UserId
                });

            return result.Outcode switch
            {
                -1 => new SimpleResponse { Message = "Category deleted successfully!" },
                1 => throw new RpcException(new Status(StatusCode.NotFound, "Category not found")),
                2 => throw new RpcException(new Status(StatusCode.Internal, "Database error")),
                3 => throw new RpcException(new Status(StatusCode.FailedPrecondition,
                    "Unable to delete category, please delete transactions related to this category")),
                _ => throw new RpcException(new Status(StatusCode.Internal, "Unexpected Server error"))
            };
        }
        catch (RpcException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error deleting category {CategoryId} for user {UserId}", request.Id,
                request.UserId);
            throw new RpcException(new Status(StatusCode.Internal,
                "An internal error occurred processing the wallet request"));
        }
    }
}
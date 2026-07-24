using Grpc.Core;
using Npgsql;
using Dapper;
using Contracts.Wallet;
using Wallet.Extensions;
using Wallet.Models;
using Timeout = Wallet.Utils.Timeout;

namespace Wallet.Services;

public sealed class Transaction(ILogger<Transaction> logger, NpgsqlDataSource dataSource)
    : FinanceService.FinanceServiceBase
{
    public override async Task<GetTransactionsResponse> GetTransactions(GetTransactionsRequest request,
        ServerCallContext context)
    {
        try
        {
            using var ctx = Timeout.WithDeadline(Timeout.Duration, context.CancellationToken);
            await using var connection = await dataSource.OpenConnectionAsync(ctx.Token);

            var limit = int.Min(int.Max(request.Limit, 25), 50);
            var page = int.Max(request.Page, 1);
            var offset = (page - 1) * limit;

            var whereClause = "where t.userId = @p_userId";

            var parameters = new DynamicParameters();
            parameters.Add("p_userId", request.UserId);
            parameters.Add("p_limit", limit);
            parameters.Add("p_offset", offset);

            if (request.HasType)
            {
                whereClause += " and c.type = @p_type";
                parameters.Add("p_type", request.Type);
            }

            if (request.HasCategoryId)
            {
                whereClause += " and t.categoryId = @p_categoryId";
                parameters.Add("p_categoryId", request.CategoryId);
            }

            if (request.StartDate is not null && request.EndDate is not null)
            {
                whereClause += " and t.createdAt between @p_startDate and @p_endDate";
                parameters.Add("p_startDate", request.StartDate.ToDateTime());
                parameters.Add("p_endDate", request.EndDate.ToDateTime());
            }

            var query = $"""
                           select
                               t.id,
                               t.description,
                               t.amount,
                               t.createdAt,
                               t.updatedAt,
                               c.id as categoryId,
                               c.name as categoryName,
                               c.icon as categoryIcon,
                               c.type as categoryType
                           from wallet.transaction t
                           inner join wallet.category c on t.categoryId = c.id
                           {whereClause}
                           order by t.createdAt desc
                           limit @p_limit offset @p_offset;                             
                         """;

            var result = await connection.QueryAsync<Models.Transaction>(query, parameters);

            GetTransactionsResponse response = new();
            response.Transactions.AddRange(result.Select(x => new Contracts.Wallet.Transaction
            {
                Id = x.Id,
                Description = x.Description,
                Amount = x.Amount.ToGrpcDecimal(),
                CreatedAt = x.CreatedAt.ToGrpcTimestamp(),
                UpdatedAt = x.UpdatedAt.ToGrpcTimestamp(),
                Category = new Contracts.Wallet.CategorySummary
                {
                    Id = x.Category.Id,
                    Name = x.Category.Name,
                    Icon = x.Category.Icon,
                    Type = x.Category.Type
                }
            }));

            return response;
        }
        catch (RpcException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Unexpected error retrieving transactions for user {UserId}",
                request.UserId);
            throw new RpcException(new Status(StatusCode.Internal,
                "An internal error occurred processing the wallet request"));
        }
    }

    public override async Task<GetTransactionsUpdateHistoryResponse> GetTransactionsUpdateHistory(
        GetTransactionsUpdateHistoryRequest request,
        ServerCallContext context)
    {
        try
        {
            using var ctx = Timeout.WithDeadline(Timeout.Duration, context.CancellationToken);
            await using var connection = await dataSource.OpenConnectionAsync(ctx.Token);

            const string query = """
                                 select
                                    t.id,
                                    pc.id as previousCategoryId,
                                    pc.name as previousCategoryName,
                                    pc.icon as previousCategoryIcon,
                                    pc.type as previousCategoryType,
                                    cc.id as currentCategoryId,
                                    cc.name as currentCategoryName,
                                    cc.icon as currentCategoryIcon,
                                    cc.type as currentCategoryType,
                                    t.previousDescription,
                                    t.currentDescription,
                                    t.previousAmount,
                                    t.currentAmount,
                                    t.createdAt
                                 from wallet.transaction_update t
                                 left join wallet.category pc on t.previousCategoryId = pc.id
                                 left join wallet.category cc on t.currentCategoryId = cc.id
                                 where t.transactionId = @p_transactionId and t.userId = @p_userId
                                 order by t.createdAt
                                 """;

            var result = await connection.QueryAsync<Models.TransactionsUpdateHistory>(query,
                new { p_transactionId = request.TransactionId, p_userId = request.UserId });

            GetTransactionsUpdateHistoryResponse response = new();
            response.History.AddRange(result.Select(x =>
                new Contracts.Wallet.TransactionsUpdateHistory
                {
                    Id = x.Id,
                    PreviousCategory = new Contracts.Wallet.CategorySummary
                    {
                        Id = x.PreviousCategory.Id,
                        Name = x.PreviousCategory.Name,
                        Icon = x.PreviousCategory.Icon,
                        Type = x.PreviousCategory.Type
                    },
                    CurrentCategory = new Contracts.Wallet.CategorySummary
                    {
                        Id = x.CurrentCategory.Id,
                        Name = x.CurrentCategory.Name,
                        Icon = x.CurrentCategory.Icon,
                        Type = x.CurrentCategory.Type
                    },
                    PreviousDescription = x.PreviousDescription,
                    CurrentDescription = x.CurrentDescription,
                    PreviousAmount = x.PreviousAmount.ToGrpcDecimal(),
                    CurrentAmount = x.CurrentAmount.ToGrpcDecimal(),
                    CreatedAt = x.CreatedAt.ToGrpcTimestamp()
                }));

            return response;
        }
        catch (RpcException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Unexpected error retrieving transaction update history for transaction {TransactionId} user {UserId}",
                request.TransactionId, request.UserId);
            throw new RpcException(new Status(StatusCode.Internal,
                "An internal error occurred processing the wallet request"));
        }
    }

    public override async Task<SimpleResponse> CreateTransaction(CreateTransactionRequest request,
        ServerCallContext context)
    {
        try
        {
            using var ctx = Timeout.WithDeadline(Timeout.Duration, context.CancellationToken);
            await using var connection = await dataSource.OpenConnectionAsync(ctx.Token);

            var result = await connection.QuerySingleAsync<DbFuncResponse>(
                "select outcode from wallet.edit_transaction(p_id := -1, p_userId := @p_userId, p_categoryId := @p_categoryId, p_description := @p_description, p_amount := @p_amount)",
                new
                {
                    p_userId = request.UserId,
                    p_categoryId = request.CategoryId,
                    p_description = request.Description,
                    p_amount = request.Amount
                });

            return result.Outcode switch
            {
                -1 => new SimpleResponse { Message = "Transaction created successfully!" },
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
            logger.LogError(ex, "Unexpected error creating transaction for {UserId}", request.UserId);
            throw new RpcException(new Status(StatusCode.Internal,
                "An internal error occurred processing the wallet request"));
        }
    }

    public override async Task<SimpleResponse> UpdateTransaction(UpdateTransactionRequest request,
        ServerCallContext context)
    {
        try
        {
            using var ctx = Timeout.WithDeadline(Timeout.Duration, context.CancellationToken);
            await using var connection = await dataSource.OpenConnectionAsync(ctx.Token);

            var result = await connection.QuerySingleAsync<DbFuncResponse>(
                "select outcode from wallet.edit_transaction(p_id := @p_id, p_userId := @p_userId, p_categoryId := @p_categoryId, p_description := @p_description, p_amount := @p_amount)",
                new
                {
                    p_id = request.Id,
                    p_userId = request.UserId,
                    p_categoryId = request.CategoryId,
                    p_description = request.Description,
                    p_amount = request.Amount
                });

            return result.Outcode switch
            {
                -1 => new SimpleResponse { Message = "Transaction updated successfully!" },
                1 => throw new RpcException(new Status(StatusCode.NotFound, "Transaction not found")),
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
            logger.LogError(ex, "Unexpected error updating transaction {TransactionId} for user {UserId}", request.Id,
                request.UserId);
            throw new RpcException(new Status(StatusCode.Internal,
                "An internal error occurred processing the wallet request"));
        }
    }

    public override async Task<SimpleResponse> DeleteTransaction(DeleteRequest request, ServerCallContext context)
    {
        try
        {
            using var ctx = Timeout.WithDeadline(Timeout.Duration, context.CancellationToken);
            await using var connection = await dataSource.OpenConnectionAsync(ctx.Token);

            var result = await connection.QuerySingleAsync<DbFuncResponse>(
                "select outcode from wallet.delete_transaction(p_id := @p_id, p_userId := @p_userId)",
                new
                {
                    p_id = request.Id,
                    p_userId = request.UserId
                });

            return result.Outcode switch
            {
                -1 => new SimpleResponse { Message = "Transaction deleted successfully!" },
                1 => throw new RpcException(new Status(StatusCode.NotFound, "Transaction not found")),
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
            logger.LogError(ex, "Unexpected error deleting transaction {TransactionId} for user {UserId}", request.Id,
                request.UserId);
            throw new RpcException(new Status(StatusCode.Internal,
                "An internal error occurred processing the wallet request"));
        }
    }
}
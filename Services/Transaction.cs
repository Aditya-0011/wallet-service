using Grpc.Core;
using System.Data;
using System.Text.Json;
using Google.Protobuf;
using Dapper;
using SpreadCheetah;
using Contracts.Wallet;
using Wallet.Extensions;
using Wallet.Models;
using Contracts.Grpcx;
using Wallet.Utils;

namespace Wallet.Services;

public sealed partial class Finance
{
    public override async Task<GetTransactionsResponse> GetTransactions(GetTransactionsRequest request,
        ServerCallContext context)
    {
        using var cts = context.CreateTimeout(options);
        await using var connection = await dataSource.OpenConnectionAsync(cts.Token);

        var limit = int.Clamp(request.Limit, 1, 50);
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
                           c.id as categoryId,
                           c.name as categoryName,
                           c.icon as categoryIcon,
                           c.type as categoryType,
                           t.createdAt,
                           t.updatedAt
                       from wallet.transaction t
                       inner join wallet.category c on t.categoryId = c.id
                       {whereClause}
                       order by t.createdAt desc
                       limit @p_limit offset @p_offset;                             
                     """;

        var result = await connection.QueryAsync<Models.Transaction>(
            new CommandDefinition(query, parameters, cancellationToken: cts.Token));

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

    public override async Task<GetTransactionsUpdateHistoryResponse> GetTransactionsUpdateHistory(
        GetTransactionsUpdateHistoryRequest request,
        ServerCallContext context)
    {
        using var cts = context.CreateTimeout(options);
        await using var connection = await dataSource.OpenConnectionAsync(cts.Token);

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

        var result = await connection.QueryAsync<Models.TransactionsUpdateHistory>(new CommandDefinition(query,
            new { p_transactionId = request.TransactionId, p_userId = request.UserId },
            cancellationToken: cts.Token));

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

    public override async Task<SimpleResponse> CreateTransaction(CreateTransactionRequest request,
        ServerCallContext context)
    {
        var outcode = await CallFunction(context,
            "select outcode from wallet.edit_transaction(p_id := -1, p_userId := @p_userId, p_categoryId := @p_categoryId, p_description := @p_description, p_amount := @p_amount)",
            new
            {
                p_userId = request.UserId,
                p_categoryId = request.CategoryId,
                p_description = request.Description,
                p_amount = request.Amount.ToDecimal()
            });

        return outcode switch
        {
            -1 => new SimpleResponse { Message = "Transaction created successfully!" },
            1 => throw new RpcException(new Status(StatusCode.NotFound, "Category not found")),
            _ => throw DatabaseError(outcode)
        };
    }

    public override async Task<SimpleResponse> UpdateTransaction(UpdateTransactionRequest request,
        ServerCallContext context)
    {
        var outcode = await CallFunction(context,
            "select outcode from wallet.edit_transaction(p_id := @p_id, p_userId := @p_userId, p_categoryId := @p_categoryId, p_description := @p_description, p_amount := @p_amount)",
            new
            {
                p_id = request.Id,
                p_userId = request.UserId,
                p_categoryId = request.CategoryId,
                p_description = request.Description,
                p_amount = request.Amount.ToDecimal()
            });

        return outcode switch
        {
            -1 => new SimpleResponse { Message = "Transaction updated successfully!" },
            1 => throw new RpcException(new Status(StatusCode.NotFound, "Transaction not found")),
            _ => throw DatabaseError(outcode)
        };
    }

    public override async Task<SimpleResponse> DeleteTransaction(DeleteRequest request, ServerCallContext context)
    {
        var outcode = await CallFunction(context,
            "select outcode from wallet.delete_transaction(p_id := @p_id, p_userId := @p_userId)",
            new
            {
                p_id = request.Id,
                p_userId = request.UserId
            });

        return outcode switch
        {
            -1 => new SimpleResponse { Message = "Transaction deleted successfully!" },
            1 => throw new RpcException(new Status(StatusCode.NotFound, "Transaction not found")),
            _ => throw DatabaseError(outcode)
        };
    }


    public override async Task<ExportTransactionsResponse> ExportTransactions(ExportTransactionsRequest request,
        ServerCallContext context)
    {
        using var cts = context.CreateTimeout(options);
        await using var connection = await dataSource.OpenConnectionAsync(cts.Token);

        const string query = """
                             select 
                                 t.id, t.description, t.amount, 
                                 c.name as categoryName, c.type as categoryType, 
                                 t.createdAt, t.updatedAt,
                                 coalesce(
                                     jsonb_agg(
                                         jsonb_build_object(
                                             'transactionId', tu.transactionId,
                                             'previousDescription', tu.previousDescription,
                                             'currentDescription', tu.currentDescription,
                                             'previousAmount', tu.previousAmount,
                                             'currentAmount', tu.currentAmount,
                                             'updateDate', tu.createdAt,
                                             'previousCategoryName', pc.name,
                                             'currentCategoryName', cc.name
                                         ) order by tu.createdAt desc
                                     ) filter (where tu.transactionId is not null), '[]'::jsonb
                                 ) as updates
                             from wallet.transaction t
                             inner join wallet.category c on t.categoryId = c.id
                             left join wallet.transaction_update tu on t.id = tu.transactionId
                             left join wallet.category pc on tu.previousCategoryId = pc.id
                             left join wallet.category cc on tu.currentCategoryId = cc.id
                             where t.userId = @p_userId 
                               and t.createdAt >= @p_startDate and t.createdAt <= @p_endDate
                             group by t.id, c.id
                             order by t.createdAt desc;
                             """;

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = query;
        cmd.Parameters.AddWithValue("p_userId", request.UserId);
        cmd.Parameters.AddWithValue("p_startDate", request.StartDate.ToDateTime());
        cmd.Parameters.AddWithValue("p_endDate", request.EndDate.ToDateTime());

        await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cts.Token);

        using var memoryStream = new MemoryStream();
        await using (var spreadsheet =
                     await Spreadsheet.CreateNewAsync(memoryStream, cancellationToken: cts.Token))
        {
            await spreadsheet.StartWorksheetAsync("Transactions", token: cts.Token);
            var cells = new DataCell[7];
            cells[0] = new DataCell("Id");
            cells[1] = new DataCell("Description");
            cells[2] = new DataCell("Amount");
            cells[3] = new DataCell("Category");
            cells[4] = new DataCell("Type");
            cells[5] = new DataCell("Created At");
            cells[6] = new DataCell("Updated At");
            await spreadsheet.AddRowAsync(cells, cts.Token);

            var allUpdates = new List<TransactionUpdateHistoryRow>();

            var indianTz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");

            while (await reader.ReadAsync(cts.Token))
            {
                cells[0] = new DataCell(reader.GetInt32(0));
                cells[1] = new DataCell(reader.GetString(1));
                cells[2] = new DataCell((double)reader.GetDecimal(2));
                cells[3] = new DataCell(reader.GetString(3));
                cells[4] = new DataCell(reader.GetInt16(4) == 1 ? "Income" : "Expense");
                cells[5] = new DataCell(TimeZoneInfo.ConvertTimeFromUtc(reader.GetDateTime(5), indianTz));
                cells[6] = new DataCell(TimeZoneInfo.ConvertTimeFromUtc(reader.GetDateTime(6), indianTz));
                await spreadsheet.AddRowAsync(cells, cts.Token);

                var jsonString = reader.GetString(7);
                if (jsonString == "[]") continue;
                var updates = JsonSerializer.Deserialize(jsonString, WalletJsonSerializerContext.Default.ListTransactionUpdateHistoryRow);
                if (updates != null)
                {
                    allUpdates.AddRange(updates);
                }
            }

            await spreadsheet.StartWorksheetAsync("History", token: cts.Token);

            var historyCells = new DataCell[8];
            historyCells[0] = new DataCell("Transaction Id");
            historyCells[1] = new DataCell("Previous Description");
            historyCells[2] = new DataCell("Current Description");
            historyCells[3] = new DataCell("Previous Amount");
            historyCells[4] = new DataCell("Current Amount");
            historyCells[5] = new DataCell("Update Date");
            historyCells[6] = new DataCell("Previous Category");
            historyCells[7] = new DataCell("Current Category");
            await spreadsheet.AddRowAsync(historyCells, cts.Token);

            foreach (var update in allUpdates)
            {
                historyCells[0] = new DataCell(update.TransactionId);
                historyCells[1] = new DataCell(update.PreviousDescription);
                historyCells[2] = new DataCell(update.CurrentDescription);
                historyCells[3] = new DataCell((double)update.PreviousAmount);
                historyCells[4] = new DataCell((double)update.CurrentAmount);
                historyCells[5] = new DataCell(TimeZoneInfo.ConvertTime(update.UpdateDate.UtcDateTime, indianTz));
                historyCells[6] = new DataCell(string.IsNullOrEmpty(update.PreviousCategoryName)
                    ? ""
                    : update.PreviousCategoryName);
                historyCells[7] = new DataCell(string.IsNullOrEmpty(update.CurrentCategoryName)
                    ? ""
                    : update.CurrentCategoryName);
                await spreadsheet.AddRowAsync(historyCells, cts.Token);
            }

            await spreadsheet.FinishAsync(cts.Token);
        }

        return new ExportTransactionsResponse
        {
            FileData = ByteString.CopyFrom(memoryStream.ToArray()),
            FileName = $"transactions_export_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx",
            ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
        };
    }
}

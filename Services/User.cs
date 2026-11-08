using Contracts.Wallet;
using Dapper;
using Grpc.Core;
using Npgsql;
using Wallet.Extensions;
using Wallet.Models;
using Contracts.Grpcx;

namespace Wallet.Services;

public sealed class User(ILogger<User> logger, NpgsqlDataSource dataSource, ServiceOptions options)
    : UserService.UserServiceBase
{
    public override async Task<GetUserSummaryResponse> GetUserSummary(SimpleRequest request, ServerCallContext context)
    {
        using var cts = context.CreateTimeout(options);
        await using var connection = await dataSource.OpenConnectionAsync(cts.Token);

        const string query = """
                             select allIncome, allExpense, monthIncome, monthExpense, weekIncome, weekExpense, startDate
                             from wallet.get_summary(@p_userId)
                             """;

        var result = await connection.QueryFirstOrDefaultAsync<UserSummary>(
            new CommandDefinition(query, new { p_userId = request.UserId }, cancellationToken: cts.Token));

        if (result is not null)
            return new GetUserSummaryResponse
            {
                AllIncome = result.AllIncome.ToGrpcDecimal(),
                AllExpense = result.AllExpense.ToGrpcDecimal(),
                MonthIncome = result.MonthIncome.ToGrpcDecimal(),
                MonthExpense = result.MonthExpense.ToGrpcDecimal(),
                WeekIncome = result.WeekIncome.ToGrpcDecimal(),
                WeekExpense = result.WeekExpense.ToGrpcDecimal(),
                StartDate = result.StartDate.ToGrpcTimestamp()
            };

        logger.LogInformation("User {UserId} has no wallet yet, initiating", request.UserId);
        var init = await connection.QuerySingleAsync<InitiateUserResult>(new CommandDefinition(
            "select v_startdate as StartDate, outcode from wallet.initiate_user(@p_userId)",
            new { p_userId = request.UserId }, cancellationToken: cts.Token));

        if (init.Outcode == 2)
            throw new RpcException(new Status(StatusCode.Internal, "Database error during wallet initiation"));

        return new GetUserSummaryResponse
        {
            AllIncome = 0m.ToGrpcDecimal(),
            AllExpense = 0m.ToGrpcDecimal(),
            MonthIncome = 0m.ToGrpcDecimal(),
            MonthExpense = 0m.ToGrpcDecimal(),
            WeekIncome = 0m.ToGrpcDecimal(),
            WeekExpense = 0m.ToGrpcDecimal(),
            StartDate = init.StartDate.ToGrpcTimestamp()
        };
    }
}

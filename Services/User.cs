using Grpc.Core;
using Npgsql;
using Dapper;
using Contracts.Wallet;
using Wallet.Models;
using Wallet.Extensions;
using Timeout = Wallet.Utils.Timeout;

namespace Wallet.Services;

public sealed class User(ILogger<User> logger, NpgsqlDataSource dataSource) : UserService.UserServiceBase
{
    public override async Task<GetUserSummaryResponse> GetUserSummary(SimpleRequest request, ServerCallContext context)
    {
        try
        {
            using var ctx = Timeout.WithDeadline(Timeout.Duration, context.CancellationToken);
            await using var connection = await dataSource.OpenConnectionAsync(ctx.Token);

            const string query = """

                                     select 
                                         allIncome,
                                         allExpense,
                                         monthIncome,
                                         monthExpense,
                                         weekIncome,
                                         weekExpense,
                                         startDate
                                     from wallet.user 
                                     where id = @p_userId
                                                              
                                 """;

            var result = await connection.QueryFirstOrDefaultAsync<UserSummary>(query, new { p_userId = request.UserId });

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

            logger.LogInformation("User {UserId} not found, initiating wallet", request.UserId);
            var initResult = await connection.QuerySingleAsync<InitiateUserResult>(
                "select v_startdate as StartDate, outcode from wallet.initiate_user(@p_userId)",
                new { p_userId = request.UserId });

            if (initResult.Outcode == 2)
            {
                throw new RpcException(new Status(StatusCode.Internal, "Database error during user initiation"));
            }

            return new GetUserSummaryResponse
            {
                AllIncome = 0m.ToGrpcDecimal(),
                AllExpense = 0m.ToGrpcDecimal(),
                MonthIncome = 0m.ToGrpcDecimal(),
                MonthExpense = 0m.ToGrpcDecimal(),
                WeekIncome = 0m.ToGrpcDecimal(),
                WeekExpense = 0m.ToGrpcDecimal(),
                StartDate = initResult.StartDate.ToGrpcTimestamp()
            };
        }
        catch (RpcException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error retrieving wallet summary for {UserId}", request.UserId);
            throw new RpcException(new Status(StatusCode.Internal,
                "An internal error occurred processing the wallet request"));
        }
    }
}


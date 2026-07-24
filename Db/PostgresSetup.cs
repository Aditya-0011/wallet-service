using Npgsql;

namespace Wallet.Db;

public static class PostgresSetup
{
    public static void AddPostgresDb(this IServiceCollection services)
    {
        var connectionUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
        
        if (string.IsNullOrWhiteSpace(connectionUrl))
        {
            throw new InvalidOperationException("DATABASE_URL environment variable is not set");
        }

        var connStringBuilder = new NpgsqlConnectionStringBuilder(connectionUrl)
        {
            MaxPoolSize = 3,
            MinPoolSize = 1,
            ConnectionLifetime = 1800,     
            ConnectionIdleLifetime = 300,  
            KeepAlive = 60                
        };

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connStringBuilder.ConnectionString);

        var dataSource = dataSourceBuilder.Build();
        services.AddSingleton(dataSource);
    }
}

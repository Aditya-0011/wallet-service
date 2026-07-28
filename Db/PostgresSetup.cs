using Npgsql;

namespace Wallet.Db;

public static class PostgresSetup
{
    public static void AddPostgresDb(this IServiceCollection services)
    {
        var connectionUrl = Environment.GetEnvironmentVariable("POSTGRES_URL");
        
        if (string.IsNullOrWhiteSpace(connectionUrl))
        {
            throw new InvalidOperationException("POSTGRES_URL environment variable is not set");
        }

        var finalConnectionString = connectionUrl;

        if (Uri.TryCreate(connectionUrl, UriKind.Absolute, out var uri) && 
            uri.Scheme is "postgres" or "postgresql")
        {
            var userInfo = uri.UserInfo.Split(':', 2);
            var builder = new NpgsqlConnectionStringBuilder
            {
                Host = uri.Host,
                Port = uri.IsDefaultPort ? 5432 : uri.Port,
                Database = uri.AbsolutePath.TrimStart('/'),
                Username = userInfo.Length > 0 ? Uri.UnescapeDataString(userInfo[0]) : string.Empty,
                Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty
            };
            
            if (uri.Query.Contains("sslmode=disable", StringComparison.OrdinalIgnoreCase))
                builder.SslMode = SslMode.Disable;
            else if (uri.Query.Contains("sslmode=require", StringComparison.OrdinalIgnoreCase))
                builder.SslMode = SslMode.Require;

            finalConnectionString = builder.ConnectionString;
        }

        var connStringBuilder = new NpgsqlConnectionStringBuilder(finalConnectionString)
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

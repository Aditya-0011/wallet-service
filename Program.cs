using Wallet.Services;
using Wallet.Db;
using Wallet.Middlewares;
using ProtoValidate;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();

builder.Services.AddPostgresDb();

builder.Services.AddSingleton<Validator>();

builder.Services.AddGrpc(options =>
{
    options.Interceptors.Add<LoggingInterceptor>();
    options.Interceptors.Add<ValidationInterceptor>();
});

var app = builder.Build();

app.Logger.LogInformation("Postgres initialized");

app.MapGrpcService<User>();
app.MapGrpcService<Category>();

app.Run();

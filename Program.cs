using ProtoValidate;
using Wallet.Services;
using Wallet.Db;
using Wallet.Middlewares;

var bindIp = Environment.GetEnvironmentVariable("INTERNAL_BIND_IP") ?? "0.0.0.0";
var port = Environment.GetEnvironmentVariable("PORT") ?? "7297";

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options =>
{
    options.SingleLine = true;
    options.TimestampFormat = "[HH:mm:ss] ";
    options.UseUtcTimestamp = true;
});

builder.Services.AddPostgresDb();

builder.Services.AddSingleton<Validator>();

builder.Services.AddGrpc(options =>
{
    options.Interceptors.Add<LoggingInterceptor>();
    options.Interceptors.Add<ValidationInterceptor>();
});

var app = builder.Build();

app.Logger.LogInformation("Postgres initialized");

app.Logger.LogInformation(
    "runtime config: ProcessorCount={ProcessorCount}, ServerGC={ServerGC}, TotalMemoryLimit_MiB={TotalMemoryLimit_MiB}",
    Environment.ProcessorCount,
    System.Runtime.GCSettings.IsServerGC,
    GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 1024 / 1024);

app.MapGrpcService<User>();
app.MapGrpcService<Finance>();

app.Run($"http://{bindIp}:{port}");

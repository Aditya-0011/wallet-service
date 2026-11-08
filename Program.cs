using Contracts.Grpcx;
using Wallet.Db;
using Wallet.Services;
using Wallet.Validation;

var builder = WebApplication.CreateBuilder(args);

builder.AddGrpcxServer(defaultPort: 7297);
builder.Services.AddPostgresDb();
builder.Services.AddSingleton<IRequestValidator, CategoryValidator>();

var app = builder.Build();

app.Logger.LogInformation(
    "runtime config: ProcessorCount={ProcessorCount}, ServerGC={ServerGC}, TotalMemoryLimit_MiB={TotalMemoryLimitMiB}",
    Environment.ProcessorCount,
    System.Runtime.GCSettings.IsServerGC,
    GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 1024 / 1024);

app.MapGrpcService<User>();
app.MapGrpcService<Finance>();
app.MapGrpcxHealth();

app.Run();

using CrypticPortfolioConfiguration.Database.Repos;
using CrypticPortfolioConfiguration.DI;
using CrypticPortfolioConfiguration.Services.Config;
using CrypticPortfolioConfiguration.Services.gRpc;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGrpc();

var cfg = new ConfigService();

// Add services to the container.
builder.Services.InjectConfiguration(cfg);
builder.Services.ConfigureMicroservices(cfg);
builder.Services.ConfigureRepositories();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.MapGet("/",
    () =>
        "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

app.MapGrpcService<PortfolioServiceImpl>();

app.Urls.Add("http://+:5000");
app.Urls.Add("https://+:5001");

app.Run();
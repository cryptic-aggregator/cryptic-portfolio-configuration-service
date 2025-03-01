using CrypticPortfolioConfiguration.Database.Repos;
using CrypticPortfolioConfiguration.DI;
using CrypticPortfolioConfiguration.Services.Config;

var cfg = new ConfigService();
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddGrpc();
builder.Services.InjectConfiguration(cfg);
builder.Services.ConfigureMicroservices(cfg);
builder.Services.ConfigureRepositories();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.MapGet("/",
    () =>
        "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

app.MapGrpcService<PortfolioRepo>();

app.Urls.Add("http://+:5000");
app.Urls.Add("https://+:5001");

app.Run();
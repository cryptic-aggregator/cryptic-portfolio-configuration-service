using Cryptic_Domain.Database.Config.Interfaces;
using Cryptic_Domain.Database.Interfaces;
using Cryptic_Domain.Services;
using CrypticPortfolioConfiguration.Interfaces.Config;
using CrypticPortfolioConfiguration.Services.Config;

namespace CrypticPortfolioConfiguration.DI;

public static class ConfigurationDIConfigure
{
    public static void InjectConfiguration(this IServiceCollection services, ConfigService config)
    {
        services.AddSingleton<IDatabaseConfiguration>(config);
        services.AddSingleton<IRabbitMQConfig>(config);
        services.AddSingleton<IDatabaseConnectionService, DatabaseConnectionService>();
    }
}
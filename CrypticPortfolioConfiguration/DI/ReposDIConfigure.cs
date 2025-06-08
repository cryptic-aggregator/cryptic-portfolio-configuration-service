using CrypticPortfolioConfiguration.Database.Repos;
using CrypticPortfolioConfiguration.Interfaces.Database;

namespace CrypticPortfolioConfiguration.DI;

public static class ReposDIConfigure
{
    public static void ConfigureRepositories(this IServiceCollection services)
    {
        services.AddScoped<PortfolioRepo>();
        services.AddScoped<WalletRepo>();

        services.AddScoped<IPortfolioRepo, PortfolioRepo>();
        services.AddScoped<IWalletRepo, WalletRepo>();
    }
}
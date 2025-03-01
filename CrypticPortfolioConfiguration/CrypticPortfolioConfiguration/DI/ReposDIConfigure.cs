using CrypticPortfolioConfiguration.Database.Repos;

namespace CrypticPortfolioConfiguration.DI;

public static class ReposDIConfigure
{
    public static void ConfigureRepositories(this IServiceCollection services)
    {
        services.AddScoped<PortfolioRepo>();
        services.AddScoped<WalletRepo>();
    }
}
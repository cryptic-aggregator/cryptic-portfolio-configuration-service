using CrypticPortfolioConfiguration.Database.Repos;
using CrypticPortfolioConfiguration.Database.Tables;

namespace CrypticPortfolioConfiguration.Interfaces.Database;

public interface IWalletRepo : IBaseDbRepo<WalletTable>
{
    Task<WalletTable> CreateAsync(WalletTable wallet);

    Task<List<WalletTable>> GetByPortfolioIdAsync(int portfolioId);

    Task<List<WalletTable>> GetVisibleByPortfolioIdAsync(int portfolioId);

    Task<bool> UpdateVisibilityAsync(int portfolioId, int walletId, int newVisibility);
}
using Cryptic_Domain.Database.Config.Interfaces;
using Cryptic_Domain.Database.Interfaces;
using Cryptic_Domain.Database.Repos.Base;
using CrypticPortfolioConfiguration.Database.Tables;
using Npgsql;

namespace CrypticPortfolioConfiguration.Database.Repos;

public class WalletRepo : BaseDbRepo<WalletTable>
{
    public WalletRepo(IDatabaseConnectionService connectionService, IDatabaseConfiguration configuration)
        : base(connectionService, configuration)
    {
    }
        
    public async Task<WalletTable> CreateAsync(WalletTable wallet)
    {
        var insertCommand = $"INSERT INTO {FullTablePath} (portfolio_id, wallet_address, created_at) VALUES (@PortfolioId, @WalletAddress, @CreatedAt) RETURNING id;";
        using (var cmd = new NpgsqlCommand(insertCommand, Connection))
        {
            cmd.Parameters.AddWithValue("@PortfolioId", wallet.PortfolioId);
            cmd.Parameters.AddWithValue("@WalletAddress", wallet.WalletAddress);
            cmd.Parameters.AddWithValue("@CreatedAt", wallet.CreatedAt);
            wallet.Id = (int)await cmd.ExecuteScalarAsync();
        }
        return await GetByIdAsync(wallet.Id);
    }
}
using Cryptic_Domain.Database.Config.Interfaces;
using Cryptic_Domain.Database.Interfaces;
using Cryptic_Domain.Database.Repos.Base;
using Cryptic_Domain.Helpers;
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
    
    public async Task<List<WalletTable>> GetByPortfolioIdAsync(int portfolioId)
    {
        var command = $"SELECT {string.Join(", ", Columns)} FROM {FullTablePath} WHERE portfolio_id = @PortfolioId";
        using (var cmd = new NpgsqlCommand(command, Connection))
        {
            cmd.Parameters.AddWithValue("PortfolioId", portfolioId);
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                var walletList = new List<WalletTable>();
                while (await reader.ReadAsync())
                {
                    walletList.Add(await reader.MapAsync<WalletTable>());
                }
                return walletList;
            }
        }
    }
}
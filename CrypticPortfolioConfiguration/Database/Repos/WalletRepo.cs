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
        var insertCommand = $@"
        INSERT INTO {FullTablePath} 
            (portfolio_id, wallet_address, created_at, visibility, connection_type) 
        VALUES 
            (@PortfolioId, @WalletAddress, @CreatedAt, @Visibility, @ConnectionType) 
        RETURNING id;
    ";

        using (var cmd = new NpgsqlCommand(insertCommand, Connection))
        {
            cmd.Parameters.AddWithValue("@PortfolioId", wallet.PortfolioId);
            cmd.Parameters.AddWithValue("@WalletAddress", wallet.WalletAddress);
            cmd.Parameters.AddWithValue("@CreatedAt", wallet.CreatedAt);
            cmd.Parameters.AddWithValue("@Visibility", (int)wallet.Visibility);
            cmd.Parameters.AddWithValue("@ConnectionType", (int)wallet.ConnectionType);

            wallet.Id = (int)await cmd.ExecuteScalarAsync();
        }
    
        return await GetByIdAsync(wallet.Id);
    }
    
    public async Task<List<WalletTable>> GetByPortfolioIdAsync(int portfolioId)
    {
        var command = $"SELECT {string.Join(", ", Columns)} FROM {FullTablePath} WHERE portfolio_id = @PortfolioId and visibility = 1";
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
    
    public async Task<bool> UpdateVisibilityAsync(int portfolioId, int walletId, int newVisibility)
    {
        var commandText = $@"
        UPDATE {FullTablePath} 
        SET visibility = @Visibility 
        WHERE portfolio_id = @PortfolioId AND id = @WalletId
    ";

        using (var cmd = new NpgsqlCommand(commandText, Connection))
        {
            cmd.Parameters.AddWithValue("@Visibility", (int)newVisibility);
            cmd.Parameters.AddWithValue("@PortfolioId", portfolioId);
            cmd.Parameters.AddWithValue("@WalletId", walletId);

            var rowsAffected = await cmd.ExecuteNonQueryAsync();
            return rowsAffected == 1;
        }
    }
}
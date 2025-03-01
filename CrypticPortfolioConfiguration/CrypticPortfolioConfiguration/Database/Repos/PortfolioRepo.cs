using Cryptic_Domain.Database.Config.Interfaces;
using Cryptic_Domain.Database.Interfaces;
using Cryptic_Domain.Database.Repos.Base;
using CrypticPortfolioConfiguration.Database.Tables;
using Npgsql;

namespace CrypticPortfolioConfiguration.Database.Repos;

public class PortfolioRepo : BaseDbRepo<PortfolioTable>
{
    public PortfolioRepo(IDatabaseConnectionService connectionService, IDatabaseConfiguration configuration)
        : base(connectionService, configuration)
    {
    }
        
    public async Task<PortfolioTable> CreateAsync(PortfolioTable portfolio)
    {
        var insertCommand = $"INSERT INTO {FullTablePath} (name, created_at) VALUES (@Name, @CreatedAt) RETURNING id;";
        using (var cmd = new NpgsqlCommand(insertCommand, Connection))
        {
            cmd.Parameters.AddWithValue("@Name", portfolio.Name);
            cmd.Parameters.AddWithValue("@CreatedAt", portfolio.CreatedAt);
            portfolio.Id = (int)await cmd.ExecuteScalarAsync();
        }
        return await GetByIdAsync(portfolio.Id);
    }
}
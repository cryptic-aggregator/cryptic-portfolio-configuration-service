using System.Linq.Expressions;
using Cryptic_Domain.Database.Config.Interfaces;
using Cryptic_Domain.Database.Interfaces;
using Cryptic_Domain.Database.Repos.Base;
using Cryptic_Domain.Helpers;
using CrypticPortfolioConfiguration.Database.Tables;
using CrypticPortfolioConfiguration.Interfaces.Database;
using Npgsql;

namespace CrypticPortfolioConfiguration.Database.Repos;

public class PortfolioRepo : BaseDbRepo<PortfolioTable>, IPortfolioRepo
{
    public PortfolioRepo(IDatabaseConnectionService connectionService, IDatabaseConfiguration configuration)
        : base(connectionService, configuration)
    {
    }
        
    public async Task<PortfolioTable> CreateAsync(PortfolioTable portfolio)
    {
        var insertCommand = $"INSERT INTO {FullTablePath} (name, owner_id, created_at) VALUES (@Name, @OwnerId, @CreatedAt) RETURNING id;";
        using (var cmd = new NpgsqlCommand(insertCommand, Connection))
        {
            cmd.Parameters.AddWithValue("@Name", portfolio.Name);
            cmd.Parameters.AddWithValue("@OwnerId", portfolio.OwnerId);
            cmd.Parameters.AddWithValue("@CreatedAt", portfolio.CreatedAt);
            portfolio.Id = (int)await cmd.ExecuteScalarAsync();
        }
        return await GetByIdAsync(portfolio.Id);
    }
    
    public async Task<List<PortfolioTable>> GetByOwnerIdAsync(int ownerId)
    {
        var command = $"SELECT {string.Join(", ", Columns)} FROM {FullTablePath} WHERE owner_id = @OwnerId";
        using var cmd = new NpgsqlCommand(command, Connection);
        cmd.Parameters.AddWithValue("OwnerId", ownerId);
            
        using var reader = await cmd.ExecuteReaderAsync();
        var portfolios = new List<PortfolioTable>();
        while (await reader.ReadAsync())
        {
            portfolios.Add(await reader.MapAsync<PortfolioTable>());
        }
        return portfolios;
    }
    
    public async Task<PortfolioTable?> GetByIdAndOwnerIdAsync(int id, int ownerId)
    {
        var command = $"SELECT {string.Join(", ", Columns)} FROM {FullTablePath} WHERE id = @Id AND owner_id = @OwnerId";
        using var cmd = new NpgsqlCommand(command, Connection);
        cmd.Parameters.AddWithValue("Id", id);
        cmd.Parameters.AddWithValue("OwnerId", ownerId);
            
        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return await reader.MapAsync<PortfolioTable>();
        }
        return default;
    }
    
    public async Task UpdateAsync(PortfolioTable portfolio, Expression<Func<PortfolioTable, object>> selector)
    {
        var propertyNamesToUpdate = GetPropertyNamesToUpdate(selector);
        var updateParameters = GetUpdateParameters(portfolio, propertyNamesToUpdate);

        var updateQuery = $"UPDATE {FullTablePath} SET {updateParameters.SetterString} WHERE id = @Id AND owner_id = @OwnerId";

        using var cmd = new NpgsqlCommand(updateQuery, Connection);
        cmd.Parameters.AddWithValue("Id", portfolio.Id);
        cmd.Parameters.AddWithValue("OwnerId", portfolio.OwnerId);
        cmd.Parameters.AddRange(updateParameters.Parameters.ToArray());

        await cmd.ExecuteNonQueryAsync();
    }
}
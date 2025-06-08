using System.Linq.Expressions;
using CrypticPortfolioConfiguration.Database.Tables;

namespace CrypticPortfolioConfiguration.Interfaces.Database;

public interface IPortfolioRepo : IBaseDbRepo<PortfolioTable>
{
    Task<PortfolioTable> CreateAsync(PortfolioTable portfolio);
    Task<PortfolioTable> GetByIdAndOwnerIdAsync(int id, int ownerId);
    Task<List<PortfolioTable>> GetByOwnerIdAsync(int ownerId);
    Task UpdateAsync(PortfolioTable portfolio, Expression<Func<PortfolioTable, object>> selector);
}
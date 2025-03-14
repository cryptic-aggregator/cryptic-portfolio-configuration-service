using Cryptic_Domain.Database.Attributes;
using Cryptic_Domain.Interfaces.Database;
using NpgsqlTypes;

namespace CrypticPortfolioConfiguration.Database.Tables;

[Table("wallet")]
public class WalletTable : IDatabaseTable
{
    [Column("id", NpgsqlDbType.Integer)]
    public int Id { get; set; }
        
    [Column("portfolio_id", NpgsqlDbType.Integer)]
    public int PortfolioId { get; set; }
        
    [Column("wallet_address", NpgsqlDbType.Varchar)]
    public string WalletAddress { get; set; }
    
    [Column("visibility", NpgsqlDbType.Varchar)]
    public int Visibility { get; set; }
    
    [Column("connection_type", NpgsqlDbType.Varchar)]
    public int ConnectionType { get; set; }
    
    [Column("created_at", NpgsqlDbType.Bigint)]
    public long CreatedAt { get; set; }
}
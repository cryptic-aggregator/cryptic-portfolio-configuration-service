using Cryptic_Domain.Database.Attributes;
using Cryptic_Domain.Interfaces.Database;
using NpgsqlTypes;

namespace CrypticPortfolioConfiguration.Database.Tables;

[Table("portfolio")]
public class PortfolioTable : IDatabaseTable
{
    [Column("id", NpgsqlDbType.Integer)]
    public int Id { get; set; }
        
    [Column("name", NpgsqlDbType.Varchar)]
    public string Name { get; set; }
    
    [Column("created_at", NpgsqlDbType.Bigint)]
    public long CreatedAt { get; set; }
}
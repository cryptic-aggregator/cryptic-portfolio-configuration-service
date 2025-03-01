using Cryptic.BlockchainInteraction.Rpc;

namespace CrypticPortfolioConfiguration.Interfaces.Config;

public interface IMicroservicesConfig
{
    public string BlockchainInteractionConnString { get; }
}
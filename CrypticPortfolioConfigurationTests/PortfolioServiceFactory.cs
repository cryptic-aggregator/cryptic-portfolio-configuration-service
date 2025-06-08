using Cryptic.BlockchainInteraction.Rpc;
using Cryptic.PortfolioAnalytic.Rpc;
using CrypticPortfolioConfiguration.Database.Repos;
using CrypticPortfolioConfiguration.Interfaces.Database;
using CrypticPortfolioConfiguration.Services.gRpc;
using MassTransit;
using Microsoft.Extensions.Logging;
using Moq;

namespace CrypticTests;

public class PortfolioServiceFactory
{
    public Mock<IPortfolioRepo> MockPortfolioRepo { get; }
    public Mock<IWalletRepo> MockWalletRepo { get; }
    public Mock<WalletService.WalletServiceClient> MockWalletServiceClient { get; }
    public Mock<PortfolioAnalyticService.PortfolioAnalyticServiceClient> MockPortfolioAnalyticServiceClient { get; }
    public Mock<IPublishEndpoint> MockPublishEndpoint { get; }
    public Mock<ILogger<PortfolioServiceImpl>> MockLogger { get; }
    public Mock<AnalyticTransactionService.AnalyticTransactionServiceClient> MockTransactionServiceClient { get; }

    public PortfolioServiceFactory()
    {
        MockPortfolioRepo = new Mock<IPortfolioRepo>();
        MockWalletRepo = new Mock<IWalletRepo>();
        MockWalletServiceClient = new Mock<WalletService.WalletServiceClient>();
        MockPortfolioAnalyticServiceClient = new Mock<PortfolioAnalyticService.PortfolioAnalyticServiceClient>();
        MockPublishEndpoint = new Mock<IPublishEndpoint>();
        MockLogger = new Mock<ILogger<PortfolioServiceImpl>>();
        MockTransactionServiceClient = new Mock<AnalyticTransactionService.AnalyticTransactionServiceClient>();
    }

    public PortfolioServiceImpl Create(
        Action<Mock<IPortfolioRepo>> setupPortfolioRepo = null,
        Action<Mock<IWalletRepo>> setupWalletRepo = null,
        Action<Mock<WalletService.WalletServiceClient>> setupWalletServiceClient = null,
        Action<Mock<PortfolioAnalyticService.PortfolioAnalyticServiceClient>> setupPortfolioAnalyticServiceClient =
            null,
        Action<Mock<IPublishEndpoint>> setupPublishEndpoint = null,
        Action<Mock<ILogger<PortfolioServiceImpl>>> setupLogger = null,
        Action<Mock<AnalyticTransactionService.AnalyticTransactionServiceClient>> setupTransactionServiceClient = null
    )
    {
        setupPortfolioRepo?.Invoke(MockPortfolioRepo);
        setupWalletRepo?.Invoke(MockWalletRepo);
        setupWalletServiceClient?.Invoke(MockWalletServiceClient);
        setupPortfolioAnalyticServiceClient?.Invoke(MockPortfolioAnalyticServiceClient);
        setupPublishEndpoint?.Invoke(MockPublishEndpoint);
        setupLogger?.Invoke(MockLogger);
        setupTransactionServiceClient?.Invoke(MockTransactionServiceClient);

        return new PortfolioServiceImpl(
            MockPortfolioRepo.Object,
            MockWalletRepo.Object,
            MockWalletServiceClient.Object,
            MockPortfolioAnalyticServiceClient.Object,
            MockPublishEndpoint.Object,
            MockLogger.Object,
            MockTransactionServiceClient.Object
        );
    }
}
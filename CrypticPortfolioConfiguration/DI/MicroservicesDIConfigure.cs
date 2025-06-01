using Cryptic.BlockchainInteraction.Rpc;
using Cryptic.PortfolioAnalytic.Rpc;
using CrypticPortfolioConfiguration.Interfaces.Config;

namespace CrypticPortfolioConfiguration.DI;

public static class MicroservicesDIConfigure
{
    public static void ConfigureMicroservices(this IServiceCollection services, IMicroservicesConfig cfg)
    {
        var customHandler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (httpRequestMessage, cert, cetChain, policyErrors) => true
        };
        
        services.AddGrpcClient<WalletService.WalletServiceClient>(opt =>
        {
            opt.Address = new Uri(cfg.BlockchainInteractionConnString);
        }).ConfigurePrimaryHttpMessageHandler(() => customHandler);
        
        services.AddGrpcClient<PortfolioAnalyticService.PortfolioAnalyticServiceClient>(opt =>
        {
            opt.Address = new Uri(cfg.AnalyticConnString);
        }).ConfigurePrimaryHttpMessageHandler(() => customHandler);
        
        services.AddGrpcClient<AnalyticTransactionService.AnalyticTransactionServiceClient>(opt =>
        {
            opt.Address = new Uri(cfg.AnalyticConnString);
        }).ConfigurePrimaryHttpMessageHandler(() => customHandler);
    }
}
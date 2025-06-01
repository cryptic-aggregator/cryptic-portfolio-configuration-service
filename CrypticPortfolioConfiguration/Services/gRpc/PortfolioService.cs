using Cryptic_Domain.Enums.Portfolio;
using Cryptic_Domain.Models.MassTransit;
using Cryptic.Base.V1.Models.Responses;
using Cryptic.BlockchainInteraction.Models.Requests;
using Cryptic.BlockchainInteraction.Models.Responses;
using Cryptic.BlockchainInteraction.Rpc;
using Cryptic.PortfolioAnalytic.Models.Requests;
using Cryptic.PortfolioAnalytic.Rpc;
using Cryptic.PortfolioConfiguration.Models.Requests;
using Cryptic.PortfolioConfiguration.Models.Responses;
using Cryptic.PortfolioConfiguration.Rpc;
using CrypticPortfolioConfiguration.Database.Repos;
using CrypticPortfolioConfiguration.Database.Tables;
using Grpc.Core;
using MassTransit;
using GetWalletTransactionsRequest = Cryptic.PortfolioAnalytic.Models.Requests.GetWalletTransactionsRequest;
using GetWalletTransactionsResponse = Cryptic.PortfolioAnalytic.Models.Responses.GetWalletTransactionsResponse;
using WalletWithCoins = Cryptic.PortfolioConfiguration.Models.Responses.WalletWithCoins;

namespace CrypticPortfolioConfiguration.Services.gRpc;

public class PortfolioServiceImpl : PortfolioService.PortfolioServiceBase
{
    private readonly WalletRepo _walletRepo;
    private readonly PortfolioRepo _portfolioRepo;
    private readonly WalletService.WalletServiceClient _walletService;
    private readonly PortfolioAnalyticService.PortfolioAnalyticServiceClient _portfolioAnalyticService;
    private readonly AnalyticTransactionService.AnalyticTransactionServiceClient _transactionService;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<PortfolioServiceImpl> _logger;

    public PortfolioServiceImpl(PortfolioRepo portfolioRepo, WalletRepo walletRepo,
        WalletService.WalletServiceClient walletService,
        PortfolioAnalyticService.PortfolioAnalyticServiceClient portfolioAnalyticService,
        IPublishEndpoint publishEndpoint, ILogger<PortfolioServiceImpl> logger,
        AnalyticTransactionService.AnalyticTransactionServiceClient transactionService)
    {
        _portfolioRepo = portfolioRepo;
        _walletRepo = walletRepo;
        _walletService = walletService;
        _portfolioAnalyticService = portfolioAnalyticService;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
        _transactionService = transactionService;
    }

    private Portfolio ToGrpcPortfolio(PortfolioTable table)
    {
        return new Portfolio
        {
            Id = table.Id,
            Name = table.Name,
            OwnerId = table.OwnerId,
            CreatedAt = table.CreatedAt
        };
    }

    public override async Task<GetPortfolioResponse> CreatePortfolio(CreatePortfolioRequest request,
        ServerCallContext context)
    {
        var portfolio = new PortfolioTable
        {
            Name = request.Name,
            OwnerId = request.OwnerId,
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        var createdPortfolio = await _portfolioRepo.CreateAsync(portfolio);
        return new GetPortfolioResponse { Portfolio = ToGrpcPortfolio(createdPortfolio) };
    }

    public override async Task<GetPortfolioResponse> GetPortfolio(GetPortfolioRequest request,
        ServerCallContext context)
    {
        var portfolio = await _portfolioRepo.GetByIdAndOwnerIdAsync(request.Id, request.OwnerId);
        if (portfolio == null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, "Portfolio not found"));
        }

        return new GetPortfolioResponse { Portfolio = ToGrpcPortfolio(portfolio) };
    }

    public override async Task<GetPortfoliosByOwnerResponse> GetPortfoliosByOwner(GetPortfoliosByOwnerRequest request,
        ServerCallContext context)
    {
        var portfolios = await _portfolioRepo.GetByOwnerIdAsync(request.OwnerId);
        var response = new GetPortfoliosByOwnerResponse();
        response.Portfolios.AddRange(portfolios.Select(p => ToGrpcPortfolio(p)));
        return response;
    }

    public override async Task<GetPortfolioResponse> UpdatePortfolio(UpdatePortfolioRequest request,
        ServerCallContext context)
    {
        var portfolioData = request.Portfolio;
        var existingPortfolio = await _portfolioRepo.GetByIdAndOwnerIdAsync(portfolioData.Id, portfolioData.OwnerId);
        if (existingPortfolio == null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, "Portfolio not found"));
        }

        existingPortfolio.Name = portfolioData.Name;
        await _portfolioRepo.UpdateAsync(existingPortfolio, x => new { x.Name });

        var updatedPortfolio =
            await _portfolioRepo.GetByIdAndOwnerIdAsync(existingPortfolio.Id, existingPortfolio.OwnerId);
        return new GetPortfolioResponse { Portfolio = ToGrpcPortfolio(updatedPortfolio) };
    }

    public override async Task<DeletePortfolioResponse> DeletePortfolio(DeletePortfolioRequest request,
        ServerCallContext context)
    {
        var existingPortfolio = await _portfolioRepo.GetByIdAndOwnerIdAsync(request.Id, request.OwnerId);
        if (existingPortfolio == null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, "Portfolio not found"));
        }

        await _portfolioRepo.DeleteAsync(existingPortfolio.Id);
        return new DeletePortfolioResponse { Result = new TaskResponse() { Success = true } };
    }

    public override async Task<ConnectWalletsResponse> ConnectWallets(ConnectWalletsRequest request,
        ServerCallContext context)
    {
        var createdAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var resp = new ConnectWalletsResponse();

        foreach (var w in request.Wallets)
        {
            var portfolio = await _portfolioRepo.GetByIdAndOwnerIdAsync(request.PortfolioId, request.OwnerId);
            if (portfolio == null)
                throw new RpcException(new Status(StatusCode.NotFound, "Portfolio not found or access denied"));

            var entity = new WalletTable
            {
                PortfolioId = request.PortfolioId,
                WalletAddress = w.WalletAddress,
                Name = w.Name,
                Connector = w.Connector,
                CaipAddress = w.CaipAddress,
                CreatedAt = createdAt,
                Visibility = (int)WalletVisibility.Public,
                ConnectionType = w.ConnectionType
            };

            var created = await _walletRepo.CreateAsync(entity);
            await _publishEndpoint.Publish(new NewWalletConnectedMessage(created.Id, created.WalletAddress));

            resp.Wallets.Add(new Wallet
            {
                Id = created.Id,
                PortfolioId = created.PortfolioId,
                Name = created.Name,
                Connector = created.Connector,
                CaipAddress = created.CaipAddress,
                ConnectionType = created.ConnectionType,
                Visibility = created.Visibility,
                WalletAddress = created.WalletAddress,
                CreatedAt = created.CreatedAt
            });
        }

        return resp;
    }

    public override async Task<GetPortfolioInfoResponse> GetPortfolioInfo(GetPortfolioInfoRequest request,
        ServerCallContext context)
    {
        var portfolioEntity = await _portfolioRepo.GetByIdAndOwnerIdAsync(request.PortfolioId, request.OwnerId);
        if (portfolioEntity == null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, "Portfolio not found"));
        }

        var walletEntities = await _walletRepo.GetVisibleByPortfolioIdAsync(request.PortfolioId);
        var response = new GetPortfolioInfoResponse();

        if (walletEntities != null)
        {
            var walletAddresses = walletEntities.Select(w => w.WalletAddress).ToList();

            Cryptic.BlockchainInteraction.Models.Requests.GetWalletCoinsRequest walletCoinsRequest =
                new Cryptic.BlockchainInteraction.Models.Requests.GetWalletCoinsRequest();
            walletCoinsRequest.Address.AddRange(walletAddresses);
            walletCoinsRequest.PortfolioId = request.PortfolioId;

            var walletCoinsResponse = await _walletService.GetWalletCoinsAsync(walletCoinsRequest);

            response = new GetPortfolioInfoResponse
            {
                Portfolio = ToGrpcPortfolio(portfolioEntity),
                WalletInfo = walletCoinsResponse,
                Result = new TaskResponse { Success = true }
            };

            return response;
        }


        response = new GetPortfolioInfoResponse
        {
            Portfolio = ToGrpcPortfolio(portfolioEntity),
            WalletInfo = null,
            Result = new TaskResponse { Success = true }
        };

        return response;
    }

    public override async Task<PatchWalletVisibilityResponse> PatchWalletVisibility(
        PatchWalletVisibilityRequest request,
        ServerCallContext context)
    {
        if (request.PortfolioId <= 0 || request.WalletId <= 0)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid portfolio or wallet ID"));
        }

        var success = await _walletRepo.UpdateVisibilityAsync(
            request.PortfolioId,
            request.WalletId,
            request.Visibility
        );

        return new PatchWalletVisibilityResponse
        {
            Result = new TaskResponse { Success = success }
        };
    }

    public override async Task<GetWalletsByPortfolioIdResponse> GetWalletsByPortfolioId(
        GetWalletsByPortfolioIdRequest request,
        ServerCallContext context)
    {
        if (request.PortfolioId <= 0)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid portfolio ID"));
        }

        var walletTables = await _walletRepo.GetByPortfolioIdAsync(request.PortfolioId);

        var response = new GetWalletsByPortfolioIdResponse();
        foreach (var w in walletTables)
        {
            var wallet = new Wallet
            {
                Id = w.Id,
                PortfolioId = w.PortfolioId,
                WalletAddress = w.WalletAddress,
                Visibility = w.Visibility,
                ConnectionType = w.ConnectionType,
                CreatedAt = w.CreatedAt,
                Name = w.Name,
                Connector = w.Connector,
                CaipAddress = w.CaipAddress
            };
            response.Wallets.Add(wallet);
        }

        return response;
    }

    public override async Task<GetPortfolioCalculationResponse> GetPortfolioCalculation(
        GetPortfolioCalculationRequest request, ServerCallContext context)
    {
        var portfolioEntity = await _portfolioRepo.GetByIdAsync(request.PortfolioId);
        if (portfolioEntity == null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, "Portfolio not found"));
        }

        var walletEntities = await _walletRepo.GetVisibleByPortfolioIdAsync(request.PortfolioId);

        if (walletEntities == null || !walletEntities.Any())
        {
            return new GetPortfolioCalculationResponse
            {
                Portfolio = ToGrpcPortfolio(portfolioEntity),
                Result = new TaskResponse { Success = true }
            };
        }

        var walletAddresses = walletEntities.Select(w => w.WalletAddress).ToList();
        var walletCoinsRequest = new Cryptic.BlockchainInteraction.Models.Requests.GetWalletCoinsRequest
        {
            PortfolioId = request.PortfolioId
        };

        walletCoinsRequest.Address.AddRange(walletAddresses);

        var walletCoinsResponse = await _walletService.GetWalletCoinsAsync(walletCoinsRequest);

        var calcRequest = new CalculateWalletRequest { WalletResponse = walletCoinsResponse };
        var calcResponse = await _portfolioAnalyticService.GetAssetAllocationsAsync(calcRequest);

        return new GetPortfolioCalculationResponse
        {
            Portfolio = ToGrpcPortfolio(portfolioEntity),
            CalculatedCoins = { calcResponse.WalletCoins },
            Result = new TaskResponse { Success = true }
        };
    }

    public override async Task<GetPortfolioTransactionsResponse>
        GetPortfolioTransactions(
            GetPortfolioTransactionsRequest request,
            ServerCallContext context)
    {
        if (request.PortfolioId <= 0)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid portfolioId"));
        }

        var portfolioEntity = await _portfolioRepo.GetByIdAsync(request.PortfolioId);
        if (portfolioEntity == null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, "Portfolio not found"));
        }

        var walletEntities = await _walletRepo.GetVisibleByPortfolioIdAsync(request.PortfolioId);

        if (walletEntities == null || !walletEntities.Any())
        {
            return new GetPortfolioTransactionsResponse
            {
                Portfolio = ToGrpcPortfolio(portfolioEntity),
                Total = 0,
                Page = request.Page <= 0 ? 1 : request.Page,
                PerPage = request.PerPage <= 0 ? 10 : request.PerPage
            };
        }

        var analyticRequest = new GetWalletTransactionsRequest
        {
            Page = request.Page <= 0 ? 1 : request.Page,
            PerPage = request.PerPage <= 0 ? 10 : request.PerPage,
            TransactionType = request.TransactionType
        };

        if (request.DateRange != null)
        {
            analyticRequest.DateRange = new DateRange
            {
                From = request.DateRange.From,
                To = request.DateRange.To
            };
        }

        foreach (var w in walletEntities)
        {
            analyticRequest.Wallets.Add(new WalletQuery
            {
                WalletId = w.Id,
                WalletAddress = w.WalletAddress
            });
        }

        GetWalletTransactionsResponse analyticResponse;
        try
        {
            analyticResponse = await _transactionService.GetWalletTransactionsAsync(analyticRequest);
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "Error calling PortfolioAnalyticService.GetWalletTransactionsAsync");
            throw;
        }

        var response = new GetPortfolioTransactionsResponse
        {
            Portfolio = ToGrpcPortfolio(portfolioEntity),
            Total = analyticResponse.Total,
            Page = analyticResponse.Page,
            PerPage = analyticResponse.PerPage
        };

        response.Transactions.Add(analyticResponse);

        return response;
    }

    public override async Task<GetPortfolioWalletsInfoResponse> GetPortfolioWalletsInfo(
        GetPortfolioInfoRequest request,
        ServerCallContext context)
    {
        var portfolioEntity = await _portfolioRepo.GetByIdAndOwnerIdAsync(request.PortfolioId, request.OwnerId);
        if (portfolioEntity == null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, "Portfolio not found"));
        }

        var walletEntities = await _walletRepo.GetVisibleByPortfolioIdAsync(request.PortfolioId);

        var response = new GetPortfolioWalletsInfoResponse
        {
            Portfolio = ToGrpcPortfolio(portfolioEntity),
            Result = new TaskResponse { Success = true }
        };

        foreach (var w in walletEntities)
        {

            var grpcReq = new GetWalletCoinsByAddressRequest
            {
                Address = w.WalletAddress,
                PortfolioId = request.PortfolioId
            };

            GetWalletCoinsResponse coinsResponse;
            try
            {
                coinsResponse = await _walletService.GetWalletCoinsByAddressAsync(grpcReq);
            }
            catch (RpcException ex)
            {
                continue;
            }

            var walletWithCoins = new WalletWithCoins()
            {
                WalletId = w.Id,
                WalletAddress = w.WalletAddress
            };
            
            foreach (var coin in coinsResponse.Coins)
            {
                walletWithCoins.Coins.Add(new Coin
                {
                    Symbol = coin.Symbol,
                    Balance = coin.Balance,
                    Image = coin.Image,
                    Name = coin.Name
                });
            }

            response.WalletInfo.Add(walletWithCoins);
        }

        return response;
    }
}
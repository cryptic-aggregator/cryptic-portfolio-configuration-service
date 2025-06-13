using Cryptic_Domain.Enums.Portfolio;
using Cryptic_Domain.Models.MassTransit;
using Cryptic.Base.V1.Models.Responses;
using Cryptic.BlockchainInteraction.Models.Requests;
using Cryptic.BlockchainInteraction.Models.Responses;
using Cryptic.BlockchainInteraction.Rpc;
using Cryptic.PortfolioAnalytic.Models.Requests;
using Cryptic.PortfolioAnalytic.Models.Responses;
using Cryptic.PortfolioAnalytic.Rpc;
using Cryptic.PortfolioConfiguration.Models.Requests;
using Cryptic.PortfolioConfiguration.Models.Responses;
using Cryptic.PortfolioConfiguration.Rpc;
using CrypticPortfolioConfiguration.Database.Repos;
using CrypticPortfolioConfiguration.Database.Tables;
using CrypticPortfolioConfiguration.Interfaces.Database;
using Grpc.Core;
using MassTransit;
using GetWalletTransactionsRequest = Cryptic.PortfolioAnalytic.Models.Requests.GetWalletTransactionsRequest;
using GetWalletTransactionsResponse = Cryptic.PortfolioAnalytic.Models.Responses.GetWalletTransactionsResponse;
using WalletWithCoins = Cryptic.PortfolioConfiguration.Models.Responses.WalletWithCoins;

namespace CrypticPortfolioConfiguration.Services.gRpc;

public class PortfolioServiceImpl : PortfolioService.PortfolioServiceBase
{
    private readonly IWalletRepo _walletRepo;
    private readonly IPortfolioRepo _portfolioRepo;
    private readonly WalletService.WalletServiceClient _walletService;
    private readonly PortfolioAnalyticService.PortfolioAnalyticServiceClient _portfolioAnalyticService;
    private readonly AnalyticTransactionService.AnalyticTransactionServiceClient _transactionService;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<PortfolioServiceImpl> _logger;

    public PortfolioServiceImpl(IPortfolioRepo portfolioRepo, IWalletRepo walletRepo,
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
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid portfolio ID"));

        var walletTables = await _walletRepo.GetByPortfolioIdAsync(request.PortfolioId);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            walletTables = walletTables
                .Where(w => w.WalletAddress.Contains(request.Search, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (request.Networks.Count > 0)
        {
            walletTables = walletTables
                .Where(w => request.Networks.Contains(DetectNetwork(w.WalletAddress), StringComparer.OrdinalIgnoreCase))
                .ToList();
        }

        var response = new GetWalletsByPortfolioIdResponse();
        foreach (var w in walletTables)
        {
            response.Wallets.Add(new Wallet
            {
                Id = w.Id,
                PortfolioId = w.PortfolioId,
                WalletAddress = w.WalletAddress,
                CreatedAt = w.CreatedAt,
                ConnectionType = w.ConnectionType,
                Visibility = w.Visibility,
                Name = w.Name ?? "",
                CaipAddress = w.CaipAddress ?? "",
                Connector = w.Connector ?? "",
                Network = DetectNetwork(w.WalletAddress)
            });
        }

        return response;
    }
    
    public override async Task<DeleteWalletResponse> DeleteWallet(DeleteWalletRequest request, ServerCallContext context)
    {
        if (request.PortfolioId <= 0 || request.WalletId <= 0)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid portfolio or wallet ID"));
        
        var portfolio = await _portfolioRepo.GetByIdAndOwnerIdAsync(request.PortfolioId, request.OwnerId);
        if (portfolio == null)
            throw new RpcException(new Status(StatusCode.NotFound, "Portfolio not found or access denied"));

        var wallet = await _walletRepo.GetByIdAsync(request.WalletId);
        if (wallet == null || wallet.PortfolioId != request.PortfolioId)
            throw new RpcException(new Status(StatusCode.NotFound, "Wallet not found in this portfolio"));
        
        await _walletRepo.DeleteAsync(request.WalletId);

        return new DeleteWalletResponse
        {
            Result = new TaskResponse { Success = true }
        };
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
            TransactionType = request.TransactionType, Search = request.Search
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
            throw new RpcException(new Status(StatusCode.NotFound, "Portfolio not found"));
        var walletEntities = await _walletRepo.GetVisibleByPortfolioIdAsync(request.PortfolioId);

        var response = new GetPortfolioWalletsInfoResponse
        {
            Portfolio = ToGrpcPortfolio(portfolioEntity),
            Result = new TaskResponse { Success = true }
        };

        if (walletEntities == null || walletEntities.Count == 0)
            return response;

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
            catch (RpcException)
            {
                continue;
            }

            var walletDto = new Wallet
            {
                Id = w.Id,
                PortfolioId = w.PortfolioId,
                WalletAddress = w.WalletAddress,
                CreatedAt = w.CreatedAt,
                ConnectionType = w.ConnectionType,
                Visibility = w.Visibility,
                Name = w.Name ?? string.Empty,
                CaipAddress = w.CaipAddress ?? string.Empty,
                Connector = w.Connector ?? string.Empty,
                Network = DetectNetwork(w.WalletAddress)
            };

            var walletWithCoins = new WalletWithCoins
            {
                Wallet = walletDto
            };

            foreach (var c in coinsResponse.Coins)
            {
                walletWithCoins.Coins.Add(new Cryptic.BlockchainInteraction.Models.Responses.Coin
                {
                    Symbol = c.Symbol,
                    Balance = c.Balance,
                    Image = c.Image,
                    Name = c.Name
                });
            }

            response.WalletInfo.Add(walletWithCoins);
        }

        return response;
    }

    public override async Task<GetPortfolioCorrelationResponse>
        GetPortfolioCorrelation(GetPortfolioCorrelationRequest request,
            ServerCallContext context)
    {
        if (request.PortfolioId <= 0)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid portfolio_id"));
        if (string.IsNullOrWhiteSpace(request.TokenSymbol))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "token_symbol is required"));
        if (request.FromTs == null || request.FromTs >= request.ToTs)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid date_range"));

        var wallets = await _walletRepo.GetVisibleByPortfolioIdAsync(request.PortfolioId);
        var walletIds = wallets.Select(w => w.Id).ToList();
        if (!walletIds.Any())
        {
            return new GetPortfolioCorrelationResponse
            {
                Result = new TaskResponse { Success = true }
            };
        }

        var analyticReq = new Cryptic.PortfolioAnalytic.Models.Requests.GetPortfolioVsTokenPointsRequest
        {
            TokenSymbol = request.TokenSymbol,
            FromTs = request.FromTs,
            ToTs = request.ToTs,
            PointsCount = 12
        };
        analyticReq.WalletIds.AddRange(walletIds);

        GetPortfolioCorrelationResponse analyticResp;
        try
        {
            analyticResp = await _portfolioAnalyticService
                .GetPortfolioVsTokenPointsAsync(analyticReq);
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "Error calling analytic service");
            throw;
        }

        var response = new GetPortfolioCorrelationResponse
        {
            Result = new TaskResponse { Success = true }
        };

        foreach (var p in analyticResp.Points)
        {
            response.Points.Add(new PortfolioCorrelationPoint
            {
                Ts = p.Ts,
                PortfolioValue = p.PortfolioValue,
                TokenPrice = p.TokenPrice,
                PortfolioChangePct = p.PortfolioChangePct,
                TokenChangePct = p.TokenChangePct
            });
        }

        return response;
    }

    private string DetectNetwork(string address)
    {
        if (address.StartsWith("0x") && address.Length == 42)
            return "eth";
        if (System.Text.RegularExpressions.Regex.IsMatch(address, "^(1|3|bc1)"))
            return "btc";
        return "sol";
    }
}
using Cryptic.Base.V1.Models.Responses;
using Cryptic.PortfolioConfiguration.Models.Requests;
using Cryptic.PortfolioConfiguration.Models.Responses;
using Cryptic.PortfolioConfiguration.Rpc;
using CrypticPortfolioConfiguration.Database.Repos;
using CrypticPortfolioConfiguration.Database.Tables;
using Grpc.Core;

namespace CrypticPortfolioConfiguration.Services.gRpc;

public class PortfolioServiceImpl : PortfolioService.PortfolioServiceBase
{
    private readonly PortfolioRepo _portfolioRepo;

    public PortfolioServiceImpl(PortfolioRepo portfolioRepo)
    {
        _portfolioRepo = portfolioRepo;
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
        return new DeletePortfolioResponse { Result = new TaskResponse(){Success = true} };
    }
}
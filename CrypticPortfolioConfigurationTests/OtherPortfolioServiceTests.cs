using System.Linq.Expressions;
using Cryptic.BlockchainInteraction.Models.Responses;
using Cryptic.PortfolioAnalytic.Models.Requests;
using Cryptic.PortfolioAnalytic.Models.Responses;
using Cryptic.PortfolioConfiguration.Models.Requests;
using Cryptic.PortfolioConfiguration.Models.Responses;
using CrypticPortfolioConfiguration.Database.Tables;
using CrypticPortfolioConfiguration.Services.gRpc;
using Grpc.Core;
using Moq;

namespace CrypticTests;

[TestFixture]
public class OtherPortfolioServiceTests
{
    private PortfolioServiceFactory _factory;
    private PortfolioServiceImpl _service;
    private ServerCallContext _serverCallContext;

    [SetUp]
    public void Setup()
    {
        _factory = new PortfolioServiceFactory();
        _service = _factory.Create();
    }
    
    [Test]
    public async Task GetPortfolio_Success()
    {
        // Arrange
        var request = new GetPortfolioRequest { Id = 200, OwnerId = 2 };
        var portfolioTable = new PortfolioTable
        {
            Id = 200,
            Name = "Existing Portfolio",
            OwnerId = 2,
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        _factory.MockPortfolioRepo
            .Setup(repo => repo.GetByIdAndOwnerIdAsync(request.Id, request.OwnerId))
            .ReturnsAsync(portfolioTable);

        // Act
        var response = await _service.GetPortfolio(request, _serverCallContext);

        // Assert
        Assert.IsNotNull(response);
        Assert.IsNotNull(response.Portfolio);
        Assert.AreEqual(200, response.Portfolio.Id);
        Assert.AreEqual("Existing Portfolio", response.Portfolio.Name);
    }

    [Test]
    public void GetPortfolio_NotFound_ThrowsRpcException()
    {
        // Arrange
        var request = new GetPortfolioRequest { Id = 300, OwnerId = 3 };
        _factory.MockPortfolioRepo
            .Setup(repo => repo.GetByIdAndOwnerIdAsync(request.Id, request.OwnerId))
            .ReturnsAsync((PortfolioTable)null);

        // Act & Assert
        var ex = Assert.ThrowsAsync<RpcException>(async () => await _service.GetPortfolio(request, _serverCallContext));
        Assert.AreEqual(StatusCode.NotFound, ex.Status.StatusCode);
    }

    [Test]
    public async Task UpdatePortfolio_Success()
    {
        var originalPortfolio = new PortfolioTable
        {
            Id = 400,
            Name = "Old Portfolio",
            OwnerId = 4,
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };
        var updatedPortfolioData = new Portfolio
        {
            Id = 400,
            Name = "Updated Portfolio",
            OwnerId = 4,
            CreatedAt = originalPortfolio.CreatedAt
        };
        var request = new UpdatePortfolioRequest { Portfolio = updatedPortfolioData };
        
        _factory.MockPortfolioRepo
            .Setup(repo => repo.GetByIdAndOwnerIdAsync(updatedPortfolioData.Id, updatedPortfolioData.OwnerId))
            .ReturnsAsync(originalPortfolio);
        _factory.MockPortfolioRepo
            .Setup(repo =>
                repo.UpdateAsync(It.IsAny<PortfolioTable>(), It.IsAny<Expression<Func<PortfolioTable, object>>>()))
            .Returns(Task.CompletedTask);
        _factory.MockPortfolioRepo
            .Setup(repo => repo.GetByIdAndOwnerIdAsync(originalPortfolio.Id, originalPortfolio.OwnerId))
            .ReturnsAsync(new PortfolioTable
            {
                Id = 400,
                Name = "Updated Portfolio",
                OwnerId = 4,
                CreatedAt = originalPortfolio.CreatedAt
            });
        
        var response = await _service.UpdatePortfolio(request, _serverCallContext);
        
        Assert.IsNotNull(response);
        Assert.IsNotNull(response.Portfolio);
        Assert.AreEqual("Updated Portfolio", response.Portfolio.Name);
    }

    [Test]
    public void UpdatePortfolio_NotFound_ThrowsRpcException()
    {
        // Arrange
        var updatedPortfolioData = new Portfolio
        {
            Id = 400,
            Name = "Nonexistent Portfolio",
            OwnerId = 4
        };
        var request = new UpdatePortfolioRequest { Portfolio = updatedPortfolioData };

        _factory.MockPortfolioRepo
            .Setup(repo => repo.GetByIdAndOwnerIdAsync(updatedPortfolioData.Id, updatedPortfolioData.OwnerId))
            .ReturnsAsync((PortfolioTable)null);

        // Act & Assert
        var ex = Assert.ThrowsAsync<RpcException>(async () =>
            await _service.UpdatePortfolio(request, _serverCallContext));
        Assert.AreEqual(StatusCode.NotFound, ex.Status.StatusCode);
    }

    [Test]
    public async Task DeletePortfolio_Success()
    {
        // Arrange
        var request = new DeletePortfolioRequest { Id = 600, OwnerId = 6 };
        var portfolioTable = new PortfolioTable
        {
            Id = 600,
            Name = "Portfolio to Delete",
            OwnerId = 6,
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        _factory.MockPortfolioRepo
            .Setup(repo => repo.GetByIdAndOwnerIdAsync(request.Id, request.OwnerId))
            .ReturnsAsync(portfolioTable);
        _factory.MockPortfolioRepo
            .Setup(repo => repo.DeleteAsync(request.Id))
            .Returns(Task.CompletedTask);

        // Act
        var response = await _service.DeletePortfolio(request, _serverCallContext);

        // Assert
        Assert.IsNotNull(response);
        Assert.IsTrue(response.Result.Success);
    }

    [Test]
    public void DeletePortfolio_NotFound_ThrowsRpcException()
    {
        // Arrange
        var request = new DeletePortfolioRequest { Id = 700, OwnerId = 7 };
        _factory.MockPortfolioRepo
            .Setup(repo => repo.GetByIdAndOwnerIdAsync(request.Id, request.OwnerId))
            .ReturnsAsync((PortfolioTable)null);

        // Act & Assert
        var ex = Assert.ThrowsAsync<RpcException>(async () =>
            await _service.DeletePortfolio(request, _serverCallContext));
        Assert.AreEqual(StatusCode.NotFound, ex.Status.StatusCode);
    }

    [Test]
    public async Task ConnectWallets_Success()
    {
        var request = new ConnectWalletsRequest
        {
            PortfolioId = 400,
            Wallets =
            {
                new WalletConnectEntity() {CaipAddress = "test", ConnectionType = 1, Connector = "meta", Name = "test", WalletAddress = "addr1"},
                new WalletConnectEntity() {CaipAddress = "test", ConnectionType = 1, Connector = "meta", Name = "test", WalletAddress = "addr2"}
            },
            OwnerId = 4
        };

        int counter = 1;
        
        _factory.MockPortfolioRepo
            .Setup(repo => repo.GetByIdAndOwnerIdAsync(request.PortfolioId, request.OwnerId))
            .ReturnsAsync(new PortfolioTable(){OwnerId = 4, Id = 400});
        
        _factory.MockWalletRepo
            .Setup(repo => repo.CreateAsync(It.IsAny<WalletTable>()))
            .ReturnsAsync((WalletTable wallet) =>
            {
                wallet.Id = counter++;
                return wallet;
            });
        
        var response = await _service.ConnectWallets(request, _serverCallContext);
        
        Assert.IsNotNull(response);
        Assert.AreEqual(2, response.Wallets.Count);
        Assert.AreEqual("addr1", response.Wallets[0].WalletAddress);
        Assert.AreEqual("addr2", response.Wallets[1].WalletAddress);
    }

    [Test]
    public async Task GetPortfolioInfo_Success_WithWallets()
    {
        // Arrange
        var request = new GetPortfolioInfoRequest { PortfolioId = 900, OwnerId = 9 };
        var portfolioTable = new PortfolioTable
        {
            Id = 900,
            Name = "Portfolio Info",
            OwnerId = 9,
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };
        var walletList = new List<WalletTable>
        {
            new WalletTable
            {
                Id = 1, PortfolioId = 900, WalletAddress = "addr1",
                CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            },
            new WalletTable
            {
                Id = 2, PortfolioId = 900, WalletAddress = "addr2",
                CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            }
        };

        _factory.MockPortfolioRepo
            .Setup(repo => repo.GetByIdAndOwnerIdAsync(request.PortfolioId, request.OwnerId))
            .ReturnsAsync(portfolioTable);
        _factory.MockWalletRepo
            .Setup(repo => repo.GetVisibleByPortfolioIdAsync(request.PortfolioId))
            .ReturnsAsync(walletList);
        
        var dummyWalletCoinsResponse = new GetWalletCoinsResponse();
        dummyWalletCoinsResponse.Coins.Add(new Coin { Name = "Coin1" });
        _factory.MockWalletServiceClient
            .Setup(client => client.GetWalletCoinsAsync(
                It.IsAny<Cryptic.BlockchainInteraction.Models.Requests.GetWalletCoinsRequest>(),
                null, null, It.IsAny<CancellationToken>()))
            .Returns(new AsyncUnaryCall<GetWalletCoinsResponse>(
                Task.FromResult(dummyWalletCoinsResponse),
                Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess,
                () => new Metadata(),
                () => { }));
        var dummyCalcResponse = new CalculateWalletResponse();
        dummyCalcResponse.WalletCoins.Add(new WalletCoinResult() { DollarValue = "123"});
        _factory.MockPortfolioAnalyticServiceClient
            .Setup(client => client.GetAssetAllocationsAsync(
                It.IsAny<CalculateWalletRequest>(),
                null, null, It.IsAny<CancellationToken>()))
            .Returns(new AsyncUnaryCall<CalculateWalletResponse>(
                Task.FromResult(dummyCalcResponse),
                Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess,
                () => new Metadata(),
                () => { }));

        // Act
        var response = await _service.GetPortfolioInfo(request, _serverCallContext);

        // Assert
        Assert.IsNotNull(response);
        Assert.IsNotNull(response.Portfolio);
        Assert.IsNotNull(response.WalletInfo);
        Assert.IsTrue(response.Result.Success);
    }

    [Test]
    public async Task PatchWalletVisibility_Success()
    {
        // Arrange
        var request = new PatchWalletVisibilityRequest
        {
            PortfolioId = 920,
            WalletId = 1,
            Visibility = 2
        };

        _factory.MockWalletRepo
            .Setup(repo => repo.UpdateVisibilityAsync(request.PortfolioId, request.WalletId, request.Visibility))
            .ReturnsAsync(true);

        // Act
        var response = await _service.PatchWalletVisibility(request, _serverCallContext);

        // Assert
        Assert.IsNotNull(response);
        Assert.IsTrue(response.Result.Success);
    }

    [Test]
    public void PatchWalletVisibility_InvalidArguments_ThrowsRpcException()
    {
        // Arrange: якщо PortfolioId або WalletId <= 0
        var request = new PatchWalletVisibilityRequest
        {
            PortfolioId = 0,
            WalletId = 0,
            Visibility = 2
        };

        // Act & Assert
        var ex = Assert.ThrowsAsync<RpcException>(async () =>
            await _service.PatchWalletVisibility(request, _serverCallContext));
        Assert.AreEqual(StatusCode.InvalidArgument, ex.Status.StatusCode);
    }

    [Test]
    public async Task GetWalletsByPortfolioId_Success()
    {
        var request = new GetWalletsByPortfolioIdRequest { PortfolioId = 930 };
        var walletList = new List<WalletTable>
        {
            new WalletTable
            {
                Id = 1, PortfolioId = 930, WalletAddress = "addr1", Name = "test", ConnectionType = 1, CaipAddress = "test", Connector = "test",
                CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            },
            new WalletTable
            {
                Id = 2, PortfolioId = 930, WalletAddress = "addr2", Name = "test", ConnectionType = 1, CaipAddress = "test", Connector = "test",
                CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            }
        };

        _factory.MockWalletRepo
            .Setup(repo => repo.GetByPortfolioIdAsync(request.PortfolioId))
            .ReturnsAsync(walletList);

        // Act
        var response = await _service.GetWalletsByPortfolioId(request, _serverCallContext);

        // Assert
        Assert.IsNotNull(response);
        Assert.AreEqual(2, response.Wallets.Count);
    }

    [Test]
    public void GetWalletsByPortfolioId_InvalidPortfolioId_ThrowsRpcException()
    {
        // Arrange
        var request = new GetWalletsByPortfolioIdRequest { PortfolioId = 0 };

        // Act & Assert
        var ex = Assert.ThrowsAsync<RpcException>(async () =>
            await _service.GetWalletsByPortfolioId(request, _serverCallContext));
        Assert.AreEqual(StatusCode.InvalidArgument, ex.Status.StatusCode);
    }

    [Test]
    public async Task GetPortfolioCalculation_Success()
    {
        var request = new GetPortfolioCalculationRequest { PortfolioId = 940 };
        var portfolioTable = new PortfolioTable
        {
            Id = 940,
            Name = "Calc Portfolio",
            OwnerId = 10,
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };
        var walletList = new List<WalletTable>
        {
            new WalletTable
            {
                Id = 1, PortfolioId = 940, WalletAddress = "addr1",
                CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            }
        };

        _factory.MockPortfolioRepo
            .Setup(repo => repo.GetByIdAsync(request.PortfolioId))
            .ReturnsAsync(portfolioTable);
        _factory.MockWalletRepo
            .Setup(repo => repo.GetVisibleByPortfolioIdAsync(request.PortfolioId))
            .ReturnsAsync(walletList);

        var dummyWalletCoinsResponse = new GetWalletCoinsResponse();
        dummyWalletCoinsResponse.Coins.Add(new Coin { Name = "Coin1" });
        _factory.MockWalletServiceClient
            .Setup(client => client.GetWalletCoinsAsync(
                It.IsAny<Cryptic.BlockchainInteraction.Models.Requests.GetWalletCoinsRequest>(),
                null, null, It.IsAny<CancellationToken>()))
            .Returns(new AsyncUnaryCall<GetWalletCoinsResponse>(
                Task.FromResult(dummyWalletCoinsResponse),
                Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess,
                () => new Metadata(),
                () => { }));
        var dummyCalcResponse = new CalculateWalletResponse();
        dummyCalcResponse.WalletCoins.Add(new WalletCoinResult() { DollarValue = "2"});
        _factory.MockPortfolioAnalyticServiceClient
            .Setup(client => client.GetAssetAllocationsAsync(
                It.IsAny<CalculateWalletRequest>(),
                null, null, It.IsAny<CancellationToken>()))
            .Returns(new AsyncUnaryCall<CalculateWalletResponse>(
                Task.FromResult(dummyCalcResponse),
                Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess,
                () => new Metadata(),
                () => { }));
        
        var response = await _service.GetPortfolioCalculation(request, _serverCallContext);

        // Assert
        Assert.IsNotNull(response);
        Assert.IsNotNull(response.Portfolio);
        Assert.IsTrue(response.Result.Success);
        Assert.IsTrue(response.CalculatedCoins.Count > 0);
    }

    [Test]
    public async Task GetPortfolioCalculation_NoWallets_ReturnsPortfolioOnly()
    {
        // Arrange
        var request = new GetPortfolioCalculationRequest { PortfolioId = 950 };
        var portfolioTable = new PortfolioTable
        {
            Id = 950,
            Name = "Calc Portfolio No Wallets",
            OwnerId = 10,
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        _factory.MockPortfolioRepo
            .Setup(repo => repo.GetByIdAsync(request.PortfolioId))
            .ReturnsAsync(portfolioTable);
        _factory.MockWalletRepo
            .Setup(repo => repo.GetVisibleByPortfolioIdAsync(request.PortfolioId))
            .ReturnsAsync(new List<WalletTable>());

        // Act
        var response = await _service.GetPortfolioCalculation(request, _serverCallContext);

        // Assert
        Assert.IsNotNull(response);
        Assert.IsNotNull(response.Portfolio);
        Assert.IsTrue(response.Result.Success);
        Assert.IsEmpty(response.CalculatedCoins);
    }
}
using System.Linq.Expressions;
using Cryptic.PortfolioConfiguration.Models.Requests;
using Cryptic.PortfolioConfiguration.Models.Responses;
using CrypticPortfolioConfiguration.Database.Tables;
using CrypticPortfolioConfiguration.Services.gRpc;
using Grpc.Core;
using Moq;
using TechTalk.SpecFlow;

namespace CrypticTests.Cucumber
{
    [Binding]
    public class PortfolioServiceSteps
    {
        private readonly PortfolioServiceFactory _factory;
        private PortfolioServiceImpl _service;
        private ServerCallContext _serverCallContext;
        private ArgumentException _exception;
        
        private CreatePortfolioRequest _createRequest;
        private GetPortfolioRequest _getRequest;
        private UpdatePortfolioRequest _updateRequest;
        private DeletePortfolioRequest _deleteRequest;
        private ConnectWalletsRequest _connectWalletsRequest;

        private object _response;

        public PortfolioServiceSteps()
        {
            _factory = new PortfolioServiceFactory();
            _service = _factory.Create(
                setupPortfolioRepo: repo =>
                {
                    repo.Setup(r => r.CreateAsync(It.IsAny<PortfolioTable>()))
                        .ReturnsAsync((PortfolioTable portfolio) =>
                        {
                            portfolio.Id = 100;
                            return portfolio;
                        });

                    repo.Setup(r => r.GetByIdAndOwnerIdAsync(It.IsAny<int>(), It.IsAny<int>()))
                        .ReturnsAsync((int id, int ownerId) =>
                            new PortfolioTable
                            {
                                Id = id,
                                Name = id == 200 ? "Existing Portfolio" : "Old Portfolio",
                                OwnerId = ownerId,
                                CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                            });

                    repo.Setup(r =>
                        r.UpdateAsync(It.IsAny<PortfolioTable>(), It.IsAny<Expression<Func<PortfolioTable, object>>>()))
                        .Returns(Task.CompletedTask);

                    repo.Setup(r => r.DeleteAsync(It.IsAny<int>()))
                        .Returns(Task.CompletedTask);
                }
            );
            
            _factory.MockWalletRepo
                .Setup(r => r.CreateAsync(It.IsAny<WalletTable>()))
                .ReturnsAsync((WalletTable wallet) =>
                {
                    wallet.Id = 1;
                    return wallet;
                });

            _serverCallContext = CreateServerCallContext();
        }

        private ServerCallContext CreateServerCallContext()
        {
            var metadata = new Metadata();
            var mockContext = new Mock<ServerCallContext>();
            return mockContext.Object;
        }

        [Given(@"I have a portfolio creation request with name ""(.*)"" and owner id (\d+)")]
        public void GivenIHaveAPortfolioCreationRequestWithNameAndOwnerId(string name, int ownerId)
        {
            _createRequest = new CreatePortfolioRequest { Name = name, OwnerId = ownerId };
        }

        [When(@"I send the request to create a portfolio")]
        public async Task WhenISendTheRequestToCreateAPortfolio()
        {
            _response = await _service.CreatePortfolio(_createRequest, _serverCallContext);
        }

        [Then(@"the portfolio should be created with id (\d+) and name ""(.*)""")]
        public void ThenThePortfolioShouldBeCreatedWithIdAndName(int expectedId, string expectedName)
        {
            var response = _response as GetPortfolioResponse;
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Portfolio);
            Assert.AreEqual(expectedId, response.Portfolio.Id);
            Assert.AreEqual(expectedName, response.Portfolio.Name);
        }
        
        [Given(@"I have a portfolio creation request with name ""(.*)"" and owner id (\d+) for throw ex")]
        public void GivenIHaveAPortfolioCreationRequestWithNameAndOwnerIdException(string name, int ownerId)
        {
            _createRequest = new CreatePortfolioRequest { Name = name, OwnerId = ownerId };
        }

        [When(@"I send the request to create a portfolio for throw ex")]
        public async Task WhenISendTheRequestToCreateAPortfolioException()
        {
            try
            {
                _response = await _service.CreatePortfolio(_createRequest, _serverCallContext);
            }
            catch (ArgumentException e)
            {
                _exception = e;
            }
        }

        [Then(@"the method that create portfolio should throw argumet exception")]
        public void ThenThePortfolioShouldBeCreatedWithIdAndNameException()
        {
            Assert.IsNotNull(_exception);
            Assert.IsInstanceOf<ArgumentException>(_exception);
        }

        [Given(@"an existing portfolio with id (\d+), name ""(.*)"", and owner id (\d+) exists")]
        public void GivenAnExistingPortfolioWithIdNameAndOwnerIdExists(int id, string name, int ownerId)
        {
            _factory.MockPortfolioRepo
                .Setup(r => r.GetByIdAndOwnerIdAsync(id, ownerId))
                .ReturnsAsync(new PortfolioTable
                {
                    Id = id,
                    Name = name,
                    OwnerId = ownerId,
                    CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                });
        }
        
        [Given(@"an existing portfolio with id (\d+) and owner id (\d+) exists")]
        public void GivenAnExistingPortfolioWithIdAndOwnerIdExists(int id, int ownerId)
        {
            _factory.MockPortfolioRepo
                .Setup(r => r.GetByIdAndOwnerIdAsync(id, ownerId))
                .ReturnsAsync(new PortfolioTable
                {
                    Id = id,
                    Name = "Default Portfolio",
                    OwnerId = ownerId,
                    CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                });
        }

        [When(@"I request the portfolio with id (\d+) and owner id (\d+)")]
        public async Task WhenIRequestThePortfolioWithIdAndOwnerId(int id, int ownerId)
        {
            _getRequest = new GetPortfolioRequest { Id = id, OwnerId = ownerId };
            _response = await _service.GetPortfolio(_getRequest, _serverCallContext);
        }

        [Then(@"the returned portfolio should have id (\d+) and name ""(.*)""")]
        public void ThenTheReturnedPortfolioShouldHaveIdAndName(int expectedId, string expectedName)
        {
            var response = _response as GetPortfolioResponse;
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Portfolio);
            Assert.AreEqual(expectedId, response.Portfolio.Id);
            Assert.AreEqual(expectedName, response.Portfolio.Name);
        }

        [Given(@"I have an update request with portfolio id (\d+) and new name ""(.*)""")]
        public void GivenIHaveAnUpdateRequestWithPortfolioIdAndNewName(int id, string newName)
        {
            _updateRequest = new UpdatePortfolioRequest
            {
                Portfolio = new Portfolio
                {
                    Id = id,
                    Name = newName,
                    OwnerId = 1,
                    CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                }
            };
        }

        [When(@"I send the update request")]
        public async Task WhenISendTheUpdateRequest()
        {
            _response = await _service.UpdatePortfolio(_updateRequest, _serverCallContext);
        }

        [Then(@"the portfolio should be updated to name ""(.*)""")]
        public void ThenThePortfolioShouldBeUpdatedToName(string expectedName)
        {
            var response = _response as GetPortfolioResponse;
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Portfolio);
            Assert.AreEqual(response.Portfolio.Name, response.Portfolio.Name);
        }

        [Given(@"I have a delete request for portfolio with id (\d+) and owner id (\d+)")]
        public void GivenIHaveADeleteRequestForPortfolioWithIdAndOwnerId(int id, int ownerId)
        {
            _deleteRequest = new DeletePortfolioRequest { Id = id, OwnerId = ownerId };
        }

        [When(@"I send a delete request")]
        public async Task WhenISendADeleteRequest()
        {
            _response = await _service.DeletePortfolio(_deleteRequest, _serverCallContext);
        }

        [Then(@"the portfolio should be deleted successfully")]
        public void ThenThePortfolioShouldBeDeletedSuccessfully()
        {
            var response = _response as DeletePortfolioResponse;
            Assert.IsNotNull(response);
            Assert.IsTrue(response.Result.Success);
        }

        [Given(@"I have a connect wallets request with portfolio id (\d+), connection type (\d+) and wallet addresses ""(.*)""")]
        public void GivenIHaveAConnectWalletsRequest(int portfolioId, int connectionType, string walletAddressesCsv)
        {
            var addresses = walletAddressesCsv.Split(',').Select(a => a.Trim()).ToList();
            _connectWalletsRequest = new ConnectWalletsRequest
            {
                PortfolioId = 400,
                Wallets =
                {
                    new WalletConnectEntity() {CaipAddress = "test", ConnectionType = 1, Connector = "meta", Name = "test", WalletAddress = addresses[0]},
                    new WalletConnectEntity() {CaipAddress = "test", ConnectionType = 1, Connector = "meta", Name = "test", WalletAddress = addresses[1]}
                },
                OwnerId = 4
            };
        }

        [When(@"I send the connect wallets request")]
        public async Task WhenISendTheConnectWalletsRequest()
        {
            _response = await _service.ConnectWallets(_connectWalletsRequest, _serverCallContext);
        }

        [Then(@"the response should contain (\d+) wallets with addresses ""(.*)""")]
        public void ThenTheResponseShouldContainWallets(int expectedCount, string expectedWalletAddressesCsv)
        {
            var response = _response as ConnectWalletsResponse;
            Assert.IsNotNull(response);
            Assert.AreEqual(expectedCount, response.Wallets.Count);

            var expectedAddresses = expectedWalletAddressesCsv.Split(',').Select(a => a.Trim()).ToList();
            CollectionAssert.AreEquivalent(expectedAddresses, response.Wallets.Select(w => w.WalletAddress).ToList());
        }
    }
}

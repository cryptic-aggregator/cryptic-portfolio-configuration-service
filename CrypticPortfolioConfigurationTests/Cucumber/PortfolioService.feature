Feature: PortfolioService
As a client
I want to manage portfolios via the gRPC service
So that I can create, retrieve, update and delete portfolios

    Scenario: Create a new portfolio successfully
        Given I have a portfolio creation request with name "Test Portfolio" and owner id 1
        When I send the request to create a portfolio
        Then the portfolio should be created with id 100 and name "Test Portfolio"
        
    Scenario: Retrieve an existing portfolio
        Given an existing portfolio with id 200, name "Existing Portfolio", and owner id 2 exists
        When I request the portfolio with id 200 and owner id 2
        Then the returned portfolio should have id 200 and name "Existing Portfolio"

    Scenario: Update a portfolio successfully
        Given an existing portfolio with id 300, name "Old Portfolio", and owner id 3 exists
        And I have an update request with portfolio id 300 and new name "Updated Portfolio"
        When I send the update request
        Then the portfolio should be updated to name "Updated Portfolio"

    Scenario: Delete a portfolio successfully
        Given an existing portfolio with id 400 and owner id 4 exists
        And I have a delete request for portfolio with id 400 and owner id 4
        When I send a delete request
        Then the portfolio should be deleted successfully

    Scenario: Connect wallets successfully
        Given I have a connect wallets request with portfolio id 800, connection type 1 and wallet addresses "addr1, addr2"
        When I send the connect wallets request
        Then the response should contain 2 wallets with addresses "addr1, addr2"
        
        
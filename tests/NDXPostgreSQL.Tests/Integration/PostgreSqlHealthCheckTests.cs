using FluentAssertions;
using NDXPostgreSQL.Tests.Fixtures;
using Xunit;

namespace NDXPostgreSQL.Tests.Integration;

/// <summary>
/// Tests d'intégration pour PostgreSqlHealthCheck.
/// </summary>
[Collection("PostgreSQL")]
public class PostgreSqlHealthCheckTests
{
    private readonly PostgreSqlFixture _fixture;

    public PostgreSqlHealthCheckTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CheckHealthAsync_WithValidConnection_ShouldReturnHealthy()
    {
        // Arrange
        var healthCheck = new PostgreSqlHealthCheck(_fixture.Factory);

        // Act
        var result = await healthCheck.CheckHealthAsync();

        // Assert
        result.IsHealthy.Should().BeTrue();
        result.Message.Should().Contain("opérationnelle");
        result.ResponseTime.Should().BePositive();
        result.Exception.Should().BeNull();
    }

    [Fact]
    public async Task CheckHealthAsync_WithInvalidConnection_ShouldReturnUnhealthy()
    {
        // Arrange
        var invalidOptions = new PostgreSqlConnectionOptions
        {
            Host = "invalid-host-that-does-not-exist",
            Port = 5432,
            Database = "nonexistent",
            Username = "invalid",
            Password = "invalid",
            ConnectionTimeoutSeconds = 2
        };
        var invalidFactory = new PostgreSqlConnectionFactory(invalidOptions);
        var healthCheck = new PostgreSqlHealthCheck(invalidFactory);

        // Act
        var result = await healthCheck.CheckHealthAsync();

        // Assert
        result.IsHealthy.Should().BeFalse();
        result.Exception.Should().NotBeNull();
        result.ResponseTime.Should().BePositive();
    }

    [Fact]
    public async Task GetServerInfoAsync_ShouldReturnServerInformation()
    {
        // Arrange
        var healthCheck = new PostgreSqlHealthCheck(_fixture.Factory);

        // Act
        var serverInfo = await healthCheck.GetServerInfoAsync();

        // Assert
        serverInfo.Should().NotBeNull();
        serverInfo.Version.Should().NotBeNullOrEmpty();
        serverInfo.Version.Should().Contain("PostgreSQL");
        serverInfo.ServerVersion.Should().NotBeNullOrEmpty();
        serverInfo.CurrentDatabase.Should().Be("ndxpostgresql_test");
        serverInfo.CurrentUser.Should().Be("testuser");
        serverInfo.ConnectionPid.Should().BePositive();
    }

    [Fact]
    public async Task CheckHealthAsync_WithCancellation_ShouldRespectCancellation()
    {
        // Arrange
        var healthCheck = new PostgreSqlHealthCheck(_fixture.Factory);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = async () => await healthCheck.CheckHealthAsync(cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}

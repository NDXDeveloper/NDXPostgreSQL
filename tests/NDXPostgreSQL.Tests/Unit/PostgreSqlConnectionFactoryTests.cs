using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace NDXPostgreSQL.Tests.Unit;

/// <summary>
/// Tests unitaires pour PostgreSqlConnectionFactory.
/// </summary>
public class PostgreSqlConnectionFactoryTests
{
    private readonly PostgreSqlConnectionOptions _defaultOptions;
    private readonly Mock<ILoggerFactory> _mockLoggerFactory;

    public PostgreSqlConnectionFactoryTests()
    {
        _defaultOptions = new PostgreSqlConnectionOptions
        {
            Host = "localhost",
            Port = 5432,
            Database = "testdb",
            Username = "testuser",
            Password = "testpassword"
        };

        _mockLoggerFactory = new Mock<ILoggerFactory>();
        _mockLoggerFactory
            .Setup(x => x.CreateLogger(It.IsAny<string>()))
            .Returns(Mock.Of<ILogger>());
    }

    [Fact]
    public void Constructor_WithOptions_ShouldNotThrow()
    {
        // Act
        var factory = new PostgreSqlConnectionFactory(_defaultOptions, _mockLoggerFactory.Object);

        // Assert
        factory.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithConnectionString_ShouldNotThrow()
    {
        // Arrange
        var connectionString = "Host=localhost;Database=test;Username=user;Password=pass";

        // Act
        var factory = new PostgreSqlConnectionFactory(connectionString, _mockLoggerFactory.Object);

        // Assert
        factory.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullOptions_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new PostgreSqlConnectionFactory((PostgreSqlConnectionOptions)null!, _mockLoggerFactory.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("defaultOptions");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithEmptyConnectionString_ShouldThrowArgumentException(string? connectionString)
    {
        // Act
        var act = () => new PostgreSqlConnectionFactory(connectionString!, _mockLoggerFactory.Object);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("connectionString");
    }

    [Fact]
    public void CreateConnection_ShouldReturnNewConnection()
    {
        // Arrange
        var factory = new PostgreSqlConnectionFactory(_defaultOptions, _mockLoggerFactory.Object);

        // Act
        var connection = factory.CreateConnection();

        // Assert
        connection.Should().NotBeNull();
        connection.Should().BeAssignableTo<IPostgreSqlConnection>();
    }

    [Fact]
    public void CreateConnection_MultipleCalls_ShouldReturnDifferentInstances()
    {
        // Arrange
        var factory = new PostgreSqlConnectionFactory(_defaultOptions, _mockLoggerFactory.Object);

        // Act
        var connection1 = factory.CreateConnection();
        var connection2 = factory.CreateConnection();

        // Assert
        connection1.Should().NotBeSameAs(connection2);
    }

    [Fact]
    public void CreateConnection_MultipleCalls_ShouldHaveIncrementalIds()
    {
        // Arrange
        var factory = new PostgreSqlConnectionFactory(_defaultOptions, _mockLoggerFactory.Object);

        // Act
        var connection1 = factory.CreateConnection();
        var connection2 = factory.CreateConnection();
        var connection3 = factory.CreateConnection();

        // Assert
        connection2.Id.Should().BeGreaterThan(connection1.Id);
        connection3.Id.Should().BeGreaterThan(connection2.Id);
    }

    [Fact]
    public void CreateConnection_WithCustomOptions_ShouldUseCustomOptions()
    {
        // Arrange
        var factory = new PostgreSqlConnectionFactory(_defaultOptions, _mockLoggerFactory.Object);
        var customOptions = new PostgreSqlConnectionOptions
        {
            Host = "custom-host",
            Database = "custom-db",
            Username = "custom-user",
            Password = "custom-pass",
            IsPrimaryConnection = true
        };

        // Act
        var connection = factory.CreateConnection(customOptions);

        // Assert
        connection.IsPrimaryConnection.Should().BeTrue();
    }

    [Fact]
    public void CreateConnection_WithConfigureAction_ShouldApplyConfiguration()
    {
        // Arrange
        var factory = new PostgreSqlConnectionFactory(_defaultOptions, _mockLoggerFactory.Object);

        // Act
        var connection = factory.CreateConnection(options =>
        {
            options.IsPrimaryConnection = true;
        });

        // Assert
        connection.IsPrimaryConnection.Should().BeTrue();
    }

    [Fact]
    public void CreatePrimaryConnection_ShouldReturnPrimaryConnection()
    {
        // Arrange
        var factory = new PostgreSqlConnectionFactory(_defaultOptions, _mockLoggerFactory.Object);

        // Act
        var connection = factory.CreatePrimaryConnection();

        // Assert
        connection.IsPrimaryConnection.Should().BeTrue();
    }

    [Fact]
    public void CreateConnection_WithNullOptions_ShouldThrowArgumentNullException()
    {
        // Arrange
        var factory = new PostgreSqlConnectionFactory(_defaultOptions, _mockLoggerFactory.Object);

        // Act
        var act = () => factory.CreateConnection((PostgreSqlConnectionOptions)null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("options");
    }

    [Fact]
    public void CreateConnection_WithNullConfigureAction_ShouldThrowArgumentNullException()
    {
        // Arrange
        var factory = new PostgreSqlConnectionFactory(_defaultOptions, _mockLoggerFactory.Object);

        // Act
        var act = () => factory.CreateConnection((Action<PostgreSqlConnectionOptions>)null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("configure");
    }

    [Fact]
    public void CreateConnection_ShouldNotModifyDefaultOptions()
    {
        // Arrange
        var factory = new PostgreSqlConnectionFactory(_defaultOptions, _mockLoggerFactory.Object);
        var originalHost = _defaultOptions.Host;

        // Act
        var connection = factory.CreateConnection(options =>
        {
            options.Host = "modified-host";
        });

        // Assert
        _defaultOptions.Host.Should().Be(originalHost);
    }

    [Fact]
    public void CreateConnection_WithoutLoggerFactory_ShouldWork()
    {
        // Arrange
        var factory = new PostgreSqlConnectionFactory(_defaultOptions);

        // Act
        var connection = factory.CreateConnection();

        // Assert
        connection.Should().NotBeNull();
    }
}

using FluentAssertions;
using Xunit;

namespace NDXPostgreSQL.Tests.Unit;

/// <summary>
/// Tests unitaires pour PostgreSqlConnectionOptions.
/// </summary>
public class PostgreSqlConnectionOptionsTests
{
    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        // Arrange & Act
        var options = new PostgreSqlConnectionOptions();

        // Assert
        options.Host.Should().Be("localhost");
        options.Port.Should().Be(5432);
        options.Database.Should().BeEmpty();
        options.Username.Should().BeEmpty();
        options.Password.Should().BeEmpty();
        options.ConnectionString.Should().BeNull();
        options.IsPrimaryConnection.Should().BeFalse();
        options.DisableAutoClose.Should().BeFalse();
        options.Pooling.Should().BeTrue();
        options.MinPoolSize.Should().Be(0);
        options.MaxPoolSize.Should().Be(100);
        options.AutoCloseTimeoutMs.Should().Be(60_000);
        options.ConnectionTimeoutSeconds.Should().Be(30);
        options.CommandTimeoutSeconds.Should().Be(30);
        options.LockTimeoutMs.Should().Be(120_000);
        options.SslMode.Should().Be("Prefer");
        options.UseSsl.Should().BeFalse();
        options.ApplicationName.Should().Be("NDXPostgreSQL");
        options.Multiplexing.Should().BeFalse();
    }

    [Fact]
    public void BuildConnectionString_WithIndividualParameters_ShouldBuildCorrectly()
    {
        // Arrange
        var options = new PostgreSqlConnectionOptions
        {
            Host = "myserver.local",
            Port = 5433,
            Database = "mydb",
            Username = "myuser",
            Password = "mypassword"
        };

        // Act
        var connectionString = options.BuildConnectionString();

        // Assert
        connectionString.Should().Contain("Host=myserver.local");
        connectionString.Should().Contain("Port=5433");
        connectionString.Should().Contain("Database=mydb");
        connectionString.Should().Contain("Username=myuser");
        connectionString.Should().Contain("Password=mypassword");
    }

    [Fact]
    public void BuildConnectionString_WithConnectionStringSet_ShouldReturnConnectionString()
    {
        // Arrange
        var expectedConnectionString = "Host=override;Database=db;Username=user;Password=pass";
        var options = new PostgreSqlConnectionOptions
        {
            Host = "ignored",
            Database = "ignored",
            ConnectionString = expectedConnectionString
        };

        // Act
        var connectionString = options.BuildConnectionString();

        // Assert
        connectionString.Should().Be(expectedConnectionString);
    }

    [Fact]
    public void BuildConnectionString_WithPoolingOptions_ShouldIncludePoolingSettings()
    {
        // Arrange
        var options = new PostgreSqlConnectionOptions
        {
            Host = "localhost",
            Database = "testdb",
            Username = "user",
            Password = "pass",
            Pooling = true,
            MinPoolSize = 5,
            MaxPoolSize = 50
        };

        // Act
        var connectionString = options.BuildConnectionString();

        // Assert
        connectionString.Should().Contain("Pooling=True");
        connectionString.Should().Contain("Minimum Pool Size=5");
        connectionString.Should().Contain("Maximum Pool Size=50");
    }

    [Fact]
    public void BuildConnectionString_WithSslEnabled_ShouldIncludeSslSettings()
    {
        // Arrange
        var options = new PostgreSqlConnectionOptions
        {
            Host = "localhost",
            Database = "testdb",
            Username = "user",
            Password = "pass",
            UseSsl = true,
            SslMode = "Require"
        };

        // Act
        var connectionString = options.BuildConnectionString();

        // Assert
        connectionString.Should().Contain("SSL Mode=Require");
    }

    [Fact]
    public void BuildConnectionString_WithApplicationName_ShouldIncludeAppName()
    {
        // Arrange
        var options = new PostgreSqlConnectionOptions
        {
            Host = "localhost",
            Database = "testdb",
            Username = "user",
            Password = "pass",
            ApplicationName = "MyTestApp"
        };

        // Act
        var connectionString = options.BuildConnectionString();

        // Assert
        connectionString.Should().Contain("Application Name=MyTestApp");
    }

    [Fact]
    public void BuildConnectionString_WithTimeouts_ShouldIncludeTimeoutSettings()
    {
        // Arrange
        var options = new PostgreSqlConnectionOptions
        {
            Host = "localhost",
            Database = "testdb",
            Username = "user",
            Password = "pass",
            ConnectionTimeoutSeconds = 60,
            CommandTimeoutSeconds = 120
        };

        // Act
        var connectionString = options.BuildConnectionString();

        // Assert
        connectionString.Should().Contain("Timeout=60");
        connectionString.Should().Contain("Command Timeout=120");
    }

    [Fact]
    public void Clone_ShouldCreateIndependentCopy()
    {
        // Arrange
        var original = new PostgreSqlConnectionOptions
        {
            Host = "original-host",
            Port = 5433,
            Database = "original-db",
            Username = "original-user",
            Password = "original-pass",
            IsPrimaryConnection = true,
            DisableAutoClose = true,
            Pooling = false,
            MinPoolSize = 5,
            MaxPoolSize = 50,
            AutoCloseTimeoutMs = 30_000,
            ConnectionTimeoutSeconds = 60,
            CommandTimeoutSeconds = 120,
            LockTimeoutMs = 60_000,
            SslMode = "Require",
            UseSsl = true,
            ApplicationName = "CloneTest",
            Multiplexing = true
        };

        // Act
        var clone = original.Clone();

        // Modify clone
        clone.Host = "cloned-host";
        clone.Database = "cloned-db";

        // Assert - Clone should have original values initially
        clone.Port.Should().Be(5433);
        clone.Username.Should().Be("original-user");
        clone.Password.Should().Be("original-pass");
        clone.IsPrimaryConnection.Should().BeTrue();
        clone.DisableAutoClose.Should().BeTrue();
        clone.Pooling.Should().BeFalse();
        clone.MinPoolSize.Should().Be(5);
        clone.MaxPoolSize.Should().Be(50);
        clone.AutoCloseTimeoutMs.Should().Be(30_000);
        clone.ConnectionTimeoutSeconds.Should().Be(60);
        clone.CommandTimeoutSeconds.Should().Be(120);
        clone.LockTimeoutMs.Should().Be(60_000);
        clone.SslMode.Should().Be("Require");
        clone.UseSsl.Should().BeTrue();
        clone.ApplicationName.Should().Be("CloneTest");
        clone.Multiplexing.Should().BeTrue();

        // Assert - Original should not be affected
        original.Host.Should().Be("original-host");
        original.Database.Should().Be("original-db");
    }

    [Theory]
    [InlineData("Disable")]
    [InlineData("Allow")]
    [InlineData("Prefer")]
    [InlineData("Require")]
    [InlineData("VerifyCA")]
    [InlineData("VerifyFull")]
    public void BuildConnectionString_WithDifferentSslModes_ShouldWork(string sslMode)
    {
        // Arrange
        var options = new PostgreSqlConnectionOptions
        {
            Host = "localhost",
            Database = "testdb",
            Username = "user",
            Password = "pass",
            UseSsl = true,
            SslMode = sslMode
        };

        // Act
        var connectionString = options.BuildConnectionString();

        // Assert
        connectionString.Should().Contain($"SSL Mode={sslMode}");
    }

    [Fact]
    public void BuildConnectionString_WithMultiplexing_ShouldIncludeMultiplexingSetting()
    {
        // Arrange
        var options = new PostgreSqlConnectionOptions
        {
            Host = "localhost",
            Database = "testdb",
            Username = "user",
            Password = "pass",
            Multiplexing = true
        };

        // Act
        var connectionString = options.BuildConnectionString();

        // Assert
        connectionString.Should().Contain("Multiplexing=True");
    }
}

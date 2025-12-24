using System.Data;
using FluentAssertions;
using NDXPostgreSQL.Tests.Fixtures;
using Xunit;

namespace NDXPostgreSQL.Tests.Integration;

/// <summary>
/// Tests d'intégration pour PostgreSqlConnection.
/// </summary>
[Collection("PostgreSQL")]
public class PostgreSqlConnectionTests : IAsyncLifetime
{
    private readonly PostgreSqlFixture _fixture;

    public PostgreSqlConnectionTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.CleanupTestDataAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    #region Connexion Open/Close

    [Fact]
    public async Task OpenAsync_ShouldOpenConnection()
    {
        // Arrange
        await using var connection = _fixture.Factory.CreateConnection();

        // Act
        await connection.OpenAsync();

        // Assert
        connection.State.Should().Be(ConnectionState.Open);
    }

    [Fact]
    public void Open_ShouldOpenConnectionSynchronously()
    {
        // Arrange
        using var connection = _fixture.Factory.CreateConnection();

        // Act
        connection.Open();

        // Assert
        connection.State.Should().Be(ConnectionState.Open);
    }

    [Fact]
    public async Task CloseAsync_ShouldCloseConnection()
    {
        // Arrange
        await using var connection = _fixture.Factory.CreateConnection();
        await connection.OpenAsync();

        // Act
        await connection.CloseAsync();

        // Assert
        connection.State.Should().Be(ConnectionState.Closed);
    }

    [Fact]
    public void Close_ShouldCloseConnectionSynchronously()
    {
        // Arrange
        using var connection = _fixture.Factory.CreateConnection();
        connection.Open();

        // Act
        connection.Close();

        // Assert
        connection.State.Should().Be(ConnectionState.Closed);
    }

    [Fact]
    public async Task DisposeAsync_ShouldCloseAndDisposeConnection()
    {
        // Arrange
        var connection = _fixture.Factory.CreateConnection();
        await connection.OpenAsync();

        // Act
        await connection.DisposeAsync();

        // Assert
        connection.State.Should().Be(ConnectionState.Closed);
    }

    #endregion

    #region Propriétés

    [Fact]
    public async Task Id_ShouldBeUniqueAndIncremental()
    {
        // Arrange & Act
        await using var connection1 = _fixture.Factory.CreateConnection();
        await using var connection2 = _fixture.Factory.CreateConnection();

        // Assert
        connection2.Id.Should().BeGreaterThan(connection1.Id);
    }

    [Fact]
    public async Task CreatedAt_ShouldBeSetOnCreation()
    {
        // Arrange
        var before = DateTime.UtcNow;

        // Act
        await using var connection = _fixture.Factory.CreateConnection();

        // Assert
        connection.CreatedAt.Should().BeOnOrAfter(before);
        connection.CreatedAt.Should().BeOnOrBefore(DateTime.UtcNow);
    }

    [Fact]
    public async Task State_ShouldReflectConnectionState()
    {
        // Arrange
        await using var connection = _fixture.Factory.CreateConnection();

        // Assert initial
        connection.State.Should().Be(ConnectionState.Closed);

        // Act & Assert after open
        await connection.OpenAsync();
        connection.State.Should().Be(ConnectionState.Open);

        // Act & Assert after close
        await connection.CloseAsync();
        connection.State.Should().Be(ConnectionState.Closed);
    }

    [Fact]
    public async Task Connection_ShouldExposeNativeConnection()
    {
        // Arrange
        await using var connection = _fixture.Factory.CreateConnection();

        // Assert
        connection.Connection.Should().NotBeNull();
    }

    [Fact]
    public async Task IsPrimaryConnection_ShouldBeFalseByDefault()
    {
        // Arrange & Act
        await using var connection = _fixture.Factory.CreateConnection();

        // Assert
        connection.IsPrimaryConnection.Should().BeFalse();
    }

    [Fact]
    public async Task IsPrimaryConnection_ShouldBeTrueForPrimaryConnection()
    {
        // Arrange & Act
        await using var connection = _fixture.Factory.CreatePrimaryConnection();

        // Assert
        connection.IsPrimaryConnection.Should().BeTrue();
    }

    #endregion

    #region ActionHistory

    [Fact]
    public async Task LastAction_ShouldBeUpdatedAfterOperations()
    {
        // Arrange
        await using var connection = _fixture.Factory.CreateConnection();

        // Act
        await connection.OpenAsync();

        // Assert
        connection.LastAction.Should().Contain("OpenAsync");
    }

    [Fact]
    public async Task ActionHistory_ShouldKeepLast5Actions()
    {
        // Arrange
        await using var connection = _fixture.Factory.CreateConnection();
        await connection.OpenAsync();

        // Act - Perform multiple operations
        await connection.ExecuteNonQueryAsync("SELECT 1");
        await connection.ExecuteNonQueryAsync("SELECT 2");
        await connection.ExecuteNonQueryAsync("SELECT 3");
        await connection.ExecuteNonQueryAsync("SELECT 4");
        await connection.ExecuteNonQueryAsync("SELECT 5");
        await connection.ExecuteNonQueryAsync("SELECT 6");

        // Assert
        connection.ActionHistory.Count.Should().BeLessOrEqualTo(5);
    }

    #endregion

    #region Transactions

    [Fact]
    public async Task BeginTransactionAsync_ShouldStartTransaction()
    {
        // Arrange
        await using var connection = _fixture.Factory.CreateConnection();
        await connection.OpenAsync();

        // Act
        var result = await connection.BeginTransactionAsync();

        // Assert
        result.Should().BeTrue();
        connection.IsTransactionActive.Should().BeTrue();
        connection.Transaction.Should().NotBeNull();
    }

    [Fact]
    public void BeginTransaction_ShouldStartTransactionSynchronously()
    {
        // Arrange
        using var connection = _fixture.Factory.CreateConnection();
        connection.Open();

        // Act
        var result = connection.BeginTransaction();

        // Assert
        result.Should().BeTrue();
        connection.IsTransactionActive.Should().BeTrue();
    }

    [Fact]
    public async Task CommitAsync_ShouldCommitTransaction()
    {
        // Arrange
        await using var connection = _fixture.Factory.CreateConnection();
        await connection.OpenAsync();
        await connection.BeginTransactionAsync();

        await connection.ExecuteNonQueryAsync(
            "INSERT INTO test_users (name, email) VALUES (@Name, @Email)",
            new { Name = "CommitTest", Email = "commit@test.com" });

        // Act
        await connection.CommitAsync();

        // Assert
        connection.IsTransactionActive.Should().BeFalse();
        connection.Transaction.Should().BeNull();

        // Verify data was committed
        var count = await connection.ExecuteScalarAsync<long>(
            "SELECT COUNT(*) FROM test_users WHERE email = @Email",
            new { Email = "commit@test.com" });
        count.Should().Be(1);
    }

    [Fact]
    public async Task RollbackAsync_ShouldRollbackTransaction()
    {
        // Arrange
        await using var connection = _fixture.Factory.CreateConnection();
        await connection.OpenAsync();
        await connection.BeginTransactionAsync();

        await connection.ExecuteNonQueryAsync(
            "INSERT INTO test_users (name, email) VALUES (@Name, @Email)",
            new { Name = "RollbackTest", Email = "rollback@test.com" });

        // Act
        await connection.RollbackAsync();

        // Assert
        connection.IsTransactionActive.Should().BeFalse();
        connection.Transaction.Should().BeNull();

        // Verify data was rolled back
        var count = await connection.ExecuteScalarAsync<long>(
            "SELECT COUNT(*) FROM test_users WHERE email = @Email",
            new { Email = "rollback@test.com" });
        count.Should().Be(0);
    }

    [Fact]
    public async Task Transaction_WithDifferentIsolationLevels_ShouldWork()
    {
        // Arrange
        await using var connection = _fixture.Factory.CreateConnection();
        await connection.OpenAsync();

        // Act & Assert - Test different isolation levels
        foreach (var isolationLevel in new[] { IsolationLevel.ReadCommitted, IsolationLevel.Serializable, IsolationLevel.RepeatableRead })
        {
            var result = await connection.BeginTransactionAsync(isolationLevel);
            result.Should().BeTrue();
            await connection.RollbackAsync();
        }
    }

    [Fact]
    public async Task Savepoint_ShouldAllowPartialRollback()
    {
        // Arrange
        await using var connection = _fixture.Factory.CreateConnection();
        await connection.OpenAsync();
        await connection.BeginTransactionAsync();

        // Insert first user
        await connection.ExecuteNonQueryAsync(
            "INSERT INTO test_users (name, email) VALUES (@Name, @Email)",
            new { Name = "User1", Email = "user1@test.com" });

        // Create savepoint
        await connection.SaveAsync("savepoint1");

        // Insert second user
        await connection.ExecuteNonQueryAsync(
            "INSERT INTO test_users (name, email) VALUES (@Name, @Email)",
            new { Name = "User2", Email = "user2@test.com" });

        // Act - Rollback to savepoint
        await connection.RollbackToSavepointAsync("savepoint1");

        // Commit
        await connection.CommitAsync();

        // Assert - Only first user should exist
        var count = await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM test_users");
        count.Should().Be(1);
    }

    #endregion

    #region CRUD Operations

    [Fact]
    public async Task ExecuteNonQueryAsync_Insert_ShouldReturnRowsAffected()
    {
        // Arrange
        await using var connection = _fixture.Factory.CreateConnection();
        await connection.OpenAsync();

        // Act
        var result = await connection.ExecuteNonQueryAsync(
            "INSERT INTO test_users (name, email, age) VALUES (@Name, @Email, @Age)",
            new { Name = "TestUser", Email = "test@example.com", Age = 25 });

        // Assert
        result.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteScalarAsync_ShouldReturnSingleValue()
    {
        // Arrange
        await using var connection = _fixture.Factory.CreateConnection();
        await connection.OpenAsync();

        await connection.ExecuteNonQueryAsync(
            "INSERT INTO test_users (name, email, age) VALUES (@Name, @Email, @Age)",
            new { Name = "ScalarTest", Email = "scalar@test.com", Age = 30 });

        // Act
        var result = await connection.ExecuteScalarAsync<int>(
            "SELECT age FROM test_users WHERE email = @Email",
            new { Email = "scalar@test.com" });

        // Assert
        result.Should().Be(30);
    }

    [Fact]
    public async Task ExecuteQueryAsync_ShouldReturnDataTable()
    {
        // Arrange
        await using var connection = _fixture.Factory.CreateConnection();
        await connection.OpenAsync();

        await connection.ExecuteNonQueryAsync(
            "INSERT INTO test_users (name, email) VALUES (@Name, @Email)",
            new { Name = "QueryTest1", Email = "query1@test.com" });
        await connection.ExecuteNonQueryAsync(
            "INSERT INTO test_users (name, email) VALUES (@Name, @Email)",
            new { Name = "QueryTest2", Email = "query2@test.com" });

        // Act
        var dataTable = await connection.ExecuteQueryAsync(
            "SELECT name, email FROM test_users ORDER BY name");

        // Assert
        dataTable.Rows.Count.Should().Be(2);
        dataTable.Columns.Count.Should().BeGreaterOrEqualTo(2);
    }

    [Fact]
    public async Task ExecuteReaderAsync_ShouldReturnDataReader()
    {
        // Arrange
        await using var connection = _fixture.Factory.CreateConnection();
        await connection.OpenAsync();

        await connection.ExecuteNonQueryAsync(
            "INSERT INTO test_users (name, email) VALUES (@Name, @Email)",
            new { Name = "ReaderTest", Email = "reader@test.com" });

        // Act
        await using var reader = await connection.ExecuteReaderAsync(
            "SELECT name, email FROM test_users WHERE email = @Email",
            new { Email = "reader@test.com" });

        // Assert
        reader.HasRows.Should().BeTrue();
        await reader.ReadAsync();
        reader.GetString(0).Should().Be("ReaderTest");
    }

    [Fact]
    public async Task Update_ShouldModifyExistingData()
    {
        // Arrange
        await using var connection = _fixture.Factory.CreateConnection();
        await connection.OpenAsync();

        await connection.ExecuteNonQueryAsync(
            "INSERT INTO test_users (name, email, age) VALUES (@Name, @Email, @Age)",
            new { Name = "UpdateTest", Email = "update@test.com", Age = 20 });

        // Act
        var result = await connection.ExecuteNonQueryAsync(
            "UPDATE test_users SET age = @Age WHERE email = @Email",
            new { Age = 25, Email = "update@test.com" });

        // Assert
        result.Should().Be(1);

        var newAge = await connection.ExecuteScalarAsync<int>(
            "SELECT age FROM test_users WHERE email = @Email",
            new { Email = "update@test.com" });
        newAge.Should().Be(25);
    }

    [Fact]
    public async Task Delete_ShouldRemoveData()
    {
        // Arrange
        await using var connection = _fixture.Factory.CreateConnection();
        await connection.OpenAsync();

        await connection.ExecuteNonQueryAsync(
            "INSERT INTO test_users (name, email) VALUES (@Name, @Email)",
            new { Name = "DeleteTest", Email = "delete@test.com" });

        // Act
        var result = await connection.ExecuteNonQueryAsync(
            "DELETE FROM test_users WHERE email = @Email",
            new { Email = "delete@test.com" });

        // Assert
        result.Should().Be(1);

        var count = await connection.ExecuteScalarAsync<long>(
            "SELECT COUNT(*) FROM test_users WHERE email = @Email",
            new { Email = "delete@test.com" });
        count.Should().Be(0);
    }

    #endregion

    #region Functions and Procedures

    [Fact]
    public async Task ExecuteScalarAsync_WithFunction_ShouldReturnResult()
    {
        // Arrange
        await using var connection = _fixture.Factory.CreateConnection();
        await connection.OpenAsync();

        // Act
        var result = await connection.ExecuteScalarAsync<int>(
            "SELECT add_numbers(@A, @B)",
            new { A = 5, B = 3 });

        // Assert
        result.Should().Be(8);
    }

    [Fact]
    public async Task ExecuteQueryAsync_WithFunctionReturningRecord_ShouldWork()
    {
        // Arrange
        await using var connection = _fixture.Factory.CreateConnection();
        await connection.OpenAsync();

        // Insert test data
        await connection.ExecuteNonQueryAsync(
            "INSERT INTO test_users (name, email) VALUES (@Name, @Email)",
            new { Name = "FuncUser", Email = "func@test.com" });

        var userId = await connection.ExecuteScalarAsync<int>(
            "SELECT id FROM test_users WHERE email = @Email",
            new { Email = "func@test.com" });

        await connection.ExecuteNonQueryAsync(
            "INSERT INTO test_orders (user_id, amount) VALUES (@UserId, @Amount)",
            new { UserId = userId, Amount = 100.50m });

        // Act
        var result = await connection.ExecuteQueryAsync(
            "SELECT * FROM get_user_stats(@UserId)",
            new { UserId = userId });

        // Assert
        result.Rows.Count.Should().Be(1);
        Convert.ToInt32(result.Rows[0]["total_orders"]).Should().Be(1);
    }

    [Fact]
    public async Task CallProcedure_ShouldInsertData()
    {
        // Arrange
        await using var connection = _fixture.Factory.CreateConnection();
        await connection.OpenAsync();

        // Act
        await connection.ExecuteNonQueryAsync(
            "CALL insert_test_user(@Name, @Email, @Age)",
            new { Name = "ProcUser", Email = "proc@test.com", Age = 35 });

        // Assert
        var count = await connection.ExecuteScalarAsync<long>(
            "SELECT COUNT(*) FROM test_users WHERE email = @Email",
            new { Email = "proc@test.com" });
        count.Should().Be(1);
    }

    #endregion

    #region CreateCommand

    [Fact]
    public async Task CreateCommand_ShouldCreateNativeCommand()
    {
        // Arrange
        await using var connection = _fixture.Factory.CreateConnection();
        await connection.OpenAsync();

        // Act
        using var command = connection.CreateCommand("SELECT 1");

        // Assert
        command.Should().NotBeNull();
        command.CommandText.Should().Be("SELECT 1");
    }

    [Fact]
    public async Task CreateCommand_WithTransaction_ShouldAssociateTransaction()
    {
        // Arrange
        await using var connection = _fixture.Factory.CreateConnection();
        await connection.OpenAsync();
        await connection.BeginTransactionAsync();

        // Act
        using var command = connection.CreateCommand("SELECT 1");

        // Assert
        command.Transaction.Should().NotBeNull();
    }

    #endregion

    #region ResetAutoCloseTimer

    [Fact]
    public async Task ResetAutoCloseTimer_ShouldNotThrow()
    {
        // Arrange
        await using var connection = _fixture.Factory.CreateConnection();
        await connection.OpenAsync();

        // Act
        var act = () => connection.ResetAutoCloseTimer();

        // Assert
        act.Should().NotThrow();
    }

    #endregion

    #region JSONB Support

    [Fact]
    public async Task ExecuteNonQueryAsync_WithJsonb_ShouldWork()
    {
        // Arrange
        await using var connection = _fixture.Factory.CreateConnection();
        await connection.OpenAsync();

        var metadata = "{\"role\": \"admin\", \"permissions\": [\"read\", \"write\"]}";

        // Act
        await connection.ExecuteNonQueryAsync(
            "INSERT INTO test_users (name, email, metadata) VALUES (@Name, @Email, @Metadata::jsonb)",
            new { Name = "JsonUser", Email = "json@test.com", Metadata = metadata });

        // Assert
        var result = await connection.ExecuteScalarAsync<string>(
            "SELECT metadata->>'role' FROM test_users WHERE email = @Email",
            new { Email = "json@test.com" });
        result.Should().Be("admin");
    }

    #endregion

    #region Dispose

    [Fact]
    public async Task Dispose_WithActiveTransaction_ShouldRollback()
    {
        // Arrange
        var connection = _fixture.Factory.CreateConnection();
        await connection.OpenAsync();
        await connection.BeginTransactionAsync();

        await connection.ExecuteNonQueryAsync(
            "INSERT INTO test_users (name, email) VALUES (@Name, @Email)",
            new { Name = "DisposeTest", Email = "dispose@test.com" });

        // Act
        await connection.DisposeAsync();

        // Assert - Data should not exist (transaction was rolled back)
        await using var checkConnection = _fixture.Factory.CreateConnection();
        await checkConnection.OpenAsync();
        var count = await checkConnection.ExecuteScalarAsync<long>(
            "SELECT COUNT(*) FROM test_users WHERE email = @Email",
            new { Email = "dispose@test.com" });
        count.Should().Be(0);
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_ShouldNotThrow()
    {
        // Arrange
        var connection = _fixture.Factory.CreateConnection();
        connection.Open();

        // Act
        connection.Dispose();
        var act = () => connection.Dispose();

        // Assert
        act.Should().NotThrow();
    }

    #endregion
}

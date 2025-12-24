using Microsoft.Extensions.Logging;
using Testcontainers.PostgreSql;
using Xunit;

namespace NDXPostgreSQL.Tests.Fixtures;

/// <summary>
/// Fixture partagée pour les tests d'intégration PostgreSQL.
/// Utilise Testcontainers pour créer une instance PostgreSQL 18 Docker.
/// </summary>
public sealed class PostgreSqlFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container;
    private readonly ILoggerFactory _loggerFactory;

    /// <summary>
    /// Options de connexion configurées pour le conteneur de test.
    /// </summary>
    public PostgreSqlConnectionOptions Options { get; private set; } = null!;

    /// <summary>
    /// Factory de connexions configurée pour les tests.
    /// </summary>
    public IPostgreSqlConnectionFactory Factory { get; private set; } = null!;

    /// <summary>
    /// Factory de loggers pour les tests.
    /// </summary>
    public ILoggerFactory LoggerFactory => _loggerFactory;

    public PostgreSqlFixture()
    {
        _loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        _container = new PostgreSqlBuilder()
            .WithImage("postgres:18")
            .WithDatabase("ndxpostgresql_test")
            .WithUsername("testuser")
            .WithPassword("testpassword")
            .WithCleanUp(true)
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        Options = new PostgreSqlConnectionOptions
        {
            ConnectionString = _container.GetConnectionString(),
            Pooling = true,
            MinPoolSize = 0,
            MaxPoolSize = 10
        };

        Factory = new PostgreSqlConnectionFactory(Options, _loggerFactory);

        // Initialiser la base de données avec les objets de test
        await InitializeTestDatabaseAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
        _loggerFactory.Dispose();
    }

    private async Task InitializeTestDatabaseAsync()
    {
        await using var connection = Factory.CreateConnection();
        await connection.OpenAsync();

        // Créer la table de test
        await connection.ExecuteNonQueryAsync(@"
            CREATE TABLE IF NOT EXISTS test_users (
                id SERIAL PRIMARY KEY,
                name VARCHAR(100) NOT NULL,
                email VARCHAR(255) UNIQUE,
                age INT,
                is_active BOOLEAN DEFAULT true,
                created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
                metadata JSONB
            )
        ");

        // Créer une table pour les tests de transaction
        await connection.ExecuteNonQueryAsync(@"
            CREATE TABLE IF NOT EXISTS test_orders (
                id SERIAL PRIMARY KEY,
                user_id INT REFERENCES test_users(id),
                amount DECIMAL(10, 2) NOT NULL,
                status VARCHAR(50) DEFAULT 'pending',
                created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
            )
        ");

        // Créer une fonction PostgreSQL
        await connection.ExecuteNonQueryAsync(@"
            CREATE OR REPLACE FUNCTION add_numbers(a INT, b INT)
            RETURNS INT AS $$
            BEGIN
                RETURN a + b;
            END;
            $$ LANGUAGE plpgsql
        ");

        // Créer une fonction avec paramètre OUT
        await connection.ExecuteNonQueryAsync(@"
            CREATE OR REPLACE FUNCTION get_user_stats(
                p_user_id INT,
                OUT total_orders INT,
                OUT total_amount DECIMAL
            ) AS $$
            BEGIN
                SELECT COUNT(*), COALESCE(SUM(amount), 0)
                INTO total_orders, total_amount
                FROM test_orders
                WHERE user_id = p_user_id;
            END;
            $$ LANGUAGE plpgsql
        ");

        // Créer une procédure stockée (PostgreSQL 11+)
        await connection.ExecuteNonQueryAsync(@"
            CREATE OR REPLACE PROCEDURE insert_test_user(
                p_name VARCHAR,
                p_email VARCHAR,
                p_age INT
            )
            LANGUAGE plpgsql
            AS $$
            BEGIN
                INSERT INTO test_users (name, email, age)
                VALUES (p_name, p_email, p_age);
            END;
            $$
        ");
    }

    /// <summary>
    /// Nettoie les données de test entre les tests.
    /// </summary>
    public async Task CleanupTestDataAsync()
    {
        await using var connection = Factory.CreateConnection();
        await connection.OpenAsync();

        await connection.ExecuteNonQueryAsync("DELETE FROM test_orders");
        await connection.ExecuteNonQueryAsync("DELETE FROM test_users");
        await connection.ExecuteNonQueryAsync("ALTER SEQUENCE test_users_id_seq RESTART WITH 1");
        await connection.ExecuteNonQueryAsync("ALTER SEQUENCE test_orders_id_seq RESTART WITH 1");
    }
}

/// <summary>
/// Collection de tests pour partager le fixture PostgreSQL.
/// </summary>
[CollectionDefinition("PostgreSQL")]
public class PostgreSqlCollection : ICollectionFixture<PostgreSqlFixture>
{
}

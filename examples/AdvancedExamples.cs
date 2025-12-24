// ============================================================================
// NDXPostgreSQL - Exemples Avancés
// ============================================================================
// Ce fichier contient des exemples avancés: health checks, injection de
// dépendances, CreateCommand, ClearPool, JSONB et fonctionnalités PostgreSQL.
//
// NOTE: Ces exemples sont fournis à titre de documentation.
//       Ils ne sont pas exécutés par les tests unitaires.
//
// Auteur: Nicolas DEOUX <NDXDev@gmail.com>
// ============================================================================

using System.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NDXPostgreSQL;
using NDXPostgreSQL.Extensions;

namespace NDXPostgreSQL.Examples;

/// <summary>
/// Exemples avancés d'utilisation de NDXPostgreSQL.
/// </summary>
public static class AdvancedExamples
{
    // ========================================================================
    // Health Checks
    // ========================================================================

    /// <summary>
    /// Vérification basique de la santé de la connexion.
    /// </summary>
    public static async Task<bool> BasicHealthCheckAsync(IPostgreSqlConnection connection)
    {
        try
        {
            var result = await connection.ExecuteScalarAsync<int>("SELECT 1");
            return result == 1;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Health check complet avec informations détaillées.
    /// </summary>
    public static async Task<HealthCheckResultExtended> DetailedHealthCheckAsync(IPostgreSqlConnection connection)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            // Test de connexion basique
            await connection.ExecuteScalarAsync<int>("SELECT 1");

            // Récupérer les informations du serveur PostgreSQL
            var version = await connection.ExecuteScalarAsync<string>("SELECT version()");
            var uptime = await connection.ExecuteScalarAsync<double>(
                "SELECT EXTRACT(EPOCH FROM (NOW() - pg_postmaster_start_time()))");
            var connections = await connection.ExecuteScalarAsync<int>(
                "SELECT count(*) FROM pg_stat_activity WHERE state IS NOT NULL");
            var maxConnections = await connection.ExecuteScalarAsync<int>("SHOW max_connections");

            var responseTime = DateTime.UtcNow - startTime;

            return new HealthCheckResultExtended
            {
                IsHealthy = true,
                Message = "Connexion PostgreSQL OK",
                ResponseTime = responseTime,
                ServerVersion = version ?? "Unknown",
                UptimeSeconds = (long)uptime,
                ActiveConnections = connections,
                MaxConnections = int.Parse(maxConnections.ToString()!)
            };
        }
        catch (Exception ex)
        {
            return new HealthCheckResultExtended
            {
                IsHealthy = false,
                Message = ex.Message,
                ResponseTime = DateTime.UtcNow - startTime,
                Exception = ex
            };
        }
    }

    /// <summary>
    /// Résultat d'un health check étendu.
    /// </summary>
    public class HealthCheckResultExtended
    {
        public bool IsHealthy { get; set; }
        public string Message { get; set; } = string.Empty;
        public TimeSpan ResponseTime { get; set; }
        public string? ServerVersion { get; set; }
        public long UptimeSeconds { get; set; }
        public int ActiveConnections { get; set; }
        public int MaxConnections { get; set; }
        public Exception? Exception { get; set; }

        public void Print()
        {
            Console.WriteLine("=== Health Check PostgreSQL ===");
            Console.WriteLine($"Status: {(IsHealthy ? "HEALTHY" : "UNHEALTHY")}");
            Console.WriteLine($"Message: {Message}");
            Console.WriteLine($"Response Time: {ResponseTime.TotalMilliseconds:F2}ms");

            if (IsHealthy)
            {
                Console.WriteLine($"Server Version: {ServerVersion}");
                Console.WriteLine($"Uptime: {TimeSpan.FromSeconds(UptimeSeconds):g}");
                Console.WriteLine($"Active Connections: {ActiveConnections}/{MaxConnections}");
            }
            else if (Exception != null)
            {
                Console.WriteLine($"Error: {Exception.GetType().Name}");
            }
        }
    }

    // ========================================================================
    // Injection de dépendances
    // ========================================================================

    /// <summary>
    /// Configuration de l'injection de dépendances avec IServiceCollection.
    /// </summary>
    public static IServiceCollection ConfigureDependencyInjection(IServiceCollection services)
    {
        // Ajouter NDXPostgreSQL avec la méthode d'extension
        services.AddNDXPostgreSQL(options =>
        {
            options.Host = "localhost";
            options.Port = 5432;
            options.Database = "ma_base";
            options.Username = "mon_user";
            options.Password = "mon_pass";
            options.Pooling = true;
            options.MaxPoolSize = 100;
        });

        // Enregistrer les services métier
        services.AddScoped<IClientRepository, ClientRepository>();
        services.AddScoped<IOrderService, OrderService>();

        // Ajouter le logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        return services;
    }

    /// <summary>
    /// Exemple de repository utilisant l'injection de dépendances.
    /// </summary>
    public interface IClientRepository
    {
        Task<DataTable> GetAllAsync();
        Task<DataRow?> GetByIdAsync(int id);
        Task<int> CreateAsync(string nom, string email);
    }

    public class ClientRepository : IClientRepository
    {
        private readonly IPostgreSqlConnectionFactory _connectionFactory;
        private readonly ILogger<ClientRepository> _logger;

        public ClientRepository(
            IPostgreSqlConnectionFactory connectionFactory,
            ILogger<ClientRepository> logger)
        {
            _connectionFactory = connectionFactory;
            _logger = logger;
        }

        public async Task<DataTable> GetAllAsync()
        {
            await using var connection = _connectionFactory.CreateConnection();

            _logger.LogDebug("Récupération de tous les clients");
            return await connection.ExecuteQueryAsync("SELECT * FROM clients WHERE actif = TRUE");
        }

        public async Task<DataRow?> GetByIdAsync(int id)
        {
            await using var connection = _connectionFactory.CreateConnection();

            _logger.LogDebug("Récupération du client {ClientId}", id);
            var result = await connection.ExecuteQueryAsync(
                "SELECT * FROM clients WHERE id = @id",
                new { id });

            return result.Rows.Count > 0 ? result.Rows[0] : null;
        }

        public async Task<int> CreateAsync(string nom, string email)
        {
            await using var connection = _connectionFactory.CreateConnection();

            var newId = await connection.ExecuteScalarAsync<int>(
                "INSERT INTO clients (nom, email, date_inscription) VALUES (@nom, @email, NOW()) RETURNING id",
                new { nom, email });

            _logger.LogInformation("Client créé avec l'ID {ClientId}", newId);
            return newId;
        }
    }

    public interface IOrderService
    {
        Task<int> CreateOrderAsync(int clientId, List<OrderItem> items);
    }

    public class OrderItem
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
    }

    public class OrderService : IOrderService
    {
        private readonly IPostgreSqlConnectionFactory _connectionFactory;
        private readonly ILogger<OrderService> _logger;

        public OrderService(
            IPostgreSqlConnectionFactory connectionFactory,
            ILogger<OrderService> logger)
        {
            _connectionFactory = connectionFactory;
            _logger = logger;
        }

        public async Task<int> CreateOrderAsync(int clientId, List<OrderItem> items)
        {
            await using var connection = _connectionFactory.CreateConnection();
            await connection.BeginTransactionAsync();

            try
            {
                _logger.LogInformation("Création de commande pour le client {ClientId}", clientId);

                var orderId = await connection.ExecuteScalarAsync<int>(
                    "INSERT INTO commandes (client_id, date_commande, statut) VALUES (@clientId, NOW(), 'PENDING') RETURNING id",
                    new { clientId });

                decimal total = 0;

                foreach (var item in items)
                {
                    var price = await connection.ExecuteScalarAsync<decimal>(
                        "SELECT prix FROM produits WHERE id = @id",
                        new { id = item.ProductId });

                    var lineTotal = price * item.Quantity;
                    total += lineTotal;

                    await connection.ExecuteNonQueryAsync(
                        @"INSERT INTO lignes_commande (commande_id, produit_id, quantite, prix_unitaire)
                          VALUES (@orderId, @productId, @quantity, @price)",
                        new { orderId, productId = item.ProductId, quantity = item.Quantity, price });
                }

                await connection.ExecuteNonQueryAsync(
                    "UPDATE commandes SET montant_total = @total WHERE id = @orderId",
                    new { total, orderId });

                await connection.CommitAsync();

                _logger.LogInformation("Commande {OrderId} créée - Total: {Total:C}", orderId, total);
                return orderId;
            }
            catch (Exception ex)
            {
                await connection.RollbackAsync();
                _logger.LogError(ex, "Erreur lors de la création de la commande");
                throw;
            }
        }
    }

    // ========================================================================
    // CreateCommand - Utilisation de commandes natives
    // ========================================================================

    /// <summary>
    /// Exemple d'utilisation de CreateCommand pour des commandes personnalisées.
    /// </summary>
    public static async Task CreateCommandExampleAsync(IPostgreSqlConnection connection)
    {
        await connection.OpenAsync();

        // Créer une commande native
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, nom, email FROM clients WHERE actif = @actif LIMIT @limit";
        command.Parameters.AddWithValue("@actif", true);
        command.Parameters.AddWithValue("@limit", 10);

        // Exécuter et lire les résultats
        await using var reader = await command.ExecuteReaderAsync();

        Console.WriteLine("Clients actifs:");
        while (await reader.ReadAsync())
        {
            Console.WriteLine($"  [{reader.GetInt32(0)}] {reader.GetString(1)} - {reader.GetString(2)}");
        }
    }

    /// <summary>
    /// Exemple de commande préparée pour des exécutions multiples.
    /// </summary>
    public static async Task PreparedStatementExampleAsync(IPostgreSqlConnection connection)
    {
        await connection.OpenAsync();

        // Créer et préparer la commande
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO logs (action, message, created_at) VALUES (@action, @message, @created)";

        var actionParam = command.Parameters.Add("@action", NpgsqlTypes.NpgsqlDbType.Varchar);
        var messageParam = command.Parameters.Add("@message", NpgsqlTypes.NpgsqlDbType.Text);
        var createdParam = command.Parameters.Add("@created", NpgsqlTypes.NpgsqlDbType.TimestampTz);

        await command.PrepareAsync();

        // Exécuter plusieurs fois avec différentes valeurs
        var actions = new[] { "LOGIN", "LOGOUT", "VIEW", "EDIT", "DELETE" };

        foreach (var action in actions)
        {
            actionParam.Value = action;
            messageParam.Value = $"Utilisateur a effectué: {action}";
            createdParam.Value = DateTime.UtcNow;

            await command.ExecuteNonQueryAsync();
        }

        Console.WriteLine($"{actions.Length} logs insérés avec une commande préparée");
    }

    // ========================================================================
    // ClearPool - Gestion du pool de connexions
    // ========================================================================

    /// <summary>
    /// Exemple de nettoyage du pool de connexions.
    /// </summary>
    public static async Task ClearPoolExampleAsync()
    {
        var options = new PostgreSqlConnectionOptions
        {
            Host = "localhost",
            Database = "test",
            Username = "user",
            Password = "pass",
            Pooling = true,
            MinPoolSize = 5,
            MaxPoolSize = 20
        };

        // Créer plusieurs connexions
        var connections = new List<PostgreSqlConnection>();

        for (int i = 0; i < 10; i++)
        {
            var conn = new PostgreSqlConnection(options);
            await conn.OpenAsync();
            connections.Add(conn);
        }

        Console.WriteLine($"Créé {connections.Count} connexions");

        // Fermer et disposer les connexions (retourne au pool)
        foreach (var conn in connections)
        {
            await conn.DisposeAsync();
        }

        Console.WriteLine("Connexions fermées (retournées au pool)");

        // Pour une connexion principale, le pool sera vidé lors du Dispose
        await using var primaryConnection = new PostgreSqlConnection(
            new PostgreSqlConnectionOptions
            {
                ConnectionString = options.BuildConnectionString(),
                IsPrimaryConnection = true // Videra le pool lors du Dispose
            });

        await primaryConnection.OpenAsync();
        Console.WriteLine("Connexion principale ouverte");

        // Le pool sera vidé automatiquement lors du DisposeAsync
    }

    // ========================================================================
    // JSONB - Fonctionnalités PostgreSQL spécifiques
    // ========================================================================

    /// <summary>
    /// Exemples d'utilisation de JSONB.
    /// </summary>
    public static async Task JsonbExamplesAsync(IPostgreSqlConnection connection)
    {
        await connection.OpenAsync();

        // Créer une table avec JSONB
        await connection.ExecuteNonQueryAsync(@"
            CREATE TABLE IF NOT EXISTS utilisateurs_jsonb (
                id SERIAL PRIMARY KEY,
                nom VARCHAR(100) NOT NULL,
                metadata JSONB DEFAULT '{}'
            )");

        // Insérer des données JSONB
        var metadata = """{"role": "admin", "permissions": ["read", "write", "delete"], "settings": {"theme": "dark", "notifications": true}}""";

        var userId = await connection.ExecuteScalarAsync<int>(
            "INSERT INTO utilisateurs_jsonb (nom, metadata) VALUES (@nom, @metadata::jsonb) RETURNING id",
            new { nom = "Admin User", metadata });

        Console.WriteLine($"Utilisateur créé avec ID: {userId}");

        // Requêtes JSONB - Accéder aux propriétés
        var role = await connection.ExecuteScalarAsync<string>(
            "SELECT metadata->>'role' FROM utilisateurs_jsonb WHERE id = @id",
            new { id = userId });
        Console.WriteLine($"Rôle: {role}");

        // Requêtes JSONB - Vérifier l'existence d'une clé
        var hasPermissions = await connection.ExecuteScalarAsync<bool>(
            "SELECT metadata ? 'permissions' FROM utilisateurs_jsonb WHERE id = @id",
            new { id = userId });
        Console.WriteLine($"A des permissions: {hasPermissions}");

        // Requêtes JSONB - Rechercher dans un tableau
        var users = await connection.ExecuteQueryAsync(
            "SELECT nom FROM utilisateurs_jsonb WHERE metadata->'permissions' ? @perm",
            new { perm = "delete" });
        Console.WriteLine($"Utilisateurs avec permission 'delete': {users.Rows.Count}");

        // Mise à jour JSONB - Modifier une propriété
        await connection.ExecuteNonQueryAsync(
            @"UPDATE utilisateurs_jsonb
              SET metadata = jsonb_set(metadata, '{settings,theme}', '""light""')
              WHERE id = @id",
            new { id = userId });
        Console.WriteLine("Thème mis à jour");

        // Mise à jour JSONB - Ajouter un élément au tableau
        await connection.ExecuteNonQueryAsync(
            @"UPDATE utilisateurs_jsonb
              SET metadata = jsonb_set(metadata, '{permissions}', (metadata->'permissions') || '""admin""')
              WHERE id = @id",
            new { id = userId });
        Console.WriteLine("Permission 'admin' ajoutée");

        // Suppression JSONB - Retirer une clé
        await connection.ExecuteNonQueryAsync(
            @"UPDATE utilisateurs_jsonb
              SET metadata = metadata - 'settings'
              WHERE id = @id",
            new { id = userId });
        Console.WriteLine("Settings supprimés");

        // Nettoyage
        await connection.ExecuteNonQueryAsync("DROP TABLE IF EXISTS utilisateurs_jsonb");
    }

    // ========================================================================
    // Statistiques du serveur PostgreSQL
    // ========================================================================

    /// <summary>
    /// Récupération des statistiques du serveur PostgreSQL.
    /// </summary>
    public static async Task<ServerStats> GetServerStatsAsync(IPostgreSqlConnection connection)
    {
        var stats = new ServerStats();

        // Informations de base
        stats.Version = await connection.ExecuteScalarAsync<string>("SELECT version()") ?? "Unknown";
        stats.CurrentDatabase = await connection.ExecuteScalarAsync<string>("SELECT current_database()") ?? "Unknown";
        stats.CurrentUser = await connection.ExecuteScalarAsync<string>("SELECT current_user") ?? "Unknown";

        // Uptime
        var uptime = await connection.ExecuteScalarAsync<double>(
            "SELECT EXTRACT(EPOCH FROM (NOW() - pg_postmaster_start_time()))");
        stats.UptimeSeconds = (long)uptime;

        // Connexions
        stats.ActiveConnections = await connection.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM pg_stat_activity WHERE state = 'active'");
        stats.IdleConnections = await connection.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM pg_stat_activity WHERE state = 'idle'");
        stats.MaxConnections = int.Parse((await connection.ExecuteScalarAsync<string>("SHOW max_connections"))!);

        // Statistiques de la base de données
        var dbStats = await connection.ExecuteQueryAsync(@"
            SELECT
                pg_database_size(current_database()) AS db_size,
                xact_commit,
                xact_rollback,
                blks_read,
                blks_hit,
                tup_returned,
                tup_fetched,
                tup_inserted,
                tup_updated,
                tup_deleted
            FROM pg_stat_database
            WHERE datname = current_database()");

        if (dbStats.Rows.Count > 0)
        {
            var row = dbStats.Rows[0];
            stats.DatabaseSizeBytes = Convert.ToInt64(row["db_size"]);
            stats.TransactionsCommitted = Convert.ToInt64(row["xact_commit"]);
            stats.TransactionsRolledBack = Convert.ToInt64(row["xact_rollback"]);
            stats.BlocksRead = Convert.ToInt64(row["blks_read"]);
            stats.BlocksHit = Convert.ToInt64(row["blks_hit"]);
            stats.RowsReturned = Convert.ToInt64(row["tup_returned"]);
            stats.RowsInserted = Convert.ToInt64(row["tup_inserted"]);
            stats.RowsUpdated = Convert.ToInt64(row["tup_updated"]);
            stats.RowsDeleted = Convert.ToInt64(row["tup_deleted"]);
        }

        // Cache hit ratio
        if (stats.BlocksRead + stats.BlocksHit > 0)
        {
            stats.CacheHitRatio = (double)stats.BlocksHit / (stats.BlocksRead + stats.BlocksHit) * 100;
        }

        return stats;
    }

    /// <summary>
    /// Statistiques du serveur PostgreSQL.
    /// </summary>
    public class ServerStats
    {
        public string Version { get; set; } = string.Empty;
        public string CurrentDatabase { get; set; } = string.Empty;
        public string CurrentUser { get; set; } = string.Empty;
        public long UptimeSeconds { get; set; }
        public int ActiveConnections { get; set; }
        public int IdleConnections { get; set; }
        public int MaxConnections { get; set; }
        public long DatabaseSizeBytes { get; set; }
        public long TransactionsCommitted { get; set; }
        public long TransactionsRolledBack { get; set; }
        public long BlocksRead { get; set; }
        public long BlocksHit { get; set; }
        public double CacheHitRatio { get; set; }
        public long RowsReturned { get; set; }
        public long RowsInserted { get; set; }
        public long RowsUpdated { get; set; }
        public long RowsDeleted { get; set; }

        public void Print()
        {
            Console.WriteLine("=== Statistiques Serveur PostgreSQL ===");
            Console.WriteLine($"Version: {Version}");
            Console.WriteLine($"Base de données: {CurrentDatabase}");
            Console.WriteLine($"Utilisateur: {CurrentUser}");
            Console.WriteLine($"Uptime: {TimeSpan.FromSeconds(UptimeSeconds):g}");
            Console.WriteLine($"Connexions: {ActiveConnections} actives, {IdleConnections} idle (max: {MaxConnections})");
            Console.WriteLine($"Taille base: {DatabaseSizeBytes / 1024.0 / 1024.0:F2} MB");
            Console.WriteLine($"Transactions: {TransactionsCommitted:N0} commit, {TransactionsRolledBack:N0} rollback");
            Console.WriteLine($"Cache hit ratio: {CacheHitRatio:F2}%");
            Console.WriteLine($"Lignes: {RowsInserted:N0} insert, {RowsUpdated:N0} update, {RowsDeleted:N0} delete");
        }
    }

    // ========================================================================
    // Exemple complet
    // ========================================================================

    /// <summary>
    /// Exemple complet d'utilisation avancée.
    /// </summary>
    public static async Task FullExampleAsync()
    {
        // Configuration DI
        var services = new ServiceCollection();
        ConfigureDependencyInjection(services);
        var serviceProvider = services.BuildServiceProvider();

        // Récupérer les services
        var clientRepo = serviceProvider.GetRequiredService<IClientRepository>();
        var factory = serviceProvider.GetRequiredService<IPostgreSqlConnectionFactory>();
        var healthCheck = serviceProvider.GetRequiredService<PostgreSqlHealthCheck>();

        // Health check
        var health = await healthCheck.CheckHealthAsync();
        Console.WriteLine($"Health: {(health.IsHealthy ? "OK" : "FAILED")} ({health.ResponseTime.TotalMilliseconds:F0}ms)");

        if (!health.IsHealthy)
        {
            Console.WriteLine("Serveur non disponible, arrêt");
            return;
        }

        // Statistiques serveur
        await using (var connection = factory.CreateConnection())
        {
            await connection.OpenAsync();

            var stats = await GetServerStatsAsync(connection);
            stats.Print();

            // Exemples JSONB
            await JsonbExamplesAsync(connection);

            // CreateCommand
            await CreateCommandExampleAsync(connection);
        }

        // Opérations métier
        var clients = await clientRepo.GetAllAsync();
        Console.WriteLine($"Clients trouvés: {clients.Rows.Count}");

        Console.WriteLine("Exemple avancé terminé!");
    }
}

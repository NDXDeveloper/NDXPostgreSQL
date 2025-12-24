using System.Diagnostics;

namespace NDXPostgreSQL;

/// <summary>
/// Classe pour vérifier l'état de santé des connexions PostgreSQL.
/// </summary>
public sealed class PostgreSqlHealthCheck
{
    private readonly IPostgreSqlConnectionFactory _connectionFactory;

    /// <summary>
    /// Crée une nouvelle instance du health check.
    /// </summary>
    /// <param name="connectionFactory">Factory de connexions à utiliser.</param>
    /// <exception cref="ArgumentNullException">Si connectionFactory est null.</exception>
    public PostgreSqlHealthCheck(IPostgreSqlConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    /// <summary>
    /// Vérifie l'état de santé de la connexion PostgreSQL.
    /// </summary>
    /// <param name="cancellationToken">Token d'annulation optionnel.</param>
    /// <returns>Le résultat du health check.</returns>
    public async Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            await using var connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            var result = await connection.ExecuteScalarAsync<int>("SELECT 1", cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            stopwatch.Stop();

            if (result == 1)
            {
                return new HealthCheckResult(
                    IsHealthy: true,
                    Message: "La connexion PostgreSQL est opérationnelle.",
                    ResponseTime: stopwatch.Elapsed);
            }

            return new HealthCheckResult(
                IsHealthy: false,
                Message: "La réponse de PostgreSQL est inattendue.",
                ResponseTime: stopwatch.Elapsed);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            return new HealthCheckResult(
                IsHealthy: false,
                Message: $"Échec de la connexion PostgreSQL: {ex.Message}",
                ResponseTime: stopwatch.Elapsed,
                Exception: ex);
        }
    }

    /// <summary>
    /// Récupère les informations du serveur PostgreSQL.
    /// </summary>
    /// <param name="cancellationToken">Token d'annulation optionnel.</param>
    /// <returns>Les informations du serveur.</returns>
    public async Task<ServerInfo> GetServerInfoAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var version = await connection.ExecuteScalarAsync<string>(
            "SELECT version()",
            cancellationToken: cancellationToken).ConfigureAwait(false);

        var currentDatabase = await connection.ExecuteScalarAsync<string>(
            "SELECT current_database()",
            cancellationToken: cancellationToken).ConfigureAwait(false);

        var currentUser = await connection.ExecuteScalarAsync<string>(
            "SELECT current_user",
            cancellationToken: cancellationToken).ConfigureAwait(false);

        var connectionPid = await connection.ExecuteScalarAsync<int>(
            "SELECT pg_backend_pid()",
            cancellationToken: cancellationToken).ConfigureAwait(false);

        var serverVersion = await connection.ExecuteScalarAsync<string>(
            "SHOW server_version",
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return new ServerInfo
        {
            Version = version ?? "Unknown",
            ServerVersion = serverVersion ?? "Unknown",
            CurrentDatabase = currentDatabase ?? "Unknown",
            CurrentUser = currentUser ?? "Unknown",
            ConnectionPid = connectionPid
        };
    }
}

/// <summary>
/// Résultat d'un health check.
/// </summary>
/// <param name="IsHealthy">Indique si la connexion est saine.</param>
/// <param name="Message">Message descriptif du résultat.</param>
/// <param name="ResponseTime">Temps de réponse.</param>
/// <param name="Exception">Exception éventuelle en cas d'erreur.</param>
public sealed record HealthCheckResult(
    bool IsHealthy,
    string Message,
    TimeSpan ResponseTime,
    Exception? Exception = null);

/// <summary>
/// Informations sur le serveur PostgreSQL.
/// </summary>
public sealed record ServerInfo
{
    /// <summary>
    /// Version complète du serveur (résultat de version()).
    /// </summary>
    public required string Version { get; init; }

    /// <summary>
    /// Version courte du serveur PostgreSQL.
    /// </summary>
    public required string ServerVersion { get; init; }

    /// <summary>
    /// Base de données courante.
    /// </summary>
    public required string CurrentDatabase { get; init; }

    /// <summary>
    /// Utilisateur courant.
    /// </summary>
    public required string CurrentUser { get; init; }

    /// <summary>
    /// PID du processus backend PostgreSQL pour cette connexion.
    /// </summary>
    public required int ConnectionPid { get; init; }
}

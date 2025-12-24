using Microsoft.Extensions.Logging;

namespace NDXPostgreSQL;

/// <summary>
/// Factory pour la création de connexions PostgreSQL.
/// Permet de créer des connexions avec des options par défaut ou personnalisées.
/// </summary>
public sealed class PostgreSqlConnectionFactory : IPostgreSqlConnectionFactory
{
    private readonly PostgreSqlConnectionOptions _defaultOptions;
    private readonly ILoggerFactory? _loggerFactory;

    /// <summary>
    /// Crée une nouvelle factory avec les options par défaut spécifiées.
    /// </summary>
    /// <param name="defaultOptions">Options par défaut pour les nouvelles connexions.</param>
    /// <param name="loggerFactory">Factory de logger optionnelle.</param>
    /// <exception cref="ArgumentNullException">Si defaultOptions est null.</exception>
    public PostgreSqlConnectionFactory(PostgreSqlConnectionOptions defaultOptions, ILoggerFactory? loggerFactory = null)
    {
        ArgumentNullException.ThrowIfNull(defaultOptions);

        _defaultOptions = defaultOptions;
        _loggerFactory = loggerFactory;
    }

    /// <summary>
    /// Crée une nouvelle factory avec une chaîne de connexion.
    /// </summary>
    /// <param name="connectionString">Chaîne de connexion PostgreSQL.</param>
    /// <param name="loggerFactory">Factory de logger optionnelle.</param>
    /// <exception cref="ArgumentException">Si connectionString est null ou vide.</exception>
    public PostgreSqlConnectionFactory(string connectionString, ILoggerFactory? loggerFactory = null)
        : this(new PostgreSqlConnectionOptions { ConnectionString = connectionString }, loggerFactory)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("La chaîne de connexion ne peut pas être vide.", nameof(connectionString));
    }

    /// <inheritdoc />
    public IPostgreSqlConnection CreateConnection()
    {
        return CreateConnection(_defaultOptions.Clone());
    }

    /// <inheritdoc />
    public IPostgreSqlConnection CreateConnection(PostgreSqlConnectionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var logger = _loggerFactory?.CreateLogger<PostgreSqlConnection>();
        return new PostgreSqlConnection(options, logger);
    }

    /// <inheritdoc />
    public IPostgreSqlConnection CreatePrimaryConnection()
    {
        var options = _defaultOptions.Clone();
        options.IsPrimaryConnection = true;
        options.DisableAutoClose = true;

        return CreateConnection(options);
    }

    /// <inheritdoc />
    public IPostgreSqlConnection CreateConnection(Action<PostgreSqlConnectionOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var options = _defaultOptions.Clone();
        configure(options);

        return CreateConnection(options);
    }
}

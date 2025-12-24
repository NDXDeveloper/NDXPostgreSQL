using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace NDXPostgreSQL.Extensions;

/// <summary>
/// Extensions pour l'injection de dépendances de NDXPostgreSQL.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Ajoute les services NDXPostgreSQL au conteneur d'injection de dépendances.
    /// </summary>
    /// <param name="services">Collection de services.</param>
    /// <param name="configure">Action pour configurer les options.</param>
    /// <returns>La collection de services pour le chaînage.</returns>
    public static IServiceCollection AddNDXPostgreSQL(
        this IServiceCollection services,
        Action<PostgreSqlConnectionOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new PostgreSqlConnectionOptions();
        configure(options);

        services.TryAddSingleton(options);
        services.TryAddSingleton<IPostgreSqlConnectionFactory, PostgreSqlConnectionFactory>();
        services.TryAddTransient<IPostgreSqlConnection>(sp =>
        {
            var factory = sp.GetRequiredService<IPostgreSqlConnectionFactory>();
            return factory.CreateConnection();
        });
        services.TryAddSingleton<PostgreSqlHealthCheck>();

        return services;
    }

    /// <summary>
    /// Ajoute les services NDXPostgreSQL avec une chaîne de connexion.
    /// </summary>
    /// <param name="services">Collection de services.</param>
    /// <param name="connectionString">Chaîne de connexion PostgreSQL.</param>
    /// <returns>La collection de services pour le chaînage.</returns>
    public static IServiceCollection AddNDXPostgreSQL(
        this IServiceCollection services,
        string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("La chaîne de connexion ne peut pas être vide.", nameof(connectionString));

        return services.AddNDXPostgreSQL(options =>
        {
            options.ConnectionString = connectionString;
        });
    }

    /// <summary>
    /// Ajoute les services NDXPostgreSQL avec configuration détaillée.
    /// </summary>
    /// <param name="services">Collection de services.</param>
    /// <param name="host">Serveur PostgreSQL.</param>
    /// <param name="database">Nom de la base de données.</param>
    /// <param name="username">Nom d'utilisateur.</param>
    /// <param name="password">Mot de passe.</param>
    /// <param name="port">Port (par défaut: 5432).</param>
    /// <returns>La collection de services pour le chaînage.</returns>
    public static IServiceCollection AddNDXPostgreSQL(
        this IServiceCollection services,
        string host,
        string database,
        string username,
        string password,
        int port = 5432)
    {
        return services.AddNDXPostgreSQL(options =>
        {
            options.Host = host;
            options.Port = port;
            options.Database = database;
            options.Username = username;
            options.Password = password;
        });
    }
}

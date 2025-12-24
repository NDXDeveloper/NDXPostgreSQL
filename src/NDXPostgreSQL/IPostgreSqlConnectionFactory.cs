namespace NDXPostgreSQL;

/// <summary>
/// Interface pour la factory de création de connexions PostgreSQL.
/// Permet l'injection de dépendances et la création de connexions configurées.
/// </summary>
public interface IPostgreSqlConnectionFactory
{
    /// <summary>
    /// Crée une nouvelle connexion en utilisant les options par défaut.
    /// </summary>
    /// <returns>Une nouvelle instance de IPostgreSqlConnection.</returns>
    IPostgreSqlConnection CreateConnection();

    /// <summary>
    /// Crée une nouvelle connexion avec des options personnalisées.
    /// </summary>
    /// <param name="options">Options de configuration pour la connexion.</param>
    /// <returns>Une nouvelle instance de IPostgreSqlConnection.</returns>
    IPostgreSqlConnection CreateConnection(PostgreSqlConnectionOptions options);

    /// <summary>
    /// Crée une connexion principale (longue durée de vie, pas de fermeture automatique).
    /// </summary>
    /// <returns>Une nouvelle instance de IPostgreSqlConnection configurée comme connexion principale.</returns>
    IPostgreSqlConnection CreatePrimaryConnection();

    /// <summary>
    /// Crée une nouvelle connexion en permettant de modifier les options par défaut.
    /// </summary>
    /// <param name="configure">Action pour configurer les options.</param>
    /// <returns>Une nouvelle instance de IPostgreSqlConnection.</returns>
    IPostgreSqlConnection CreateConnection(Action<PostgreSqlConnectionOptions> configure);
}

using Npgsql;

namespace NDXPostgreSQL;

/// <summary>
/// Options de configuration pour les connexions PostgreSQL.
/// Permet de construire une chaîne de connexion à partir de paramètres individuels
/// ou d'utiliser une chaîne de connexion existante.
/// </summary>
public sealed class PostgreSqlConnectionOptions
{
    /// <summary>
    /// Serveur PostgreSQL (par défaut: localhost).
    /// </summary>
    public string Host { get; set; } = "localhost";

    /// <summary>
    /// Port du serveur PostgreSQL (par défaut: 5432).
    /// </summary>
    public int Port { get; set; } = 5432;

    /// <summary>
    /// Nom de la base de données.
    /// </summary>
    public string Database { get; set; } = string.Empty;

    /// <summary>
    /// Nom d'utilisateur pour l'authentification.
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Mot de passe pour l'authentification.
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Chaîne de connexion complète. Si définie, elle est utilisée en priorité
    /// sur les paramètres individuels (Host, Port, Database, etc.).
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Indique si cette connexion est une connexion principale (longue durée de vie).
    /// Les connexions principales ne sont pas fermées automatiquement par le timer
    /// et leur pool est vidé lors du Dispose.
    /// </summary>
    public bool IsPrimaryConnection { get; set; }

    /// <summary>
    /// Désactive la fermeture automatique de la connexion.
    /// </summary>
    public bool DisableAutoClose { get; set; }

    /// <summary>
    /// Active ou désactive le pooling de connexions (par défaut: true).
    /// </summary>
    public bool Pooling { get; set; } = true;

    /// <summary>
    /// Taille minimale du pool de connexions (par défaut: 0).
    /// </summary>
    public int MinPoolSize { get; set; } = 0;

    /// <summary>
    /// Taille maximale du pool de connexions (par défaut: 100).
    /// </summary>
    public int MaxPoolSize { get; set; } = 100;

    /// <summary>
    /// Timeout en millisecondes avant la fermeture automatique de la connexion (par défaut: 60000 ms = 1 minute).
    /// </summary>
    public int AutoCloseTimeoutMs { get; set; } = 60_000;

    /// <summary>
    /// Timeout de connexion en secondes (par défaut: 30).
    /// </summary>
    public int ConnectionTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Timeout des commandes en secondes (par défaut: 30).
    /// </summary>
    public int CommandTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Timeout de verrouillage en millisecondes (équivalent PostgreSQL de innodb_lock_wait_timeout).
    /// Par défaut: 120000 ms (2 minutes).
    /// </summary>
    public int LockTimeoutMs { get; set; } = 120_000;

    /// <summary>
    /// Mode SSL pour la connexion.
    /// Valeurs possibles: Disable, Allow, Prefer (défaut), Require, VerifyCA, VerifyFull.
    /// </summary>
    public string SslMode { get; set; } = "Prefer";

    /// <summary>
    /// Active ou désactive SSL (par défaut: false).
    /// </summary>
    public bool UseSsl { get; set; }

    /// <summary>
    /// Nom de l'application visible dans pg_stat_activity.
    /// </summary>
    public string? ApplicationName { get; set; } = "NDXPostgreSQL";

    /// <summary>
    /// Active le mode multiplexage pour les connexions (Npgsql 6.0+).
    /// </summary>
    public bool Multiplexing { get; set; }

    /// <summary>
    /// Construit la chaîne de connexion à partir des options configurées.
    /// </summary>
    /// <returns>La chaîne de connexion PostgreSQL.</returns>
    public string BuildConnectionString()
    {
        // Si une chaîne de connexion complète est fournie, l'utiliser
        if (!string.IsNullOrWhiteSpace(ConnectionString))
        {
            return ConnectionString;
        }

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = Host,
            Port = Port,
            Database = Database,
            Username = Username,
            Password = Password,
            Pooling = Pooling,
            MinPoolSize = MinPoolSize,
            MaxPoolSize = MaxPoolSize,
            Timeout = ConnectionTimeoutSeconds,
            CommandTimeout = CommandTimeoutSeconds,
            Multiplexing = Multiplexing
        };

        if (!string.IsNullOrEmpty(ApplicationName))
        {
            builder.ApplicationName = ApplicationName;
        }

        if (UseSsl)
        {
            builder.SslMode = Enum.Parse<SslMode>(SslMode, ignoreCase: true);
        }

        return builder.ConnectionString;
    }

    /// <summary>
    /// Crée une copie profonde de ces options.
    /// </summary>
    /// <returns>Une nouvelle instance avec les mêmes valeurs.</returns>
    public PostgreSqlConnectionOptions Clone()
    {
        return new PostgreSqlConnectionOptions
        {
            Host = Host,
            Port = Port,
            Database = Database,
            Username = Username,
            Password = Password,
            ConnectionString = ConnectionString,
            IsPrimaryConnection = IsPrimaryConnection,
            DisableAutoClose = DisableAutoClose,
            Pooling = Pooling,
            MinPoolSize = MinPoolSize,
            MaxPoolSize = MaxPoolSize,
            AutoCloseTimeoutMs = AutoCloseTimeoutMs,
            ConnectionTimeoutSeconds = ConnectionTimeoutSeconds,
            CommandTimeoutSeconds = CommandTimeoutSeconds,
            LockTimeoutMs = LockTimeoutMs,
            SslMode = SslMode,
            UseSsl = UseSsl,
            ApplicationName = ApplicationName,
            Multiplexing = Multiplexing
        };
    }
}

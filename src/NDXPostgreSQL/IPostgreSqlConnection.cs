using System.Data;
using Npgsql;

namespace NDXPostgreSQL;

/// <summary>
/// Interface définissant le contrat pour une connexion PostgreSQL gérée.
/// Fournit des méthodes synchrones et asynchrones pour les opérations de base de données.
/// </summary>
public interface IPostgreSqlConnection : IAsyncDisposable, IDisposable
{
    /// <summary>
    /// Identifiant unique de la connexion (incrémenté à chaque nouvelle instance).
    /// </summary>
    int Id { get; }

    /// <summary>
    /// Date et heure UTC de création de la connexion.
    /// </summary>
    DateTime CreatedAt { get; }

    /// <summary>
    /// État actuel de la connexion (Open, Closed, Connecting, etc.).
    /// </summary>
    ConnectionState State { get; }

    /// <summary>
    /// Indique si une transaction est actuellement active sur cette connexion.
    /// </summary>
    bool IsTransactionActive { get; }

    /// <summary>
    /// Indique si cette connexion est une connexion principale (longue durée de vie).
    /// Les connexions principales ne sont pas fermées automatiquement par le timer.
    /// </summary>
    bool IsPrimaryConnection { get; }

    /// <summary>
    /// Accès à la connexion PostgreSQL native sous-jacente.
    /// </summary>
    NpgsqlConnection? Connection { get; }

    /// <summary>
    /// Accès à la transaction PostgreSQL native en cours, si elle existe.
    /// </summary>
    NpgsqlTransaction? Transaction { get; }

    /// <summary>
    /// Dernière action effectuée sur cette connexion (thread-safe).
    /// </summary>
    string LastAction { get; }

    /// <summary>
    /// Historique des 5 dernières actions effectuées sur cette connexion.
    /// </summary>
    IReadOnlyList<string> ActionHistory { get; }

    /// <summary>
    /// Ouvre la connexion de manière synchrone.
    /// </summary>
    void Open();

    /// <summary>
    /// Ouvre la connexion de manière asynchrone.
    /// </summary>
    /// <param name="cancellationToken">Token d'annulation optionnel.</param>
    Task OpenAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Ferme la connexion de manière synchrone.
    /// </summary>
    void Close();

    /// <summary>
    /// Ferme la connexion de manière asynchrone.
    /// </summary>
    Task CloseAsync();

    /// <summary>
    /// Démarre une nouvelle transaction de manière synchrone.
    /// </summary>
    /// <param name="isolationLevel">Niveau d'isolation de la transaction (ReadCommitted par défaut).</param>
    /// <returns>True si la transaction a été démarrée avec succès.</returns>
    bool BeginTransaction(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted);

    /// <summary>
    /// Démarre une nouvelle transaction de manière asynchrone.
    /// </summary>
    /// <param name="isolationLevel">Niveau d'isolation de la transaction (ReadCommitted par défaut).</param>
    /// <param name="cancellationToken">Token d'annulation optionnel.</param>
    /// <returns>True si la transaction a été démarrée avec succès.</returns>
    Task<bool> BeginTransactionAsync(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted, CancellationToken cancellationToken = default);

    /// <summary>
    /// Valide la transaction en cours de manière synchrone.
    /// </summary>
    void Commit();

    /// <summary>
    /// Valide la transaction en cours de manière asynchrone.
    /// </summary>
    /// <param name="cancellationToken">Token d'annulation optionnel.</param>
    Task CommitAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Annule la transaction en cours de manière synchrone.
    /// </summary>
    void Rollback();

    /// <summary>
    /// Annule la transaction en cours de manière asynchrone.
    /// </summary>
    /// <param name="cancellationToken">Token d'annulation optionnel.</param>
    Task RollbackAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Crée un point de sauvegarde dans la transaction en cours.
    /// </summary>
    /// <param name="savepointName">Nom du point de sauvegarde.</param>
    Task SaveAsync(string savepointName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Annule jusqu'au point de sauvegarde spécifié.
    /// </summary>
    /// <param name="savepointName">Nom du point de sauvegarde.</param>
    Task RollbackToSavepointAsync(string savepointName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Libère le point de sauvegarde spécifié.
    /// </summary>
    /// <param name="savepointName">Nom du point de sauvegarde.</param>
    Task ReleaseSavepointAsync(string savepointName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Réinitialise le timer de fermeture automatique.
    /// </summary>
    void ResetAutoCloseTimer();

    /// <summary>
    /// Exécute une commande SQL qui ne retourne pas de résultats (INSERT, UPDATE, DELETE).
    /// </summary>
    /// <param name="query">La requête SQL à exécuter.</param>
    /// <param name="parameters">Objet anonyme contenant les paramètres (ex: new { Id = 1, Name = "Test" }).</param>
    /// <param name="cancellationToken">Token d'annulation optionnel.</param>
    /// <returns>Le nombre de lignes affectées.</returns>
    Task<int> ExecuteNonQueryAsync(string query, object? parameters = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Exécute une requête SQL et retourne la première colonne de la première ligne.
    /// </summary>
    /// <typeparam name="T">Type de la valeur attendue.</typeparam>
    /// <param name="query">La requête SQL à exécuter.</param>
    /// <param name="parameters">Objet anonyme contenant les paramètres.</param>
    /// <param name="cancellationToken">Token d'annulation optionnel.</param>
    /// <returns>La valeur scalaire convertie au type T, ou default si null.</returns>
    Task<T?> ExecuteScalarAsync<T>(string query, object? parameters = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Exécute une requête SQL et retourne les résultats sous forme de DataTable.
    /// </summary>
    /// <param name="query">La requête SQL à exécuter.</param>
    /// <param name="parameters">Objet anonyme contenant les paramètres.</param>
    /// <param name="cancellationToken">Token d'annulation optionnel.</param>
    /// <returns>Un DataTable contenant les résultats.</returns>
    Task<DataTable> ExecuteQueryAsync(string query, object? parameters = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Exécute une requête SQL et retourne un NpgsqlDataReader pour lecture séquentielle.
    /// </summary>
    /// <param name="query">La requête SQL à exécuter.</param>
    /// <param name="parameters">Objet anonyme contenant les paramètres.</param>
    /// <param name="cancellationToken">Token d'annulation optionnel.</param>
    /// <returns>Un NpgsqlDataReader pour parcourir les résultats.</returns>
    Task<NpgsqlDataReader> ExecuteReaderAsync(string query, object? parameters = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Crée une commande PostgreSQL native associée à cette connexion et sa transaction éventuelle.
    /// </summary>
    /// <param name="commandText">Texte de la commande SQL (optionnel).</param>
    /// <returns>Une nouvelle instance de NpgsqlCommand.</returns>
    NpgsqlCommand CreateCommand(string? commandText = null);
}

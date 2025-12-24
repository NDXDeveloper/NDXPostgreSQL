using System.Data;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace NDXPostgreSQL;

/// <summary>
/// Classe de connexion PostgreSQL moderne et performante.
/// Gère automatiquement le cycle de vie des connexions, les transactions,
/// et fournit des méthodes asynchrones pour toutes les opérations.
/// </summary>
public sealed class PostgreSqlConnection : IPostgreSqlConnection
{
    #region Constantes

    private const int DefaultAutoCloseTimeoutMs = 60_000;
    private const int MaxActionHistorySize = 5;

    #endregion

    #region Champs statiques

    private static int _connectionCounter;

    #endregion

    #region Champs d'instance

    private readonly object _actionLock = new();
    private readonly object _disposeLock = new();
    private readonly ILogger<PostgreSqlConnection>? _logger;
    private readonly PostgreSqlConnectionOptions _options;
    private readonly Timer? _autoCloseTimer;
    private readonly List<string> _actionHistory = new(MaxActionHistorySize);

    private NpgsqlConnection? _connection;
    private NpgsqlTransaction? _transaction;
    private string _lastAction = string.Empty;
    private bool _isDisposed;
    private bool _isTransactionActive;

    #endregion

    #region Propriétés

    /// <inheritdoc />
    public int Id { get; }

    /// <inheritdoc />
    public DateTime CreatedAt { get; }

    /// <inheritdoc />
    public ConnectionState State => _connection?.State ?? ConnectionState.Closed;

    /// <inheritdoc />
    public bool IsTransactionActive => _isTransactionActive;

    /// <inheritdoc />
    public bool IsPrimaryConnection => _options.IsPrimaryConnection;

    /// <inheritdoc />
    public NpgsqlConnection? Connection => _connection;

    /// <inheritdoc />
    public NpgsqlTransaction? Transaction => _transaction;

    /// <inheritdoc />
    public string LastAction
    {
        get
        {
            lock (_actionLock)
            {
                return _lastAction;
            }
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<string> ActionHistory
    {
        get
        {
            lock (_actionLock)
            {
                return _actionHistory.ToList().AsReadOnly();
            }
        }
    }

    #endregion

    #region Constructeurs

    /// <summary>
    /// Crée une nouvelle connexion PostgreSQL avec les options spécifiées.
    /// </summary>
    /// <param name="options">Options de configuration de la connexion.</param>
    /// <param name="logger">Logger optionnel pour le suivi des opérations.</param>
    /// <exception cref="ArgumentNullException">Si options est null.</exception>
    public PostgreSqlConnection(PostgreSqlConnectionOptions options, ILogger<PostgreSqlConnection>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options;
        _logger = logger;

        Id = Interlocked.Increment(ref _connectionCounter);
        CreatedAt = DateTime.UtcNow;

        _connection = new NpgsqlConnection(options.BuildConnectionString());
        _connection.StateChange += OnConnectionStateChanged;

        // Configure le timer de fermeture automatique sauf pour les connexions principales
        if (!_options.IsPrimaryConnection && !_options.DisableAutoClose)
        {
            _autoCloseTimer = new Timer(
                OnAutoCloseTimerElapsed,
                null,
                _options.AutoCloseTimeoutMs > 0 ? _options.AutoCloseTimeoutMs : DefaultAutoCloseTimeoutMs,
                Timeout.Infinite);
        }

        LogAction("Constructor", $"Connexion créée (ID: {Id}, Primary: {_options.IsPrimaryConnection})");
    }

    /// <summary>
    /// Crée une nouvelle connexion PostgreSQL à partir d'une chaîne de connexion.
    /// </summary>
    /// <param name="connectionString">Chaîne de connexion PostgreSQL.</param>
    /// <param name="isPrimary">Indique si c'est une connexion principale.</param>
    /// <param name="logger">Logger optionnel.</param>
    public PostgreSqlConnection(string connectionString, bool isPrimary = false, ILogger<PostgreSqlConnection>? logger = null)
        : this(new PostgreSqlConnectionOptions { ConnectionString = connectionString, IsPrimaryConnection = isPrimary }, logger)
    {
    }

    #endregion

    #region Ouverture/Fermeture

    /// <inheritdoc />
    public void Open()
    {
        ThrowIfDisposed();

        if (_connection is null)
            throw new InvalidOperationException("La connexion interne est null.");

        if (_connection.State != ConnectionState.Open)
        {
            _connection.Open();
            SetSessionOptions();
            LogAction("Open", "Connexion ouverte");
        }

        ResetAutoCloseTimer();
    }

    /// <inheritdoc />
    public async Task OpenAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (_connection is null)
            throw new InvalidOperationException("La connexion interne est null.");

        if (_connection.State != ConnectionState.Open)
        {
            await _connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await SetSessionOptionsAsync(cancellationToken).ConfigureAwait(false);
            LogAction("OpenAsync", "Connexion ouverte (async)");
        }

        ResetAutoCloseTimer();
    }

    /// <inheritdoc />
    public void Close()
    {
        if (_connection is not null && _connection.State != ConnectionState.Closed)
        {
            _connection.Close();
            LogAction("Close", "Connexion fermée");
        }
    }

    /// <inheritdoc />
    public async Task CloseAsync()
    {
        if (_connection is not null && _connection.State != ConnectionState.Closed)
        {
            await _connection.CloseAsync().ConfigureAwait(false);
            LogAction("CloseAsync", "Connexion fermée (async)");
        }
    }

    #endregion

    #region Transactions

    /// <inheritdoc />
    public bool BeginTransaction(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted)
    {
        ThrowIfDisposed();

        try
        {
            EnsureConnectionOpen();
            _transaction = _connection!.BeginTransaction(isolationLevel);
            _isTransactionActive = true;
            LogAction("BeginTransaction", $"Transaction démarrée (IsolationLevel: {isolationLevel})");
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Erreur lors du démarrage de la transaction pour la connexion {ConnectionId}", Id);
            _transaction = null;
            _isTransactionActive = false;
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> BeginTransactionAsync(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        try
        {
            await EnsureConnectionOpenAsync(cancellationToken).ConfigureAwait(false);
            _transaction = await _connection!.BeginTransactionAsync(isolationLevel, cancellationToken).ConfigureAwait(false);
            _isTransactionActive = true;
            LogAction("BeginTransactionAsync", $"Transaction démarrée (IsolationLevel: {isolationLevel})");
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Erreur lors du démarrage de la transaction pour la connexion {ConnectionId}", Id);
            _transaction = null;
            _isTransactionActive = false;
            throw;
        }
    }

    /// <inheritdoc />
    public void Commit()
    {
        if (_transaction is null)
            return;

        try
        {
            _transaction.Commit();
            LogAction("Commit", "Transaction validée");
        }
        finally
        {
            _transaction = null;
            _isTransactionActive = false;
        }
    }

    /// <inheritdoc />
    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is null)
            return;

        try
        {
            await _transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            LogAction("CommitAsync", "Transaction validée (async)");
        }
        finally
        {
            _transaction = null;
            _isTransactionActive = false;
        }
    }

    /// <inheritdoc />
    public void Rollback()
    {
        if (_transaction is null)
            return;

        try
        {
            _transaction.Rollback();
            LogAction("Rollback", "Transaction annulée");
        }
        finally
        {
            _transaction = null;
            _isTransactionActive = false;
        }
    }

    /// <inheritdoc />
    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is null)
            return;

        try
        {
            await _transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            LogAction("RollbackAsync", "Transaction annulée (async)");
        }
        finally
        {
            _transaction = null;
            _isTransactionActive = false;
        }
    }

    /// <inheritdoc />
    public async Task SaveAsync(string savepointName, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (_transaction is null)
            throw new InvalidOperationException("Aucune transaction active pour créer un point de sauvegarde.");

        await _transaction.SaveAsync(savepointName, cancellationToken).ConfigureAwait(false);
        LogAction("SaveAsync", $"Point de sauvegarde créé: {savepointName}");
    }

    /// <inheritdoc />
    public async Task RollbackToSavepointAsync(string savepointName, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (_transaction is null)
            throw new InvalidOperationException("Aucune transaction active pour annuler jusqu'au point de sauvegarde.");

        await _transaction.RollbackAsync(savepointName, cancellationToken).ConfigureAwait(false);
        LogAction("RollbackToSavepointAsync", $"Annulation jusqu'au point de sauvegarde: {savepointName}");
    }

    /// <inheritdoc />
    public async Task ReleaseSavepointAsync(string savepointName, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (_transaction is null)
            throw new InvalidOperationException("Aucune transaction active pour libérer le point de sauvegarde.");

        await _transaction.ReleaseAsync(savepointName, cancellationToken).ConfigureAwait(false);
        LogAction("ReleaseSavepointAsync", $"Point de sauvegarde libéré: {savepointName}");
    }

    #endregion

    #region Exécution de requêtes

    /// <inheritdoc />
    public async Task<int> ExecuteNonQueryAsync(string query, object? parameters = null, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await EnsureConnectionOpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = CreateCommandInternal(query, parameters);
        var result = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        ResetAutoCloseTimer();
        LogAction("ExecuteNonQueryAsync", $"Requête exécutée, {result} ligne(s) affectée(s)");
        return result;
    }

    /// <inheritdoc />
    public async Task<T?> ExecuteScalarAsync<T>(string query, object? parameters = null, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await EnsureConnectionOpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = CreateCommandInternal(query, parameters);
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);

        ResetAutoCloseTimer();
        LogAction("ExecuteScalarAsync", "Valeur scalaire récupérée");

        if (result is null || result == DBNull.Value)
            return default;

        // Gestion spéciale pour les types nullables et les conversions
        var targetType = typeof(T);
        var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (underlyingType == typeof(bool) && result is int intValue)
        {
            return (T)(object)(intValue != 0);
        }

        if (underlyingType == typeof(bool) && result is long longValue)
        {
            return (T)(object)(longValue != 0);
        }

        return (T)Convert.ChangeType(result, underlyingType);
    }

    /// <inheritdoc />
    public async Task<DataTable> ExecuteQueryAsync(string query, object? parameters = null, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await EnsureConnectionOpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = CreateCommandInternal(query, parameters);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        var dataTable = new DataTable();
        dataTable.Load(reader);

        ResetAutoCloseTimer();
        LogAction("ExecuteQueryAsync", $"Requête exécutée, {dataTable.Rows.Count} ligne(s) retournée(s)");
        return dataTable;
    }

    /// <inheritdoc />
    public async Task<NpgsqlDataReader> ExecuteReaderAsync(string query, object? parameters = null, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await EnsureConnectionOpenAsync(cancellationToken).ConfigureAwait(false);

        var command = CreateCommandInternal(query, parameters);
        var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        ResetAutoCloseTimer();
        LogAction("ExecuteReaderAsync", "Reader créé");
        return reader;
    }

    /// <inheritdoc />
    public NpgsqlCommand CreateCommand(string? commandText = null)
    {
        ThrowIfDisposed();

        var command = _connection!.CreateCommand();
        command.Transaction = _transaction;

        if (!string.IsNullOrEmpty(commandText))
            command.CommandText = commandText;

        return command;
    }

    #endregion

    #region Timer et fermeture automatique

    /// <inheritdoc />
    public void ResetAutoCloseTimer()
    {
        if (_autoCloseTimer is null || _isTransactionActive)
            return;

        _autoCloseTimer.Change(_options.AutoCloseTimeoutMs, Timeout.Infinite);
        _logger?.LogDebug("Timer de fermeture automatique réinitialisé pour la connexion {ConnectionId}", Id);
    }

    private void OnAutoCloseTimerElapsed(object? state)
    {
        if (_isTransactionActive || _options.DisableAutoClose || _options.IsPrimaryConnection)
            return;

        try
        {
            if (_connection?.State == ConnectionState.Open)
            {
                Close();
                LogAction("AutoClose", "Connexion fermée automatiquement (timeout)");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Erreur lors de la fermeture automatique de la connexion {ConnectionId}", Id);
        }
    }

    #endregion

    #region Dispose

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore().ConfigureAwait(false);
        Dispose(disposing: false);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        lock (_disposeLock)
        {
            if (_isDisposed)
                return;

            if (disposing)
            {
                // Dispose le timer
                _autoCloseTimer?.Dispose();

                // Dispose la transaction si active
                if (_transaction is not null)
                {
                    try
                    {
                        _transaction.Rollback();
                    }
                    catch
                    {
                        // Ignorer les erreurs lors du rollback pendant le dispose
                    }
                    _transaction.Dispose();
                    _transaction = null;
                    _isTransactionActive = false;
                }

                // Dispose la connexion
                if (_connection is not null)
                {
                    if (_connection.State != ConnectionState.Closed)
                        Close();

                    // Vider le pool pour les connexions principales ou non-poolées
                    if (_options.IsPrimaryConnection || !_options.Pooling)
                    {
                        NpgsqlConnection.ClearPool(_connection);
                    }

                    _connection.StateChange -= OnConnectionStateChanged;
                    _connection.Dispose();
                    _connection = null;
                }

                LogAction("Dispose", "Ressources libérées");
            }

            _isDisposed = true;
        }
    }

    private async ValueTask DisposeAsyncCore()
    {
        // Dispose le timer
        if (_autoCloseTimer is not null)
            await _autoCloseTimer.DisposeAsync().ConfigureAwait(false);

        // Dispose la transaction si active
        if (_transaction is not null)
        {
            try
            {
                await _transaction.RollbackAsync().ConfigureAwait(false);
            }
            catch
            {
                // Ignorer les erreurs lors du rollback pendant le dispose
            }
            await _transaction.DisposeAsync().ConfigureAwait(false);
            _transaction = null;
            _isTransactionActive = false;
        }

        // Dispose la connexion
        if (_connection is not null)
        {
            if (_connection.State != ConnectionState.Closed)
                await CloseAsync().ConfigureAwait(false);

            // Vider le pool pour les connexions principales ou non-poolées
            if (_options.IsPrimaryConnection || !_options.Pooling)
            {
                NpgsqlConnection.ClearPool(_connection);
            }

            _connection.StateChange -= OnConnectionStateChanged;
            await _connection.DisposeAsync().ConfigureAwait(false);
            _connection = null;
        }

        LogAction("DisposeAsync", "Ressources libérées (async)");
    }

    #endregion

    #region Méthodes privées

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
    }

    private void EnsureConnectionOpen()
    {
        if (_connection?.State != ConnectionState.Open)
            Open();
    }

    private async Task EnsureConnectionOpenAsync(CancellationToken cancellationToken = default)
    {
        if (_connection?.State != ConnectionState.Open)
            await OpenAsync(cancellationToken).ConfigureAwait(false);
    }

    private void SetSessionOptions()
    {
        if (_connection?.State != ConnectionState.Open)
            return;

        using var command = _connection.CreateCommand();
        command.CommandText = $"SET lock_timeout = '{_options.LockTimeoutMs}ms'";
        command.ExecuteNonQuery();
    }

    private async Task SetSessionOptionsAsync(CancellationToken cancellationToken = default)
    {
        if (_connection?.State != ConnectionState.Open)
            return;

        await using var command = _connection.CreateCommand();
        command.CommandText = $"SET lock_timeout = '{_options.LockTimeoutMs}ms'";
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private NpgsqlCommand CreateCommandInternal(string query, object? parameters)
    {
        var command = CreateCommand(query);
        command.CommandTimeout = _options.CommandTimeoutSeconds;

        if (parameters is not null)
            AddParameters(command, parameters);

        return command;
    }

    private static void AddParameters(NpgsqlCommand command, object parameters)
    {
        var properties = parameters.GetType().GetProperties();

        foreach (var property in properties)
        {
            var value = property.GetValue(parameters);
            var paramName = property.Name.StartsWith('@') ? property.Name : $"@{property.Name}";
            command.Parameters.AddWithValue(paramName, value ?? DBNull.Value);
        }
    }

    private void OnConnectionStateChanged(object sender, StateChangeEventArgs e)
    {
        if (e.CurrentState == ConnectionState.Open)
            ResetAutoCloseTimer();

        _logger?.LogDebug(
            "État de la connexion {ConnectionId} changé: {OldState} -> {NewState}",
            Id, e.OriginalState, e.CurrentState);
    }

    private void LogAction(string action, string description, [CallerMemberName] string? callerName = null)
    {
        lock (_actionLock)
        {
            var logEntry = $"[{DateTime.UtcNow:HH:mm:ss.fff}] [{action}] {description}";

            // Décaler l'historique (garde max 5 entrées)
            if (_actionHistory.Count >= MaxActionHistorySize)
                _actionHistory.RemoveAt(_actionHistory.Count - 1);

            if (!string.IsNullOrEmpty(_lastAction))
                _actionHistory.Insert(0, _lastAction);

            _lastAction = logEntry;
        }

        _logger?.LogDebug("Connexion {ConnectionId} - {Action}: {Description}", Id, action, description);
    }

    #endregion
}

# Documentation du projet source NDXPostgreSQL

## Vue d'ensemble

NDXPostgreSQL est une bibliothèque de connexion PostgreSQL moderne pour .NET 10.

## Architecture

```
NDXPostgreSQL/
├── PostgreSqlConnection.cs           # Classe de connexion principale
├── PostgreSqlConnectionOptions.cs    # Options de configuration
├── PostgreSqlConnectionFactory.cs    # Factory pour créer des connexions
├── IPostgreSqlConnection.cs          # Interface de connexion
├── PostgreSqlHealthCheck.cs          # Vérification de santé
└── Extensions/
    └── ServiceCollectionExtensions.cs  # Extensions DI
```

## Classes principales

### PostgreSqlConnection

La classe principale qui gère les connexions à PostgreSQL.

**Fonctionnalités :**
- Gestion synchrone et asynchrone des connexions
- Support des transactions avec savepoints et niveaux d'isolation
- Timer de fermeture automatique pour les connexions inactives
- Historique des actions pour le débogage
- Pattern IDisposable et IAsyncDisposable
- ClearPool lors du Dispose pour les connexions principales
- Support des fonctions et procédures PostgreSQL
- Support JSONB natif
- Compatible pg_cron pour les tâches planifiées

**Exemple complet :**

```csharp
var options = new PostgreSqlConnectionOptions
{
    Host = "localhost",
    Port = 5432,
    Database = "ma_base",
    Username = "user",
    Password = "pass",
    AutoCloseTimeoutMs = 60000,  // Fermeture auto après 1 min d'inactivité
    LockTimeoutMs = 120000
};

await using var connection = new PostgreSqlConnection(options);

// Ouvrir la connexion
await connection.OpenAsync();

// Exécuter des requêtes
var count = await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM users");

// Transaction avec savepoints
await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted);
try
{
    await connection.ExecuteNonQueryAsync("UPDATE users SET status = 'active' WHERE id = 1");

    // Créer un savepoint
    await connection.CreateSavepointAsync("checkpoint1");

    await connection.ExecuteNonQueryAsync("UPDATE accounts SET balance = 0 WHERE user_id = 1");

    // En cas de problème, rollback jusqu'au savepoint
    // await connection.RollbackToSavepointAsync("checkpoint1");

    await connection.CommitAsync();
}
catch
{
    await connection.RollbackAsync();
    throw;
}
```

### PostgreSqlConnectionOptions

Options de configuration pour personnaliser le comportement de la connexion.

| Propriété | Type | Par défaut | Description |
|-----------|------|------------|-------------|
| `Host` | string | "localhost" | Serveur PostgreSQL |
| `Port` | int | 5432 | Port de connexion |
| `Database` | string | "" | Nom de la base |
| `Username` | string | "" | Utilisateur |
| `Password` | string | "" | Mot de passe |
| `ConnectionString` | string? | null | Chaîne complète (surcharge autres props) |
| `IsPrimaryConnection` | bool | false | Connexion principale (pas de fermeture auto) |
| `AutoCloseTimeoutMs` | int | 60000 | Timeout de fermeture auto (ms) |
| `DisableAutoClose` | bool | false | Désactive la fermeture automatique |
| `Pooling` | bool | true | Active le pooling |
| `MinPoolSize` | int | 0 | Taille min du pool |
| `MaxPoolSize` | int | 100 | Taille max du pool |
| `ConnectionTimeoutSeconds` | int | 30 | Timeout de connexion |
| `CommandTimeoutSeconds` | int | 30 | Timeout des commandes |
| `LockTimeoutMs` | int | 120000 | Timeout verrou PostgreSQL |
| `SslMode` | string | "Prefer" | Mode SSL (Disable, Prefer, Require) |
| `Multiplexing` | bool | false | Active le multiplexing Npgsql |
| `ApplicationName` | string? | null | Nom de l'application |

### PostgreSqlConnectionFactory

Factory pour créer des instances de connexion avec des options centralisées.

```csharp
// Création de la factory
var factory = new PostgreSqlConnectionFactory(defaultOptions, loggerFactory);

// Créer une connexion standard
await using var conn1 = factory.CreateConnection();

// Créer une connexion principale
await using var mainConn = factory.CreatePrimaryConnection();

// Créer avec configuration personnalisée
await using var customConn = factory.CreateConnection(opts =>
{
    opts.CommandTimeoutSeconds = 60;
    opts.DisableAutoClose = true;
});
```

### PostgreSqlHealthCheck

Utilitaire pour vérifier l'état de la base de données.

```csharp
var healthCheck = new PostgreSqlHealthCheck(factory);

// Vérifier la santé
var result = await healthCheck.CheckHealthAsync();
Console.WriteLine($"Healthy: {result.IsHealthy}");
Console.WriteLine($"Message: {result.Message}");
Console.WriteLine($"Response Time: {result.ResponseTime.TotalMilliseconds}ms");

// Obtenir les infos serveur
var info = await healthCheck.GetServerInfoAsync();
Console.WriteLine($"Version: {info.Version}");
Console.WriteLine($"Database: {info.CurrentDatabase}");
Console.WriteLine($"Server Uptime: {info.ServerUptime}");
```

## Fonctions et procédures PostgreSQL

### Fonctions retournant un scalaire

```csharp
// Appel d'une fonction simple
var tva = await connection.ExecuteScalarAsync<decimal>(
    "SELECT calculer_tva(@montant, @taux)",
    new { montant = 100.00m, taux = 0.20m });
```

### Fonctions retournant des enregistrements

```csharp
// Fonction avec RETURNS TABLE
var result = await connection.ExecuteQueryAsync(
    "SELECT * FROM obtenir_stats_client(@id)",
    new { id = 1 });

foreach (DataRow row in result.Rows)
{
    Console.WriteLine($"Total: {row["total_achats"]}");
}
```

### Fonctions avec paramètres OUT

```csharp
// PostgreSQL utilise une syntaxe différente pour OUT
var result = await connection.ExecuteQueryAsync(
    "SELECT * FROM calculer_remise(@montant, @pourcentage)",
    new { montant = 100m, pourcentage = 10m });

if (result.Rows.Count > 0)
{
    var row = result.Rows[0];
    Console.WriteLine($"Remise: {row["montant_remise"]}");
    Console.WriteLine($"Final: {row["montant_final"]}");
}
```

### Procédures stockées (PostgreSQL 11+)

```csharp
// Appel avec CALL
await connection.ExecuteNonQueryAsync(
    "CALL transferer_stock(@source, @destination, @quantite)",
    new { source = 1, destination = 2, quantite = 10 });

// Procédure avec INOUT
var result = await connection.ExecuteQueryAsync(
    "CALL creer_commande(@clientId, @montant, NULL)",
    new { clientId = 1, montant = 299.99m });

if (result.Rows.Count > 0)
{
    var commandeId = Convert.ToInt32(result.Rows[0]["p_commande_id"]);
}
```

## Support JSONB

### Insertion de données JSONB

```csharp
var metadata = """{"role": "admin", "permissions": ["read", "write", "delete"]}""";

await connection.ExecuteNonQueryAsync(
    "INSERT INTO users (name, metadata) VALUES (@name, @metadata::jsonb)",
    new { name = "Admin", metadata });
```

### Requêtes JSONB

```csharp
// Accéder à une propriété
var admins = await connection.ExecuteQueryAsync(
    "SELECT * FROM users WHERE metadata->>'role' = @role",
    new { role = "admin" });

// Vérifier l'existence d'une valeur dans un tableau
var writers = await connection.ExecuteQueryAsync(
    "SELECT * FROM users WHERE metadata->'permissions' ? @perm",
    new { perm = "write" });

// Recherche dans JSONB avec @>
var superAdmins = await connection.ExecuteQueryAsync(
    @"SELECT * FROM users
      WHERE metadata @> '{"role": "admin", "level": "super"}'::jsonb");
```

## Tâches planifiées avec pg_cron

### Vérifier la disponibilité

```csharp
var isAvailable = await connection.ExecuteScalarAsync<bool>(
    "SELECT EXISTS(SELECT 1 FROM pg_extension WHERE extname = 'pg_cron')");
```

### Créer un job

```csharp
// Job quotidien à minuit
var jobId = await connection.ExecuteScalarAsync<long>(@"
    SELECT cron.schedule(
        'cleanup_logs',
        '0 0 * * *',
        $$DELETE FROM logs WHERE created_at < NOW() - INTERVAL '30 days'$$
    )");

Console.WriteLine($"Job créé avec l'ID: {jobId}");
```

### Gérer les jobs

```csharp
// Lister les jobs
var jobs = await connection.ExecuteQueryAsync("SELECT * FROM cron.job");

// Désactiver un job
await connection.ExecuteNonQueryAsync(
    "UPDATE cron.job SET active = false WHERE jobid = @id",
    new { id = jobId });

// Supprimer un job
await connection.ExecuteNonQueryAsync(
    "SELECT cron.unschedule('cleanup_logs')");
```

## Injection de dépendances

Intégration avec Microsoft.Extensions.DependencyInjection :

```csharp
// Dans Program.cs ou Startup.cs
services.AddNDXPostgreSQL(options =>
{
    options.Host = "localhost";
    options.Database = "ma_base";
    options.Username = "user";
    options.Password = "pass";
});

// Utilisation dans un service
public class MonService
{
    private readonly IPostgreSqlConnectionFactory _factory;
    private readonly IPostgreSqlConnection _connection;

    public MonService(
        IPostgreSqlConnectionFactory factory,
        IPostgreSqlConnection connection)
    {
        _factory = factory;
        _connection = connection;
    }
}
```

## Bonnes pratiques

### 1. Toujours utiliser `await using`

```csharp
// Correct
await using var connection = new PostgreSqlConnection(options);

// Éviter
var connection = new PostgreSqlConnection(options);
try { ... }
finally { connection.Dispose(); }
```

### 2. Utiliser RETURNING pour les INSERT

```csharp
// PostgreSQL - Préférer
var id = await connection.ExecuteScalarAsync<int>(
    "INSERT INTO users (name) VALUES (@name) RETURNING id",
    new { name = "Jean" });

// Au lieu de deux requêtes
await connection.ExecuteNonQueryAsync("INSERT INTO users ...");
var id = await connection.ExecuteScalarAsync<int>("SELECT lastval()");
```

### 3. Utiliser les paramètres pour éviter les injections SQL

```csharp
// Correct
await connection.ExecuteNonQueryAsync(
    "SELECT * FROM users WHERE id = @id",
    new { id = userId });

// DANGER - Injection SQL possible
await connection.ExecuteNonQueryAsync(
    $"SELECT * FROM users WHERE id = {userId}");
```

### 4. Utiliser les savepoints pour les transactions complexes

```csharp
await connection.BeginTransactionAsync();
try
{
    await connection.ExecuteNonQueryAsync("...");
    await connection.CreateSavepointAsync("step1");

    try
    {
        await connection.ExecuteNonQueryAsync("...");
    }
    catch
    {
        await connection.RollbackToSavepointAsync("step1");
        // Continuer avec une autre logique
    }

    await connection.CommitAsync();
}
catch
{
    await connection.RollbackAsync();
    throw;
}
```

## Performances

- Le pooling est activé par défaut (Npgsql)
- Les connexions inactives sont fermées automatiquement
- ClearPool est appelé lors du Dispose pour les connexions principales
- Les opérations async évitent de bloquer les threads
- Npgsql est utilisé pour des performances optimales
- Multiplexing disponible pour réduire le nombre de connexions

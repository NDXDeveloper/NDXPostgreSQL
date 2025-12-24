# Exemples NDXPostgreSQL

Ce dossier contient des exemples complets d'utilisation de la bibliothèque NDXPostgreSQL.

> **Note**: Ces exemples sont fournis à titre de documentation. Ils ne sont pas exécutés par les tests unitaires.

## Structure des fichiers

| Fichier | Description |
|---------|-------------|
| `BasicCrudExamples.cs` | Opérations CRUD de base (Create, Read, Update, Delete, JSONB) |
| `StoredProcedureExamples.cs` | Fonctions et procédures PostgreSQL |
| `ScheduledJobsExamples.cs` | Tâches planifiées avec pg_cron |
| `TransactionExamples.cs` | Transactions, savepoints et opérations en masse |
| `AdvancedExamples.cs` | Health checks, DI, LISTEN/NOTIFY, monitoring |

## Exemples CRUD (`BasicCrudExamples.cs`)

### Configuration
```csharp
var options = new PostgreSqlConnectionOptions
{
    Host = "localhost",
    Port = 5432,
    Database = "ma_base",
    Username = "mon_utilisateur",
    Password = "mon_mot_de_passe"
};
```

### Insertion avec RETURNING
```csharp
var newId = await connection.ExecuteScalarAsync<int>(
    "INSERT INTO clients (nom, email) VALUES (@nom, @email) RETURNING id",
    new { nom = "Jean Dupont", email = "jean@example.com" });
```

### Lecture
```csharp
var result = await connection.ExecuteQueryAsync(
    "SELECT * FROM clients WHERE actif = @actif",
    new { actif = true });
```

### Mise à jour
```csharp
var rows = await connection.ExecuteNonQueryAsync(
    "UPDATE clients SET email = @email WHERE id = @id",
    new { email = "nouveau@example.com", id = 1 });
```

### Suppression
```csharp
await connection.ExecuteNonQueryAsync(
    "DELETE FROM clients WHERE id = @id",
    new { id = 1 });
```

### JSONB (spécifique PostgreSQL)
```csharp
// Insertion
var metadata = """{"role": "admin", "permissions": ["read", "write"]}""";
await connection.ExecuteNonQueryAsync(
    "INSERT INTO users (name, metadata) VALUES (@name, @metadata::jsonb)",
    new { name = "Admin", metadata });

// Requête
var admins = await connection.ExecuteQueryAsync(
    "SELECT * FROM users WHERE metadata->>'role' = @role",
    new { role = "admin" });
```

## Fonctions et procédures (`StoredProcedureExamples.cs`)

### Fonction retournant un scalaire
```csharp
var tva = await connection.ExecuteScalarAsync<decimal>(
    "SELECT calculer_tva(@montant, @taux)",
    new { montant = 100.00m, taux = 0.20m });
```

### Fonction avec RETURNS TABLE
```csharp
var result = await connection.ExecuteQueryAsync(
    "SELECT * FROM obtenir_stats_client(@id)",
    new { id = 1 });
```

### Fonction avec paramètres OUT
```csharp
var result = await connection.ExecuteQueryAsync(
    "SELECT * FROM calculer_remise(@montant, @pourcentage)",
    new { montant = 100m, pourcentage = 10m });

var remise = result.Rows[0]["montant_remise"];
var final = result.Rows[0]["montant_final"];
```

### Procédure stockée (CALL)
```csharp
await connection.ExecuteNonQueryAsync(
    "CALL transferer_stock(@source, @destination, @quantite)",
    new { source = 1, destination = 2, quantite = 10 });
```

## Tâches planifiées - pg_cron (`ScheduledJobsExamples.cs`)

### Vérifier la disponibilité
```csharp
var isAvailable = await connection.ExecuteScalarAsync<bool>(
    "SELECT EXISTS(SELECT 1 FROM pg_extension WHERE extname = 'pg_cron')");
```

### Créer un job quotidien
```csharp
var jobId = await connection.ExecuteScalarAsync<long>(@"
    SELECT cron.schedule(
        'cleanup_old_logs',
        '0 0 * * *',
        $$DELETE FROM logs WHERE created_at < NOW() - INTERVAL '30 days'$$
    )");
```

### Créer un job toutes les heures
```csharp
await connection.ExecuteScalarAsync<long>(@"
    SELECT cron.schedule(
        'update_stats_hourly',
        '0 * * * *',
        $$SELECT update_hourly_stats()$$
    )");
```

### Gestion des jobs
```csharp
// Lister les jobs
var jobs = await connection.ExecuteQueryAsync("SELECT * FROM cron.job");

// Désactiver un job
await connection.ExecuteNonQueryAsync(
    "UPDATE cron.job SET active = false WHERE jobid = @id",
    new { id = jobId });

// Supprimer un job
await connection.ExecuteNonQueryAsync("SELECT cron.unschedule('cleanup_old_logs')");
```

### Syntaxe cron
```
* * * * *       - Chaque minute
0 * * * *       - Chaque heure
0 0 * * *       - Chaque jour à minuit
0 0 * * 0       - Chaque dimanche
0 2 * * 1-5     - À 2h, du lundi au vendredi
```

## Transactions (`TransactionExamples.cs`)

### Transaction simple
```csharp
await connection.BeginTransactionAsync();
try
{
    await connection.ExecuteNonQueryAsync("INSERT INTO ...");
    await connection.ExecuteNonQueryAsync("UPDATE ...");
    await connection.CommitAsync();
}
catch
{
    await connection.RollbackAsync();
    throw;
}
```

### Avec savepoints
```csharp
await connection.BeginTransactionAsync();
try
{
    await connection.ExecuteNonQueryAsync("UPDATE accounts SET balance = balance - 100 WHERE id = 1");

    await connection.CreateSavepointAsync("before_transfer");

    await connection.ExecuteNonQueryAsync("UPDATE accounts SET balance = balance + 100 WHERE id = 2");

    // En cas de problème, rollback jusqu'au savepoint
    // await connection.RollbackToSavepointAsync("before_transfer");

    await connection.CommitAsync();
}
catch
{
    await connection.RollbackAsync();
    throw;
}
```

### Avec niveau d'isolation
```csharp
await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead);
```

### Transfert d'argent avec FOR UPDATE
```csharp
await connection.BeginTransactionAsync();
try
{
    // Vérifier le solde avec verrouillage
    var solde = await connection.ExecuteScalarAsync<decimal>(
        "SELECT solde FROM comptes WHERE id = @id FOR UPDATE",
        new { id = fromAccount });

    if (solde < montant)
    {
        await connection.RollbackAsync();
        return false;
    }

    // Débiter
    await connection.ExecuteNonQueryAsync(
        "UPDATE comptes SET solde = solde - @montant WHERE id = @id",
        new { montant, id = fromAccount });

    // Créditer
    await connection.ExecuteNonQueryAsync(
        "UPDATE comptes SET solde = solde + @montant WHERE id = @id",
        new { montant, id = toAccount });

    await connection.CommitAsync();
    return true;
}
catch
{
    await connection.RollbackAsync();
    throw;
}
```

## Fonctionnalités avancées (`AdvancedExamples.cs`)

### Health Check
```csharp
var healthCheck = new PostgreSqlHealthCheck(factory);
var result = await healthCheck.CheckHealthAsync();

Console.WriteLine($"Healthy: {result.IsHealthy}");
Console.WriteLine($"Response Time: {result.ResponseTime.TotalMilliseconds}ms");
Console.WriteLine($"Version: {result.ServerVersion}");
```

### Injection de dépendances
```csharp
// Configuration
services.AddNDXPostgreSQL(options =>
{
    options.Host = "localhost";
    options.Database = "ma_base";
    options.Username = "user";
    options.Password = "pass";
});

// Utilisation
public class ClientRepository
{
    private readonly IPostgreSqlConnectionFactory _factory;

    public ClientRepository(IPostgreSqlConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<DataTable> GetClientsAsync()
    {
        await using var conn = _factory.CreateConnection();
        return await conn.ExecuteQueryAsync("SELECT * FROM clients");
    }
}
```

### LISTEN/NOTIFY (spécifique PostgreSQL)
```csharp
// Écouter les notifications
await connection.ExecuteNonQueryAsync("LISTEN new_orders");

// Envoyer une notification (depuis une autre connexion)
await connection.ExecuteNonQueryAsync(
    "NOTIFY new_orders, 'Order 12345 created'");

// Recevoir les notifications
connection.Notification += (sender, args) =>
{
    Console.WriteLine($"Notification: {args.Payload}");
};
```

### Requêtes parallèles
```csharp
var tasks = new List<Task<DataTable>>();
for (int i = 0; i < 5; i++)
{
    tasks.Add(Task.Run(async () =>
    {
        await using var conn = factory.CreateConnection();
        return await conn.ExecuteQueryAsync("SELECT ...");
    }));
}
var results = await Task.WhenAll(tasks);
```

### Statistiques serveur
```csharp
var stats = await connection.ExecuteQueryAsync(@"
    SELECT
        current_database() AS database,
        version() AS version,
        pg_postmaster_start_time() AS start_time,
        now() - pg_postmaster_start_time() AS uptime,
        (SELECT count(*) FROM pg_stat_activity) AS connections");
```

## Prérequis

Pour utiliser ces exemples :

1. **PostgreSQL 12+** installé et configuré (recommandé: PostgreSQL 18)
2. **pg_cron** installé (pour les exemples de tâches planifiées) :
   ```sql
   CREATE EXTENSION pg_cron;
   ```
3. **Extensions utiles** :
   ```sql
   CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
   CREATE EXTENSION IF NOT EXISTS "pgcrypto";
   ```
4. **Droits suffisants** pour créer des fonctions, procédures et jobs

## Auteur

**Nicolas DEOUX**
- Email: NDXDev@gmail.com
- LinkedIn: [nicolas-deoux-ab295980](https://www.linkedin.com/in/nicolas-deoux-ab295980/)

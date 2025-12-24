# NDXPostgreSQL

**Bibliothèque de connexion PostgreSQL moderne et performante pour .NET 10**

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com/)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-18-336791?style=flat-square&logo=postgresql)](https://www.postgresql.org/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg?style=flat-square)](LICENSE)
[![Platform](https://img.shields.io/badge/Platform-Windows%20%7C%20Linux-blue?style=flat-square)]()
[![Tests](https://img.shields.io/badge/Tests-55%20passed-brightgreen?style=flat-square)]()

---

## Pourquoi NDXPostgreSQL ?

Vous cherchez une bibliothèque de connexion PostgreSQL qui soit **simple**, **moderne** et **performante** ? NDXPostgreSQL est faite pour vous !

- **Async/Await natif** : Toutes les opérations supportent l'asynchrone
- **Cross-platform** : Fonctionne sur Windows et Linux sans modification
- **Gestion automatique** : Fermeture automatique des connexions inactives
- **Transactions simplifiées** : API intuitive avec support des savepoints
- **Fonctions et procédures** : Support complet des fonctions PostgreSQL
- **pg_cron** : Création et gestion des tâches planifiées
- **JSONB** : Support natif des types JSON PostgreSQL
- **Logging intégré** : Compatible avec Microsoft.Extensions.Logging
- **Injection de dépendances** : S'intègre parfaitement avec DI

---

## Installation rapide

```bash
# Cloner le dépôt
git clone https://github.com/NDXDeveloper/NDXPostgreSQL-Connecteur-NET10.git

# Ajouter une référence au projet dans votre solution
dotnet add reference chemin/vers/src/NDXPostgreSQL/NDXPostgreSQL.csproj
```

Ou simplement copier le dossier `src/NDXPostgreSQL` dans votre solution.

---

## Démarrage en 30 secondes

```csharp
using NDXPostgreSQL;

// Création d'une connexion
var options = new PostgreSqlConnectionOptions
{
    Host = "localhost",
    Database = "ma_base",
    Username = "utilisateur",
    Password = "mot_de_passe"
};

await using var connection = new PostgreSqlConnection(options);

// Exécuter une requête
var result = await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM users");
Console.WriteLine($"Nombre d'utilisateurs: {result}");
```

---

## Fonctionnalités principales

### Opérations CRUD asynchrones

```csharp
// INSERT avec RETURNING (récupère l'ID généré)
var newId = await connection.ExecuteScalarAsync<int>(
    "INSERT INTO users (name, email) VALUES (@name, @email) RETURNING id",
    new { name = "Jean", email = "jean@example.com" });

// SELECT avec DataTable
var users = await connection.ExecuteQueryAsync(
    "SELECT * FROM users WHERE active = @active",
    new { active = true });

// SELECT scalaire
var count = await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM users");

// UPDATE
var rows = await connection.ExecuteNonQueryAsync(
    "UPDATE users SET email = @email WHERE id = @id",
    new { email = "nouveau@example.com", id = 1 });

// DELETE
await connection.ExecuteNonQueryAsync(
    "DELETE FROM users WHERE id = @id",
    new { id = 1 });
```

### Gestion des transactions avec savepoints

```csharp
await connection.BeginTransactionAsync();
try
{
    await connection.ExecuteNonQueryAsync("UPDATE accounts SET balance = balance - 100 WHERE id = 1");

    // Créer un savepoint
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

### Fonctions et procédures stockées

```csharp
// Fonction retournant un scalaire
var tva = await connection.ExecuteScalarAsync<decimal>(
    "SELECT calculer_tva(@montant, @taux)",
    new { montant = 100.00m, taux = 0.20m });

// Fonction retournant un enregistrement
var stats = await connection.ExecuteQueryAsync(
    "SELECT * FROM obtenir_stats_client(@id)",
    new { id = 1 });

// Procédure stockée (PostgreSQL 11+)
await connection.ExecuteNonQueryAsync(
    "CALL transferer_stock(@source, @destination, @quantite)",
    new { source = 1, destination = 2, quantite = 10 });
```

### Support JSONB (spécifique PostgreSQL)

```csharp
// INSERT avec JSONB
var metadata = """{"role": "admin", "permissions": ["read", "write"]}""";
await connection.ExecuteNonQueryAsync(
    "INSERT INTO users (name, metadata) VALUES (@name, @metadata::jsonb)",
    new { name = "Admin", metadata });

// Requête JSONB
var admins = await connection.ExecuteQueryAsync(
    "SELECT * FROM users WHERE metadata->>'role' = @role",
    new { role = "admin" });

// Recherche dans un tableau JSONB
var writers = await connection.ExecuteQueryAsync(
    "SELECT * FROM users WHERE metadata->'permissions' ? @perm",
    new { perm = "write" });
```

### pg_cron (tâches planifiées)

```csharp
// Créer un job de nettoyage quotidien
var jobId = await connection.ExecuteScalarAsync<long>(@"
    SELECT cron.schedule(
        'cleanup_old_logs',
        '0 0 * * *',
        $$DELETE FROM logs WHERE created_at < NOW() - INTERVAL '30 days'$$
    )");

// Lister les jobs
var jobs = await connection.ExecuteQueryAsync("SELECT * FROM cron.job");

// Supprimer un job
await connection.ExecuteNonQueryAsync("SELECT cron.unschedule('cleanup_old_logs')");
```

### Factory et injection de dépendances

```csharp
// Configuration avec DI
services.AddNDXPostgreSQL(options =>
{
    options.Host = "localhost";
    options.Database = "ma_base";
    options.Username = "user";
    options.Password = "pass";
});

// Utilisation dans un service
public class UserService
{
    private readonly IPostgreSqlConnectionFactory _factory;

    public UserService(IPostgreSqlConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<DataTable> GetUsersAsync()
    {
        await using var conn = _factory.CreateConnection();
        return await conn.ExecuteQueryAsync("SELECT * FROM users");
    }
}
```

### Health Check intégré

```csharp
var healthCheck = new PostgreSqlHealthCheck(connectionFactory);
var result = await healthCheck.CheckHealthAsync();

if (result.IsHealthy)
{
    Console.WriteLine($"Connexion OK - Temps: {result.ResponseTime.TotalMilliseconds}ms");
    Console.WriteLine($"Version: {result.ServerVersion}");
}
```

---

## Exemples complets

Le dossier `examples/` contient des exemples détaillés :

| Fichier | Description |
|---------|-------------|
| `BasicCrudExamples.cs` | Opérations CRUD complètes |
| `StoredProcedureExamples.cs` | Fonctions et procédures PostgreSQL |
| `ScheduledJobsExamples.cs` | Tâches planifiées avec pg_cron |
| `TransactionExamples.cs` | Transactions et savepoints |
| `AdvancedExamples.cs` | Health checks, DI, JSONB, monitoring |

---

## Configuration Docker pour les tests

Le projet inclut une configuration Docker complète pour PostgreSQL 18 :

```bash
# Démarrer PostgreSQL
cd docker
docker compose up -d

# Vérifier l'état
docker compose ps

# Voir les logs
docker compose logs -f

# Arrêter
docker compose down

# Supprimer tout (données incluses)
docker compose down -v --rmi all
```

**Paramètres de connexion par défaut :**

| Paramètre | Valeur |
|-----------|--------|
| Hôte | localhost |
| Port | 5432 |
| Base | ndxpostgresql_test |
| Utilisateur | testuser |
| Mot de passe | rootpassword |

**Configuration PostgreSQL incluse :**
- Extensions uuid-ossp et pgcrypto activées
- Configuration mémoire optimisée pour les tests
- lock_timeout et statement_timeout configurés
- Tables de test avec JSONB et triggers

---

## Structure du projet

```
NDXPostgreSQL/
├── src/
│   └── NDXPostgreSQL/              # Bibliothèque principale
│       ├── PostgreSqlConnection.cs
│       ├── PostgreSqlConnectionOptions.cs
│       ├── PostgreSqlConnectionFactory.cs
│       ├── PostgreSqlHealthCheck.cs
│       └── Extensions/
├── tests/
│   └── NDXPostgreSQL.Tests/        # 55 tests (unitaires + intégration)
│       ├── Unit/
│       └── Integration/
├── examples/                        # Exemples d'utilisation
│   ├── BasicCrudExamples.cs
│   ├── StoredProcedureExamples.cs
│   ├── ScheduledJobsExamples.cs
│   ├── TransactionExamples.cs
│   └── AdvancedExamples.cs
├── docs/                            # Documentation
└── docker/                          # Configuration Docker
    ├── docker-compose.yml
    ├── config/postgresql.conf
    └── init/
```

---

## Tests

Le projet inclut **55 tests** couvrant :

- **Tests unitaires (17)** : Options, Factory, Configuration
- **Tests d'intégration (38)** :
  - Connexions et cycle de vie
  - Opérations CRUD
  - Transactions (commit, rollback, savepoints, isolation levels)
  - Fonctions et procédures stockées
  - Support JSONB
  - pg_cron (tâches planifiées)
  - Health checks

```bash
# Lancer tous les tests
dotnet test

# Avec verbosité
dotnet test --verbosity normal

# Avec couverture
dotnet test --collect:"XPlat Code Coverage"
```

---

## Documentation

La documentation complète est disponible dans le fichier [SOMMAIRE.md](/SOMMAIRE.md).

---

## Prérequis

- **.NET 10.0** ou supérieur
- **PostgreSQL 12+** (recommandé: 18)
- **Docker** (optionnel, pour les tests)

---

## Auteur

**Nicolas DEOUX**
- Email: [NDXDev@gmail.com](mailto:NDXDev@gmail.com)
- LinkedIn: [nicolas-deoux-ab295980](https://www.linkedin.com/in/nicolas-deoux-ab295980/)

---

## Licence

Ce projet est sous licence MIT. Voir le fichier [LICENSE](/LICENSE) pour plus de détails.

---

<p align="center">
  <b>Fait avec passion en France</b>
</p>

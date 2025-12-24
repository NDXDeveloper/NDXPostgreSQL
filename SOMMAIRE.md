# Sommaire - Documentation NDXPostgreSQL

## Guide de démarrage

- [README.md](README.md) - Présentation et démarrage rapide

## Documentation technique

### Bibliothèque source

- [Documentation du projet source](docs/source/README.md)
  - Architecture de la bibliothèque
  - Classes principales
  - Exemples d'utilisation avancés
  - Migration depuis .NET Framework 4.7.2

### Tests

- [Documentation des tests](docs/tests/README.md)
  - Structure des tests (55 tests)
  - Tests unitaires (17)
  - Tests d'intégration (38)
  - Exécution et couverture

### Docker

- [Documentation Docker](docs/docker/README.md)
  - Installation et configuration
  - Commandes essentielles
  - Configuration personnalisée
  - pgAdmin (interface web)
  - Dépannage

### Exemples

- [Exemples d'utilisation](examples/README.md)
  - [CRUD de base](examples/BasicCrudExamples.cs) - INSERT, SELECT, UPDATE, DELETE, JSONB
  - [Fonctions et procédures](examples/StoredProcedureExamples.cs) - Fonctions PostgreSQL, OUT params
  - [Tâches planifiées](examples/ScheduledJobsExamples.cs) - pg_cron, jobs récurrents
  - [Transactions](examples/TransactionExamples.cs) - Transactions, savepoints, isolation
  - [Avancé](examples/AdvancedExamples.cs) - Health checks, DI, LISTEN/NOTIFY, monitoring

## Références

### Classes principales

| Classe | Description |
|--------|-------------|
| `PostgreSqlConnection` | Connexion principale avec gestion async |
| `PostgreSqlConnectionOptions` | Options de configuration |
| `PostgreSqlConnectionFactory` | Factory pour créer des connexions |
| `PostgreSqlHealthCheck` | Vérification de l'état de la base |

### Interfaces

| Interface | Description |
|-----------|-------------|
| `IPostgreSqlConnection` | Interface de connexion |
| `IPostgreSqlConnectionFactory` | Interface de factory |

### Options de connexion

| Propriété | Type | Défaut | Description |
|-----------|------|--------|-------------|
| `Host` | string | localhost | Serveur PostgreSQL |
| `Port` | int | 5432 | Port de connexion |
| `Database` | string | - | Nom de la base |
| `Username` | string | - | Utilisateur |
| `Password` | string | - | Mot de passe |
| `ConnectionString` | string | null | Chaîne complète (surcharge) |
| `Pooling` | bool | true | Activer le pool |
| `MinPoolSize` | int | 0 | Taille min du pool |
| `MaxPoolSize` | int | 100 | Taille max du pool |
| `ConnectionTimeoutSeconds` | int | 30 | Timeout connexion |
| `CommandTimeoutSeconds` | int | 30 | Timeout commande |
| `LockTimeoutMs` | int | 120000 | Timeout verrous (ms) |
| `SslMode` | string | Prefer | Mode SSL (Disable, Prefer, Require) |
| `Multiplexing` | bool | false | Activer le multiplexing |
| `IsPrimaryConnection` | bool | false | Connexion principale |
| `AutoCloseTimeoutMs` | int | 60000 | Fermeture auto (ms) |
| `DisableAutoClose` | bool | false | Désactiver fermeture auto |

## Fonctionnalités

### CRUD

- INSERT avec RETURNING pour récupérer les IDs
- SELECT vers DataTable ou DataReader
- SELECT scalaire typé
- UPDATE avec conditions
- DELETE avec paramètres
- Support JSONB natif

### Transactions

- BeginTransaction / BeginTransactionAsync
- Commit / CommitAsync
- Rollback / RollbackAsync
- Savepoints (CreateSavepoint, RollbackToSavepoint, ReleaseSavepoint)
- Niveaux d'isolation (ReadUncommitted, ReadCommitted, RepeatableRead, Serializable)

### Fonctions et procédures

- Fonctions retournant un scalaire
- Fonctions retournant des enregistrements (RETURNS TABLE)
- Fonctions avec paramètres OUT
- Fonctions retournant SETOF
- Procédures stockées (CALL, PostgreSQL 11+)
- Procédures avec paramètres INOUT

### Tâches planifiées (pg_cron)

- Jobs ponctuels et récurrents
- Syntaxe cron standard
- Gestion (activer/désactiver/supprimer)
- Historique d'exécution
- Notifications d'échec

### Fonctionnalités PostgreSQL spécifiques

- JSONB (insertion, requêtes, index GIN)
- Arrays (TEXT[], INT[], etc.)
- UUID natif
- LISTEN/NOTIFY
- Types composites
- Fonctions de fenêtrage (window functions)

### Autres

- Fermeture automatique des connexions inactives
- Historique des actions (5 dernières)
- Health checks intégrés
- Logging avec ILogger
- Injection de dépendances
- ClearPool pour libérer le pool de connexions

## Tests couverts

### Tests unitaires (17)

- PostgreSqlConnectionOptions
  - Valeurs par défaut
  - Construction de chaîne de connexion
  - Modes SSL
  - Clone des options
- PostgreSqlConnectionFactory
  - Création de connexions
  - IDs uniques
  - Connexion principale

### Tests d'intégration (38)

- Connexions
  - Open/Close async
  - Dispose async avec ClearPool
  - États et historique
- CRUD
  - INSERT simple et avec RETURNING
  - SELECT (scalaire, query, reader)
  - UPDATE avec paramètres
  - DELETE conditionnel
  - Opérations en masse
  - JSONB (insertion et requêtes)
- Transactions
  - Commit
  - Rollback
  - Savepoints
  - Isolation levels
- Fonctions et procédures
  - Fonctions scalaires
  - Fonctions avec RETURNS TABLE
  - Fonctions avec OUT
  - Procédures stockées (CALL)
- pg_cron
  - Vérification disponibilité
  - Création de jobs
  - Suppression
  - Historique d'exécution
- LISTEN/NOTIFY
  - Notifications synchrones
  - Notifications avec payload

### Health Checks (3)

- Connexion saine
- Connexion défaillante
- Informations serveur (version, uptime)

## Historique

### Version 1.0.0 (Décembre 2025)

- Version initiale pour PostgreSQL
- Support complet async/await
- Compatibilité Windows et Linux
- 55 tests unitaires et d'intégration
- Configuration Docker PostgreSQL 18
- Support pg_cron pour les tâches planifiées
- Support JSONB natif
- Savepoints pour les transactions
- LISTEN/NOTIFY pour les notifications
- Exemples complets documentés

## Ressources externes

- [Documentation PostgreSQL](https://www.postgresql.org/docs/18/)
- [pg_cron Extension](https://github.com/citusdata/pg_cron)
- [Npgsql Documentation](https://www.npgsql.org/doc/)
- [.NET 10 Documentation](https://docs.microsoft.com/dotnet/)

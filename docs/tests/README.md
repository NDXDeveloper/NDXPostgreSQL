# Documentation des tests NDXPostgreSQL

## Vue d'ensemble

Le projet contient **55 tests** répartis entre tests unitaires et tests d'intégration.

| Catégorie | Nombre | Description |
|-----------|--------|-------------|
| Tests unitaires | 17 | Options, Factory, Configuration |
| Tests d'intégration | 38 | Connexions, CRUD, Transactions, Fonctions, pg_cron |

## Structure des tests

```
tests/NDXPostgreSQL.Tests/
├── Unit/                          # Tests unitaires (17)
│   ├── PostgreSqlConnectionOptionsTests.cs
│   └── PostgreSqlConnectionFactoryTests.cs
├── Integration/                   # Tests d'intégration (38)
│   ├── PostgreSqlConnectionTests.cs
│   └── PostgreSqlHealthCheckTests.cs
├── Fixtures/                      # Fixtures partagées
│   └── PostgreSqlFixture.cs
└── appsettings.test.json         # Configuration de test
```

## Prérequis

- .NET 10 SDK
- Docker (pour les tests d'intégration)

## Frameworks de test utilisés

| Framework | Version | Usage |
|-----------|---------|-------|
| xUnit | 2.9.2 | Framework de test principal |
| FluentAssertions | 6.12.2 | Assertions lisibles |
| Moq | 4.20.72 | Mocking |
| Testcontainers.PostgreSql | 4.1.0 | Conteneur PostgreSQL automatique |
| coverlet | 6.0.2 | Couverture de code |

## Exécuter les tests

### Tests unitaires uniquement

```bash
dotnet test --filter "FullyQualifiedName~Unit"
```

### Tests d'intégration uniquement

```bash
dotnet test --filter "FullyQualifiedName~Integration"
```

### Tous les tests

```bash
dotnet test
```

### Avec verbosité

```bash
dotnet test --verbosity normal
```

### Avec couverture de code

```bash
dotnet test --collect:"XPlat Code Coverage"

# Générer un rapport HTML (nécessite ReportGenerator)
reportgenerator \
  -reports:"**/coverage.cobertura.xml" \
  -targetdir:"coverage-report" \
  -reporttypes:Html
```

## Tests unitaires (17)

### PostgreSqlConnectionOptionsTests (9 tests)

| Test | Description |
|------|-------------|
| `DefaultValues_ShouldBeCorrect` | Valeurs par défaut |
| `BuildConnectionString_WithConnectionString_*` | Construction avec chaîne |
| `BuildConnectionString_WithProperties_*` | Construction avec propriétés |
| `BuildConnectionString_WithSslMode_*` | Modes SSL (Disable, Prefer, Require) |
| `Clone_ShouldCreateIndependentCopy` | Clonage des options |

### PostgreSqlConnectionFactoryTests (8 tests)

| Test | Description |
|------|-------------|
| `Constructor_WithNullOptions_ShouldThrow` | Validation des paramètres |
| `Constructor_WithConnectionString_ShouldWork` | Création avec chaîne |
| `CreateConnection_ShouldReturnNewConnection` | Création standard |
| `CreateConnection_ShouldGenerateUniqueIds` | IDs uniques |
| `CreateConnection_WithOptions_*` | Configuration personnalisée |
| `CreatePrimaryConnection_*` | Connexion principale |

## Tests d'intégration (38)

### PostgreSqlConnectionTests (35 tests)

#### Connexions (5 tests)

| Test | Description |
|------|-------------|
| `OpenAsync_ShouldOpenConnection` | Ouverture async |
| `CloseAsync_ShouldCloseConnection` | Fermeture async |
| `DisposeAsync_ShouldCloseAndClearPool` | Dispose avec ClearPool |
| `Connection_ShouldHaveUniqueId` | ID unique |
| `CreatedAt_ShouldBeSetOnConstruction` | Date de création |

#### CRUD (10 tests)

| Test | Description |
|------|-------------|
| `ExecuteScalarAsync_ShouldReturnValue` | SELECT scalaire |
| `ExecuteQueryAsync_ShouldReturnDataTable` | SELECT avec DataTable |
| `ExecuteReaderAsync_ShouldReturnReader` | SELECT avec Reader |
| `ExecuteNonQueryAsync_CreateTable_*` | CREATE TABLE |
| `ExecuteNonQueryAsync_InsertWithReturning_*` | INSERT avec RETURNING |
| `ExecuteNonQueryAsync_InsertWithParameters_*` | INSERT paramétré |
| `ExecuteNonQueryAsync_BulkInsert_*` | INSERT multiple |
| `ExecuteNonQueryAsync_UpdateData_*` | UPDATE |
| `ExecuteNonQueryAsync_DeleteData_*` | DELETE |
| `ActionHistory_ShouldTrackActions` | Historique |

#### JSONB (3 tests)

| Test | Description |
|------|-------------|
| `Jsonb_InsertAndQuery_*` | Insertion et requête JSONB |
| `Jsonb_QueryWithOperators_*` | Opérateurs JSONB (->>, @>) |
| `Jsonb_ArrayContains_*` | Recherche dans tableaux JSONB |

#### Transactions (6 tests)

| Test | Description |
|------|-------------|
| `BeginTransactionAsync_ShouldSetIsTransactionActive` | Démarrage transaction |
| `Transaction_CommitAsync_ShouldPersistChanges` | Commit |
| `Transaction_RollbackAsync_ShouldRevertChanges` | Rollback |
| `Transaction_CreateSavepoint_*` | Création savepoint |
| `Transaction_RollbackToSavepoint_*` | Rollback vers savepoint |
| `Transaction_IsolationLevels_*` | Niveaux d'isolation |

#### Fonctions et procédures (6 tests)

| Test | Description |
|------|-------------|
| `ExecuteFunction_ScalarReturn_*` | Fonction retournant scalaire |
| `ExecuteFunction_TableReturn_*` | Fonction avec RETURNS TABLE |
| `ExecuteFunction_OutParameters_*` | Fonction avec OUT |
| `ExecuteFunction_SetOfReturn_*` | Fonction avec SETOF |
| `ExecuteProcedure_Call_*` | Procédure avec CALL |
| `ExecuteProcedure_InOut_*` | Procédure avec INOUT |

#### pg_cron (5 tests)

| Test | Description |
|------|-------------|
| `PgCron_CheckAvailability_*` | Vérification disponibilité |
| `PgCron_CreateJob_*` | Création de job |
| `PgCron_DeleteJob_*` | Suppression de job |
| `PgCron_ListJobs_*` | Liste des jobs |
| `PgCron_JobHistory_*` | Historique d'exécution |

### PostgreSqlHealthCheckTests (3 tests)

| Test | Description |
|------|-------------|
| `CheckHealthAsync_WhenHealthy_*` | État sain |
| `CheckHealthAsync_WhenUnhealthy_*` | État non sain |
| `GetServerInfoAsync_*` | Informations serveur |

## Fixture Testcontainers

La fixture `PostgreSqlFixture` utilise Testcontainers pour créer automatiquement un conteneur PostgreSQL 18 :

```csharp
public sealed class PostgreSqlFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container;

    public PostgreSqlFixture()
    {
        _container = new PostgreSqlBuilder()
            .WithImage("postgres:18")
            .WithDatabase("ndxpostgresql_test")
            .WithUsername("testuser")
            .WithPassword("testpassword")
            .Build();
    }

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await SetupTestDatabase();
    }

    private async Task SetupTestDatabase()
    {
        // Crée les tables, fonctions et procédures de test
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}
```

### Utilisation dans les tests

```csharp
[Collection("PostgreSQL")]
public class MonTest
{
    private readonly PostgreSqlFixture _fixture;

    public MonTest(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task MonTestAsync()
    {
        await using var connection = _fixture.CreateConnection();
        // ...
    }
}
```

## Configuration de test

Le fichier `appsettings.test.json` contient la configuration par défaut pour les tests manuels (sans Testcontainers) :

```json
{
  "PostgreSQL": {
    "Host": "localhost",
    "Port": 5432,
    "Database": "ndxpostgresql_test",
    "Username": "testuser",
    "Password": "rootpassword"
  }
}
```

## Meilleures pratiques

### 1. Isolation des tests

Chaque test crée et supprime ses propres données :

```csharp
var tableName = $"test_{Guid.NewGuid():N}";
try
{
    await connection.ExecuteNonQueryAsync($"CREATE TABLE {tableName} ...");
    // Tests...
}
finally
{
    await connection.ExecuteNonQueryAsync($"DROP TABLE IF EXISTS {tableName}");
}
```

### 2. Tests parallèles

Les tests d'intégration utilisent des noms de table/fonction uniques pour permettre l'exécution parallèle.

### 3. Assertions lisibles

Utilisation de FluentAssertions pour des tests lisibles :

```csharp
result.Should().NotBeNull();
result.IsHealthy.Should().BeTrue();
result.ResponseTime.Should().BeGreaterThan(TimeSpan.Zero);
```

### 4. Nettoyage dans finally

Toujours nettoyer les ressources créées :

```csharp
try
{
    await connection.ExecuteNonQueryAsync($"CREATE FUNCTION {funcName}() ...");
    // Tests...
}
finally
{
    await connection.ExecuteNonQueryAsync($"DROP FUNCTION IF EXISTS {funcName}");
    await connection.ExecuteNonQueryAsync($"DROP TABLE IF EXISTS {tableName}");
}
```

## CI/CD

Exemple de configuration GitHub Actions :

```yaml
name: Tests

on: [push, pull_request]

jobs:
  test:
    runs-on: ubuntu-latest

    services:
      postgres:
        image: postgres:18
        env:
          POSTGRES_PASSWORD: rootpassword
          POSTGRES_DB: ndxpostgresql_test
          POSTGRES_USER: testuser
        ports:
          - 5432:5432
        options: >-
          --health-cmd="pg_isready -U testuser -d ndxpostgresql_test"
          --health-interval=10s
          --health-timeout=5s
          --health-retries=5

    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      - run: dotnet restore
      - run: dotnet build --no-restore
      - run: dotnet test --no-build --verbosity normal
```

## Couverture de code

Les tests couvrent :

- **Connexions** : Ouverture, fermeture, dispose avec ClearPool, états
- **CRUD** : INSERT avec RETURNING, SELECT, UPDATE, DELETE
- **JSONB** : Insertion, requêtes, opérateurs
- **Transactions** : Begin, Commit, Rollback, Savepoints, Isolation levels
- **Fonctions** : Scalaires, TABLE, OUT, SETOF
- **Procédures** : CALL, INOUT
- **pg_cron** : Création, suppression, historique
- **Health Checks** : État sain, état non sain, informations serveur
- **Factory** : Création, configuration, IDs uniques
- **Options** : Valeurs par défaut, construction chaîne connexion, SSL, Clone

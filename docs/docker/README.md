# Documentation Docker - NDXPostgreSQL

## Vue d'ensemble

Ce guide explique comment utiliser la configuration Docker fournie pour exécuter PostgreSQL 18 dans un environnement de développement et de test.

## Prérequis

- Docker 20.10+ ou Docker Desktop
- Docker Compose v2+
- 2 Go de RAM disponible minimum

Vérifier l'installation :
```bash
docker --version
docker compose version
```

## Structure des fichiers Docker

```
docker/
├── Dockerfile              # Image personnalisée (optionnel)
├── docker-compose.yml      # Configuration principale
├── config/
│   └── postgresql.conf     # Configuration PostgreSQL
└── init/
    └── 01-init-database.sql  # Script d'initialisation
```

## Démarrage rapide

### 1. Démarrer PostgreSQL

```bash
cd docker
docker compose up -d
```

### 2. Vérifier l'état

```bash
docker compose ps
```

Sortie attendue :
```
NAME                  STATUS          PORTS
ndxpostgresql-test    Up (healthy)    0.0.0.0:5432->5432/tcp
```

### 3. Se connecter à PostgreSQL

```bash
# Via psql dans le conteneur
docker compose exec postgres psql -U testuser -d ndxpostgresql_test

# Ou avec un client externe
psql -h localhost -p 5432 -U testuser -d ndxpostgresql_test
# Mot de passe: rootpassword
```

## Commandes essentielles

| Action | Commande |
|--------|----------|
| Démarrer | `docker compose up -d` |
| Arrêter | `docker compose down` |
| Redémarrer | `docker compose restart` |
| Voir les logs | `docker compose logs -f` |
| État | `docker compose ps` |
| Shell bash | `docker compose exec postgres bash` |
| Client psql | `docker compose exec postgres psql -U testuser -d ndxpostgresql_test` |

## Arrêt et nettoyage

### Arrêter le conteneur (conserver les données)

```bash
docker compose down
```

### Arrêter et supprimer les données

```bash
docker compose down -v
```

### Supprimer tout (conteneur, volumes, images)

```bash
docker compose down -v --rmi all
```

### Nettoyage complet

```bash
# Arrêter et supprimer
docker compose down -v --rmi all

# Supprimer le volume nommé si encore présent
docker volume rm ndxpostgresql-data 2>/dev/null || true

# Supprimer le réseau si encore présent
docker network rm ndxpostgresql-net 2>/dev/null || true

# Vérifier qu'il ne reste rien
docker ps -a | grep ndxpostgresql
docker volume ls | grep ndxpostgresql
docker network ls | grep ndxpostgresql
```

## Configuration

### Paramètres de connexion par défaut

| Paramètre | Valeur |
|-----------|--------|
| Hôte | localhost |
| Port | 5432 |
| Base de données | ndxpostgresql_test |
| Utilisateur | testuser |
| Mot de passe | rootpassword |

### Variables d'environnement

Modifiables dans `docker-compose.yml` :

```yaml
environment:
  POSTGRES_PASSWORD: rootpassword
  POSTGRES_DB: ndxpostgresql_test
  POSTGRES_USER: testuser
```

### Configuration PostgreSQL personnalisée

Le fichier `config/postgresql.conf` contient les paramètres optimisés :

```ini
# Mémoire
shared_buffers = 256MB
work_mem = 16MB
effective_cache_size = 512MB

# Connexions
max_connections = 100

# Timeouts
lock_timeout = 120000
statement_timeout = 300000

# Performance (tests uniquement!)
fsync = off
synchronous_commit = off
```

> **Note** : Les paramètres `fsync = off` et `synchronous_commit = off` accélèrent les tests mais ne sont PAS recommandés en production !

## Script d'initialisation

Le fichier `init/01-init-database.sql` est exécuté au premier démarrage :

- Active les extensions `uuid-ossp` et `pgcrypto`
- Crée les tables de test (`test_table`, `transaction_test`, `jsonb_test`, `large_test_table`)
- Insère des données initiales
- Crée des fonctions et procédures de test
- Crée des vues de test
- Configure des triggers de mise à jour automatique

Pour réinitialiser la base :
```bash
docker compose down -v
docker compose up -d
```

## pgAdmin (Interface Web)

Pour activer pgAdmin, une interface web pour gérer PostgreSQL :

```bash
docker compose --profile admin up -d
```

Accès : http://localhost:5050
- Email : `admin@ndxpostgresql.local`
- Mot de passe : `admin_password`

Pour configurer la connexion dans pgAdmin :
1. Cliquez sur "Add New Server"
2. Onglet "General" : Nom = `NDXPostgreSQL`
3. Onglet "Connection" :
   - Host : `postgres` (nom du service Docker)
   - Port : `5432`
   - Username : `testuser`
   - Password : `rootpassword`

## Dépannage

### Le conteneur ne démarre pas

```bash
# Vérifier les logs
docker compose logs postgres

# Causes fréquentes:
# - Port 5432 déjà utilisé
# - Permissions insuffisantes sur les volumes
```

### Port 5432 déjà utilisé

```bash
# Trouver le processus (Linux/Mac)
sudo lsof -i :5432

# Windows
netstat -ano | findstr :5432

# Ou changer le port dans docker-compose.yml
ports:
  - "5433:5432"  # Utiliser le port 5433
```

### Connexion refusée

```bash
# Vérifier que le conteneur est healthy
docker compose ps

# Attendre que PostgreSQL soit prêt (peut prendre 30s)
docker compose logs -f postgres | grep "ready to accept connections"
```

### Réinitialiser les données

```bash
docker compose down -v
docker compose up -d
```

### Problèmes de permissions (Linux)

```bash
# Donner les permissions au dossier data
sudo chown -R 999:999 ./data
```

## Utilisation avec les tests

### Avec Testcontainers (recommandé)

Les tests utilisent Testcontainers pour créer automatiquement un conteneur PostgreSQL. Aucune configuration Docker manuelle nécessaire.

```bash
dotnet test
```

### Tests manuels avec Docker

```bash
# 1. Démarrer PostgreSQL
cd docker && docker compose up -d && cd ..

# 2. Lancer les tests
dotnet test

# 3. Arrêter PostgreSQL
cd docker && docker compose down && cd ..
```

## Chaîne de connexion .NET

```csharp
var connectionString = "Host=localhost;Port=5432;Database=ndxpostgresql_test;Username=testuser;Password=rootpassword";
```

Ou avec les options :

```csharp
var options = new PostgreSqlConnectionOptions
{
    Host = "localhost",
    Port = 5432,
    Database = "ndxpostgresql_test",
    Username = "testuser",
    Password = "rootpassword"
};
```

## Construction de l'image personnalisée

Si vous avez besoin de construire l'image Docker localement :

```bash
cd docker
docker build -t ndxpostgresql:18 .
docker run -d -p 5432:5432 --name ndxpostgresql ndxpostgresql:18
```

## Sauvegarde et restauration

### Sauvegarde

```bash
# Sauvegarde SQL
docker exec ndxpostgresql-test pg_dump -U testuser ndxpostgresql_test > backup.sql

# Sauvegarde binaire (format custom)
docker exec ndxpostgresql-test pg_dump -U testuser -Fc ndxpostgresql_test > backup.dump
```

### Restauration

```bash
# Depuis SQL
docker exec -i ndxpostgresql-test psql -U testuser ndxpostgresql_test < backup.sql

# Depuis format custom
docker exec -i ndxpostgresql-test pg_restore -U testuser -d ndxpostgresql_test < backup.dump
```

## Ressources

- [Documentation PostgreSQL 18](https://www.postgresql.org/docs/18/)
- [Image Docker PostgreSQL](https://hub.docker.com/_/postgres)
- [Docker Compose Documentation](https://docs.docker.com/compose/)
- [pgAdmin Documentation](https://www.pgadmin.org/docs/)
- [Npgsql Documentation](https://www.npgsql.org/doc/)

# ============================================================================
# NDXPostgreSQL - Dockerfile pour PostgreSQL 18
# ============================================================================
# FICHIER EXEMPLE - Non utilisé par défaut.
# Le docker-compose.yml utilise directement l'image officielle postgres:18.
# Ce Dockerfile est fourni comme exemple pour créer une image personnalisée
# avec une configuration PostgreSQL spécifique.
# ============================================================================
#
# Ce Dockerfile crée une image PostgreSQL personnalisée pour les tests
# de développement de la bibliothèque NDXPostgreSQL.
#
# Auteur: Nicolas DEOUX <NDXDev@gmail.com>
# Version: 1.0.0
#
# Construction:
#   docker build -t ndxpostgresql:18 .
#
# Exécution:
#   docker run -d -p 5432:5432 --name ndxpostgresql ndxpostgresql:18
# ============================================================================

FROM postgres:18

# Métadonnées de l'image
LABEL maintainer="Nicolas DEOUX <NDXDev@gmail.com>"
LABEL description="PostgreSQL 18 pour tests NDXPostgreSQL"
LABEL version="1.0.0"

# Variables d'environnement par défaut
ENV POSTGRES_PASSWORD=rootpassword
ENV POSTGRES_DB=ndxpostgresql_test
ENV POSTGRES_USER=testuser
ENV TZ=Europe/Paris
ENV PGTZ=Europe/Paris

# Copier les scripts d'initialisation
COPY init/ /docker-entrypoint-initdb.d/

# Copier la configuration personnalisée
COPY config/postgresql.conf /etc/postgresql/postgresql.conf

# Port exposé
EXPOSE 5432

# Volume pour la persistance des données
VOLUME ["/var/lib/postgresql/data"]

# Healthcheck pour vérifier que PostgreSQL est prêt
HEALTHCHECK --interval=10s --timeout=5s --start-period=30s --retries=5 \
    CMD pg_isready -U testuser -d ndxpostgresql_test || exit 1

# Commande par défaut avec configuration personnalisée
CMD ["postgres", "-c", "config_file=/etc/postgresql/postgresql.conf"]

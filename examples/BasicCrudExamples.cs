// ============================================================================
// NDXPostgreSQL - Exemples CRUD de base
// ============================================================================
// Ce fichier contient des exemples d'opérations CRUD (Create, Read, Update, Delete)
// avec la bibliothèque NDXPostgreSQL.
//
// NOTE: Ces exemples sont fournis à titre de documentation.
//       Ils ne sont pas exécutés par les tests unitaires.
//
// Auteur: Nicolas DEOUX <NDXDev@gmail.com>
// ============================================================================

using System.Data;
using NDXPostgreSQL;

namespace NDXPostgreSQL.Examples;

/// <summary>
/// Exemples d'opérations CRUD de base.
/// </summary>
public static class BasicCrudExamples
{
    // ========================================================================
    // Configuration de la connexion
    // ========================================================================

    /// <summary>
    /// Exemple de configuration avec propriétés individuelles.
    /// </summary>
    public static PostgreSqlConnectionOptions GetOptionsWithProperties()
    {
        return new PostgreSqlConnectionOptions
        {
            Host = "localhost",
            Port = 5432,
            Database = "ma_base",
            Username = "mon_utilisateur",
            Password = "mon_mot_de_passe",
            Pooling = true,
            MinPoolSize = 5,
            MaxPoolSize = 100,
            ConnectionTimeoutSeconds = 30,
            CommandTimeoutSeconds = 60,
            LockTimeoutMs = 120_000,
            ApplicationName = "MonApplication"
        };
    }

    /// <summary>
    /// Exemple de configuration avec chaîne de connexion.
    /// </summary>
    public static PostgreSqlConnectionOptions GetOptionsWithConnectionString()
    {
        return new PostgreSqlConnectionOptions
        {
            ConnectionString = "Host=localhost;Port=5432;Database=ma_base;Username=mon_utilisateur;Password=mon_mot_de_passe;Pooling=true"
        };
    }

    // ========================================================================
    // CREATE - Insertions
    // ========================================================================

    /// <summary>
    /// Insertion simple d'un enregistrement.
    /// </summary>
    public static async Task InsertSimpleAsync(IPostgreSqlConnection connection)
    {
        var sql = "INSERT INTO clients (nom, email) VALUES (@nom, @email)";

        var rowsAffected = await connection.ExecuteNonQueryAsync(sql, new
        {
            nom = "Jean Dupont",
            email = "jean.dupont@example.com"
        });

        Console.WriteLine($"Lignes insérées: {rowsAffected}");
    }

    /// <summary>
    /// Insertion avec récupération de l'ID auto-incrémenté (RETURNING).
    /// </summary>
    public static async Task<int> InsertAndGetIdAsync(IPostgreSqlConnection connection)
    {
        // PostgreSQL utilise RETURNING pour récupérer l'ID généré
        var sql = "INSERT INTO clients (nom, email) VALUES (@nom, @email) RETURNING id";

        var newId = await connection.ExecuteScalarAsync<int>(sql, new
        {
            nom = "Marie Martin",
            email = "marie.martin@example.com"
        });

        Console.WriteLine($"Nouveau client créé avec l'ID: {newId}");
        return newId;
    }

    /// <summary>
    /// Insertion avec plusieurs champs de types différents.
    /// </summary>
    public static async Task InsertWithMultipleTypesAsync(IPostgreSqlConnection connection)
    {
        var sql = @"
            INSERT INTO produits (nom, description, prix, quantite, actif, date_creation)
            VALUES (@nom, @description, @prix, @quantite, @actif, @dateCreation)";

        await connection.ExecuteNonQueryAsync(sql, new
        {
            nom = "Laptop Pro",
            description = "Ordinateur portable haute performance",
            prix = 1299.99m,
            quantite = 50,
            actif = true,
            dateCreation = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Insertion multiple en une seule requête.
    /// </summary>
    public static async Task InsertMultipleRowsAsync(IPostgreSqlConnection connection)
    {
        var sql = @"
            INSERT INTO tags (nom) VALUES
            ('Électronique'),
            ('Informatique'),
            ('Bureau'),
            ('Accessoires')";

        var rowsAffected = await connection.ExecuteNonQueryAsync(sql);
        Console.WriteLine($"Tags créés: {rowsAffected}");
    }

    /// <summary>
    /// Insertion avec données JSONB (spécifique PostgreSQL).
    /// </summary>
    public static async Task InsertWithJsonbAsync(IPostgreSqlConnection connection)
    {
        var metadata = """{"role": "admin", "permissions": ["read", "write", "delete"]}""";

        var sql = @"
            INSERT INTO utilisateurs (nom, email, metadata)
            VALUES (@nom, @email, @metadata::jsonb)
            RETURNING id";

        var newId = await connection.ExecuteScalarAsync<int>(sql, new
        {
            nom = "Admin User",
            email = "admin@example.com",
            metadata
        });

        Console.WriteLine($"Utilisateur créé avec métadonnées JSONB, ID: {newId}");
    }

    // ========================================================================
    // READ - Lectures
    // ========================================================================

    /// <summary>
    /// Lecture d'une valeur scalaire.
    /// </summary>
    public static async Task<long> GetCountAsync(IPostgreSqlConnection connection)
    {
        var count = await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM clients");
        Console.WriteLine($"Nombre de clients: {count}");
        return count;
    }

    /// <summary>
    /// Lecture d'un enregistrement unique.
    /// </summary>
    public static async Task<DataTable> GetClientByIdAsync(IPostgreSqlConnection connection, int clientId)
    {
        var sql = "SELECT * FROM clients WHERE id = @id";
        var result = await connection.ExecuteQueryAsync(sql, new { id = clientId });

        if (result.Rows.Count > 0)
        {
            var row = result.Rows[0];
            Console.WriteLine($"Client trouvé: {row["nom"]} ({row["email"]})");
        }

        return result;
    }

    /// <summary>
    /// Lecture de plusieurs enregistrements avec filtres.
    /// </summary>
    public static async Task<DataTable> GetActiveClientsAsync(IPostgreSqlConnection connection)
    {
        var sql = @"
            SELECT id, nom, email, date_inscription
            FROM clients
            WHERE actif = @actif
            ORDER BY nom ASC
            LIMIT @limit";

        var result = await connection.ExecuteQueryAsync(sql, new
        {
            actif = true,
            limit = 100
        });

        Console.WriteLine($"Clients actifs trouvés: {result.Rows.Count}");
        return result;
    }

    /// <summary>
    /// Lecture avec jointures.
    /// </summary>
    public static async Task<DataTable> GetOrdersWithClientInfoAsync(IPostgreSqlConnection connection)
    {
        var sql = @"
            SELECT
                c.id AS commande_id,
                c.date_commande,
                c.montant_total,
                cl.nom AS client_nom,
                cl.email AS client_email
            FROM commandes c
            INNER JOIN clients cl ON c.client_id = cl.id
            WHERE c.date_commande >= @dateDebut
            ORDER BY c.date_commande DESC";

        var result = await connection.ExecuteQueryAsync(sql, new
        {
            dateDebut = DateTime.UtcNow.AddMonths(-1)
        });

        return result;
    }

    /// <summary>
    /// Lecture avec agrégations.
    /// </summary>
    public static async Task GetSalesStatisticsAsync(IPostgreSqlConnection connection)
    {
        var sql = @"
            SELECT
                COUNT(*) AS nombre_commandes,
                COALESCE(SUM(montant_total), 0) AS total_ventes,
                COALESCE(AVG(montant_total), 0) AS moyenne_commande,
                COALESCE(MIN(montant_total), 0) AS plus_petite_commande,
                COALESCE(MAX(montant_total), 0) AS plus_grande_commande
            FROM commandes
            WHERE date_commande >= @dateDebut";

        var result = await connection.ExecuteQueryAsync(sql, new
        {
            dateDebut = DateTime.UtcNow.AddMonths(-1)
        });

        if (result.Rows.Count > 0)
        {
            var row = result.Rows[0];
            Console.WriteLine($"Statistiques du mois:");
            Console.WriteLine($"  - Nombre de commandes: {row["nombre_commandes"]}");
            Console.WriteLine($"  - Total des ventes: {row["total_ventes"]:C}");
            Console.WriteLine($"  - Moyenne par commande: {row["moyenne_commande"]:C}");
        }
    }

    /// <summary>
    /// Utilisation du DataReader pour un traitement ligne par ligne.
    /// </summary>
    public static async Task ProcessLargeDatasetAsync(IPostgreSqlConnection connection)
    {
        var sql = "SELECT id, nom, email FROM clients WHERE actif = TRUE";

        await using var reader = await connection.ExecuteReaderAsync(sql);

        while (await reader.ReadAsync())
        {
            var id = reader.GetInt32(0);
            var nom = reader.GetString(1);
            var email = reader.GetString(2);

            // Traitement de chaque ligne...
            Console.WriteLine($"Traitement du client {id}: {nom}");
        }
    }

    /// <summary>
    /// Requête avec données JSONB (spécifique PostgreSQL).
    /// </summary>
    public static async Task QueryJsonbDataAsync(IPostgreSqlConnection connection)
    {
        // Rechercher les utilisateurs avec le rôle "admin"
        var sql = @"
            SELECT id, nom, email, metadata->>'role' AS role
            FROM utilisateurs
            WHERE metadata->>'role' = @role";

        var result = await connection.ExecuteQueryAsync(sql, new { role = "admin" });

        foreach (DataRow row in result.Rows)
        {
            Console.WriteLine($"Admin: {row["nom"]} - {row["email"]}");
        }

        // Rechercher dans un tableau JSONB
        var sqlWithArray = @"
            SELECT id, nom
            FROM utilisateurs
            WHERE metadata->'permissions' ? @permission";

        var writeUsers = await connection.ExecuteQueryAsync(sqlWithArray, new { permission = "write" });
        Console.WriteLine($"Utilisateurs avec permission 'write': {writeUsers.Rows.Count}");
    }

    // ========================================================================
    // UPDATE - Mises à jour
    // ========================================================================

    /// <summary>
    /// Mise à jour simple d'un enregistrement.
    /// </summary>
    public static async Task<int> UpdateClientEmailAsync(IPostgreSqlConnection connection, int clientId, string newEmail)
    {
        var sql = "UPDATE clients SET email = @email, date_modification = @dateMod WHERE id = @id";

        var rowsAffected = await connection.ExecuteNonQueryAsync(sql, new
        {
            id = clientId,
            email = newEmail,
            dateMod = DateTime.UtcNow
        });

        Console.WriteLine($"Client {clientId} mis à jour: {rowsAffected} ligne(s) affectée(s)");
        return rowsAffected;
    }

    /// <summary>
    /// Mise à jour de plusieurs enregistrements.
    /// </summary>
    public static async Task<int> DeactivateInactiveClientsAsync(IPostgreSqlConnection connection)
    {
        var sql = @"
            UPDATE clients
            SET actif = FALSE, date_desactivation = @dateDesact
            WHERE derniere_connexion < @dateLimite AND actif = TRUE";

        var rowsAffected = await connection.ExecuteNonQueryAsync(sql, new
        {
            dateDesact = DateTime.UtcNow,
            dateLimite = DateTime.UtcNow.AddYears(-1)
        });

        Console.WriteLine($"Clients désactivés: {rowsAffected}");
        return rowsAffected;
    }

    /// <summary>
    /// Mise à jour conditionnelle avec CASE.
    /// </summary>
    public static async Task UpdateProductPricesAsync(IPostgreSqlConnection connection)
    {
        var sql = @"
            UPDATE produits
            SET prix = CASE
                WHEN categorie = 'Électronique' THEN prix * 1.05
                WHEN categorie = 'Accessoires' THEN prix * 1.02
                ELSE prix
            END
            WHERE actif = TRUE";

        var rowsAffected = await connection.ExecuteNonQueryAsync(sql);
        Console.WriteLine($"Prix mis à jour pour {rowsAffected} produits");
    }

    /// <summary>
    /// Mise à jour de données JSONB.
    /// </summary>
    public static async Task UpdateJsonbDataAsync(IPostgreSqlConnection connection, int userId)
    {
        // Ajouter une permission au tableau JSONB
        var sql = @"
            UPDATE utilisateurs
            SET metadata = jsonb_set(
                metadata,
                '{permissions}',
                (metadata->'permissions') || '""admin""'::jsonb
            )
            WHERE id = @id";

        await connection.ExecuteNonQueryAsync(sql, new { id = userId });
        Console.WriteLine($"Permissions mises à jour pour l'utilisateur {userId}");
    }

    // ========================================================================
    // DELETE - Suppressions
    // ========================================================================

    /// <summary>
    /// Suppression d'un enregistrement par ID.
    /// </summary>
    public static async Task<int> DeleteClientAsync(IPostgreSqlConnection connection, int clientId)
    {
        var sql = "DELETE FROM clients WHERE id = @id";

        var rowsAffected = await connection.ExecuteNonQueryAsync(sql, new { id = clientId });
        Console.WriteLine($"Client {clientId} supprimé: {rowsAffected} ligne(s)");
        return rowsAffected;
    }

    /// <summary>
    /// Suppression conditionnelle de plusieurs enregistrements.
    /// </summary>
    public static async Task<int> DeleteOldLogsAsync(IPostgreSqlConnection connection)
    {
        var sql = "DELETE FROM logs WHERE date_creation < @dateLimite";

        var rowsAffected = await connection.ExecuteNonQueryAsync(sql, new
        {
            dateLimite = DateTime.UtcNow.AddMonths(-6)
        });

        Console.WriteLine($"Anciens logs supprimés: {rowsAffected}");
        return rowsAffected;
    }

    /// <summary>
    /// Soft delete (suppression logique).
    /// </summary>
    public static async Task<int> SoftDeleteClientAsync(IPostgreSqlConnection connection, int clientId)
    {
        var sql = @"
            UPDATE clients
            SET
                supprime = TRUE,
                date_suppression = @dateSup,
                supprime_par = @userId
            WHERE id = @id AND supprime = FALSE";

        var rowsAffected = await connection.ExecuteNonQueryAsync(sql, new
        {
            id = clientId,
            dateSup = DateTime.UtcNow,
            userId = 1 // ID de l'utilisateur courant
        });

        return rowsAffected;
    }

    // ========================================================================
    // Exemple d'utilisation complète
    // ========================================================================

    /// <summary>
    /// Exemple complet d'utilisation avec cycle de vie de la connexion.
    /// </summary>
    public static async Task FullExampleAsync()
    {
        var options = GetOptionsWithProperties();

        // Utilisation avec using pour garantir la fermeture
        await using var connection = new PostgreSqlConnection(options);

        // La connexion s'ouvre automatiquement à la première requête
        // Mais on peut l'ouvrir explicitement si nécessaire
        await connection.OpenAsync();

        try
        {
            // CREATE
            var newId = await InsertAndGetIdAsync(connection);

            // READ
            var client = await GetClientByIdAsync(connection, newId);

            // UPDATE
            await UpdateClientEmailAsync(connection, newId, "nouveau.email@example.com");

            // DELETE (soft delete)
            await SoftDeleteClientAsync(connection, newId);
        }
        finally
        {
            // La connexion sera fermée automatiquement par le using
            // Mais on peut la fermer explicitement
            await connection.CloseAsync();
        }
    }
}

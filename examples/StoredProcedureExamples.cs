// ============================================================================
// NDXPostgreSQL - Exemples de fonctions et procédures stockées
// ============================================================================
// Ce fichier contient des exemples d'utilisation des fonctions et procédures
// stockées PostgreSQL avec la bibliothèque NDXPostgreSQL.
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
/// Exemples d'utilisation des fonctions et procédures stockées PostgreSQL.
/// </summary>
public static class StoredProcedureExamples
{
    // ========================================================================
    // Création de fonctions (à exécuter une fois pour les tests)
    // ========================================================================

    /// <summary>
    /// Crée les fonctions et procédures de test dans la base de données.
    /// </summary>
    public static async Task CreateTestFunctionsAsync(IPostgreSqlConnection connection)
    {
        // Fonction simple retournant un scalaire
        await connection.ExecuteNonQueryAsync(@"
            CREATE OR REPLACE FUNCTION calculer_tva(montant_ht DECIMAL, taux_tva DECIMAL DEFAULT 0.20)
            RETURNS DECIMAL AS $$
            BEGIN
                RETURN montant_ht * taux_tva;
            END;
            $$ LANGUAGE plpgsql");

        // Fonction retournant un enregistrement
        await connection.ExecuteNonQueryAsync(@"
            CREATE OR REPLACE FUNCTION obtenir_stats_client(p_client_id INT)
            RETURNS TABLE(
                nombre_commandes BIGINT,
                total_achats DECIMAL,
                derniere_commande TIMESTAMP WITH TIME ZONE
            ) AS $$
            BEGIN
                RETURN QUERY
                SELECT
                    COUNT(*)::BIGINT,
                    COALESCE(SUM(montant_total), 0)::DECIMAL,
                    MAX(date_commande)
                FROM commandes
                WHERE client_id = p_client_id;
            END;
            $$ LANGUAGE plpgsql");

        // Fonction avec paramètres OUT
        await connection.ExecuteNonQueryAsync(@"
            CREATE OR REPLACE FUNCTION calculer_remise(
                p_montant DECIMAL,
                p_pourcentage DECIMAL,
                OUT montant_remise DECIMAL,
                OUT montant_final DECIMAL
            ) AS $$
            BEGIN
                montant_remise := p_montant * (p_pourcentage / 100);
                montant_final := p_montant - montant_remise;
            END;
            $$ LANGUAGE plpgsql");

        // Fonction retournant SETOF (plusieurs lignes)
        await connection.ExecuteNonQueryAsync(@"
            CREATE OR REPLACE FUNCTION rechercher_clients(p_terme TEXT)
            RETURNS SETOF clients AS $$
            BEGIN
                RETURN QUERY
                SELECT *
                FROM clients
                WHERE nom ILIKE '%' || p_terme || '%'
                   OR email ILIKE '%' || p_terme || '%';
            END;
            $$ LANGUAGE plpgsql");

        // Procédure stockée (PostgreSQL 11+)
        await connection.ExecuteNonQueryAsync(@"
            CREATE OR REPLACE PROCEDURE creer_commande(
                p_client_id INT,
                p_montant DECIMAL,
                INOUT p_commande_id INT DEFAULT NULL
            )
            LANGUAGE plpgsql
            AS $$
            BEGIN
                INSERT INTO commandes (client_id, montant_total, date_commande, statut)
                VALUES (p_client_id, p_montant, CURRENT_TIMESTAMP, 'nouvelle')
                RETURNING id INTO p_commande_id;
            END;
            $$");

        // Procédure avec transaction interne
        await connection.ExecuteNonQueryAsync(@"
            CREATE OR REPLACE PROCEDURE transferer_stock(
                p_produit_source INT,
                p_produit_destination INT,
                p_quantite INT
            )
            LANGUAGE plpgsql
            AS $$
            BEGIN
                -- Retirer du stock source
                UPDATE produits
                SET quantite = quantite - p_quantite
                WHERE id = p_produit_source;

                -- Vérifier que le stock n'est pas négatif
                IF (SELECT quantite FROM produits WHERE id = p_produit_source) < 0 THEN
                    RAISE EXCEPTION 'Stock insuffisant pour le produit %', p_produit_source;
                END IF;

                -- Ajouter au stock destination
                UPDATE produits
                SET quantite = quantite + p_quantite
                WHERE id = p_produit_destination;

                COMMIT;
            END;
            $$");

        Console.WriteLine("Fonctions et procédures de test créées avec succès.");
    }

    // ========================================================================
    // Appel de fonctions simples
    // ========================================================================

    /// <summary>
    /// Appel d'une fonction retournant un scalaire.
    /// </summary>
    public static async Task<decimal> CalculerTvaAsync(IPostgreSqlConnection connection, decimal montantHT, decimal tauxTva = 0.20m)
    {
        var result = await connection.ExecuteScalarAsync<decimal>(
            "SELECT calculer_tva(@montant, @taux)",
            new { montant = montantHT, taux = tauxTva });

        Console.WriteLine($"Montant HT: {montantHT:C}, TVA ({tauxTva:P0}): {result:C}");
        return result;
    }

    /// <summary>
    /// Appel d'une fonction retournant un enregistrement.
    /// </summary>
    public static async Task AfficherStatsClientAsync(IPostgreSqlConnection connection, int clientId)
    {
        var result = await connection.ExecuteQueryAsync(
            "SELECT * FROM obtenir_stats_client(@id)",
            new { id = clientId });

        if (result.Rows.Count > 0)
        {
            var row = result.Rows[0];
            Console.WriteLine($"Statistiques du client {clientId}:");
            Console.WriteLine($"  - Nombre de commandes: {row["nombre_commandes"]}");
            Console.WriteLine($"  - Total des achats: {row["total_achats"]:C}");
            Console.WriteLine($"  - Dernière commande: {row["derniere_commande"]}");
        }
    }

    /// <summary>
    /// Appel d'une fonction avec paramètres OUT.
    /// </summary>
    public static async Task CalculerRemiseAsync(IPostgreSqlConnection connection, decimal montant, decimal pourcentage)
    {
        var result = await connection.ExecuteQueryAsync(
            "SELECT * FROM calculer_remise(@montant, @pourcentage)",
            new { montant, pourcentage });

        if (result.Rows.Count > 0)
        {
            var row = result.Rows[0];
            Console.WriteLine($"Calcul de remise pour {montant:C} à {pourcentage}%:");
            Console.WriteLine($"  - Montant de la remise: {row["montant_remise"]:C}");
            Console.WriteLine($"  - Montant final: {row["montant_final"]:C}");
        }
    }

    /// <summary>
    /// Appel d'une fonction retournant plusieurs lignes.
    /// </summary>
    public static async Task RechercherClientsAsync(IPostgreSqlConnection connection, string terme)
    {
        var result = await connection.ExecuteQueryAsync(
            "SELECT * FROM rechercher_clients(@terme)",
            new { terme });

        Console.WriteLine($"Résultats de recherche pour '{terme}':");
        foreach (DataRow row in result.Rows)
        {
            Console.WriteLine($"  - {row["nom"]} ({row["email"]})");
        }
    }

    // ========================================================================
    // Appel de procédures stockées
    // ========================================================================

    /// <summary>
    /// Appel d'une procédure avec paramètre INOUT.
    /// </summary>
    public static async Task<int> CreerCommandeAsync(IPostgreSqlConnection connection, int clientId, decimal montant)
    {
        // PostgreSQL utilise CALL pour les procédures
        // Le paramètre INOUT retourne la valeur via SELECT
        var result = await connection.ExecuteQueryAsync(
            "CALL creer_commande(@clientId, @montant, NULL)",
            new { clientId, montant });

        var commandeId = 0;
        if (result.Rows.Count > 0 && result.Rows[0]["p_commande_id"] != DBNull.Value)
        {
            commandeId = Convert.ToInt32(result.Rows[0]["p_commande_id"]);
        }

        Console.WriteLine($"Commande créée avec l'ID: {commandeId}");
        return commandeId;
    }

    /// <summary>
    /// Appel d'une procédure avec gestion d'erreur.
    /// </summary>
    public static async Task TransfererStockAsync(
        IPostgreSqlConnection connection,
        int produitSource,
        int produitDestination,
        int quantite)
    {
        try
        {
            await connection.ExecuteNonQueryAsync(
                "CALL transferer_stock(@source, @destination, @quantite)",
                new { source = produitSource, destination = produitDestination, quantite });

            Console.WriteLine($"Transfert de {quantite} unités effectué avec succès.");
        }
        catch (Npgsql.PostgresException ex) when (ex.SqlState == "P0001") // RAISE EXCEPTION
        {
            Console.WriteLine($"Erreur lors du transfert: {ex.Message}");
            throw;
        }
    }

    // ========================================================================
    // Fonctions avancées PostgreSQL
    // ========================================================================

    /// <summary>
    /// Utilisation de fonctions d'agrégation personnalisées.
    /// </summary>
    public static async Task UtiliserFonctionsAgregationAsync(IPostgreSqlConnection connection)
    {
        // Utiliser array_agg pour collecter des valeurs
        var result = await connection.ExecuteQueryAsync(@"
            SELECT
                client_id,
                array_agg(DISTINCT statut ORDER BY statut) AS statuts,
                SUM(montant_total) AS total
            FROM commandes
            GROUP BY client_id
            HAVING COUNT(*) > 1");

        foreach (DataRow row in result.Rows)
        {
            Console.WriteLine($"Client {row["client_id"]}: {row["total"]:C}");
        }
    }

    /// <summary>
    /// Utilisation de fonctions de fenêtrage (window functions).
    /// </summary>
    public static async Task UtiliserWindowFunctionsAsync(IPostgreSqlConnection connection)
    {
        var result = await connection.ExecuteQueryAsync(@"
            SELECT
                id,
                client_id,
                montant_total,
                ROW_NUMBER() OVER (PARTITION BY client_id ORDER BY date_commande DESC) AS rang,
                SUM(montant_total) OVER (PARTITION BY client_id) AS total_client,
                AVG(montant_total) OVER () AS moyenne_globale
            FROM commandes
            ORDER BY client_id, rang");

        Console.WriteLine("Analyse des commandes avec fonctions de fenêtrage:");
        foreach (DataRow row in result.Rows)
        {
            Console.WriteLine($"  Commande {row["id"]}: Rang {row["rang"]} pour client {row["client_id"]}");
        }
    }

    /// <summary>
    /// Utilisation de fonctions avec types composites.
    /// </summary>
    public static async Task UtiliserTypesCompositesAsync(IPostgreSqlConnection connection)
    {
        // Créer un type composite
        await connection.ExecuteNonQueryAsync(@"
            DO $$
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'adresse') THEN
                    CREATE TYPE adresse AS (
                        rue TEXT,
                        ville TEXT,
                        code_postal TEXT,
                        pays TEXT
                    );
                END IF;
            END
            $$");

        // Fonction retournant un type composite
        await connection.ExecuteNonQueryAsync(@"
            CREATE OR REPLACE FUNCTION creer_adresse(
                p_rue TEXT,
                p_ville TEXT,
                p_cp TEXT,
                p_pays TEXT DEFAULT 'France'
            )
            RETURNS adresse AS $$
            BEGIN
                RETURN (p_rue, p_ville, p_cp, p_pays)::adresse;
            END;
            $$ LANGUAGE plpgsql");

        // Utiliser la fonction
        var result = await connection.ExecuteQueryAsync(@"
            SELECT (creer_adresse('123 Rue Example', 'Paris', '75001', 'France')).*");

        if (result.Rows.Count > 0)
        {
            var row = result.Rows[0];
            Console.WriteLine($"Adresse créée: {row["rue"]}, {row["code_postal"]} {row["ville"]}, {row["pays"]}");
        }
    }

    // ========================================================================
    // Classe pour stocker les statistiques client
    // ========================================================================

    /// <summary>
    /// Classe pour stocker les statistiques client.
    /// </summary>
    public class ClientStats
    {
        public int TotalOrders { get; set; }
        public decimal TotalSpent { get; set; }
        public decimal AverageOrder { get; set; }
        public DateTime? FirstOrderDate { get; set; }
        public DateTime? LastOrderDate { get; set; }
        public string LoyaltyLevel { get; set; } = "BRONZE";
    }

    /// <summary>
    /// Récupère les statistiques d'un client.
    /// </summary>
    public static async Task<ClientStats> GetClientStatsAsync(IPostgreSqlConnection connection, int clientId)
    {
        var result = await connection.ExecuteQueryAsync(@"
            SELECT
                COUNT(*) AS total_orders,
                COALESCE(SUM(montant_total), 0) AS total_spent,
                COALESCE(AVG(montant_total), 0) AS average_order,
                MIN(date_commande) AS first_order,
                MAX(date_commande) AS last_order,
                CASE
                    WHEN COALESCE(SUM(montant_total), 0) >= 10000 THEN 'PLATINUM'
                    WHEN COALESCE(SUM(montant_total), 0) >= 5000 THEN 'GOLD'
                    WHEN COALESCE(SUM(montant_total), 0) >= 1000 THEN 'SILVER'
                    ELSE 'BRONZE'
                END AS loyalty_level
            FROM commandes
            WHERE client_id = @clientId AND statut = 'COMPLETED'",
            new { clientId });

        var stats = new ClientStats();
        if (result.Rows.Count > 0)
        {
            var row = result.Rows[0];
            stats.TotalOrders = Convert.ToInt32(row["total_orders"]);
            stats.TotalSpent = Convert.ToDecimal(row["total_spent"]);
            stats.AverageOrder = Convert.ToDecimal(row["average_order"]);
            stats.FirstOrderDate = row["first_order"] == DBNull.Value ? null : Convert.ToDateTime(row["first_order"]);
            stats.LastOrderDate = row["last_order"] == DBNull.Value ? null : Convert.ToDateTime(row["last_order"]);
            stats.LoyaltyLevel = row["loyalty_level"]?.ToString() ?? "BRONZE";
        }

        Console.WriteLine($"Statistiques client {clientId}:");
        Console.WriteLine($"  - Commandes: {stats.TotalOrders}");
        Console.WriteLine($"  - Total dépensé: {stats.TotalSpent:C}");
        Console.WriteLine($"  - Moyenne: {stats.AverageOrder:C}");
        Console.WriteLine($"  - Niveau fidélité: {stats.LoyaltyLevel}");

        return stats;
    }

    // ========================================================================
    // Exemple d'utilisation complète
    // ========================================================================

    /// <summary>
    /// Exemple complet d'utilisation des fonctions et procédures.
    /// </summary>
    public static async Task FullExampleAsync()
    {
        var options = new PostgreSqlConnectionOptions
        {
            Host = "localhost",
            Port = 5432,
            Database = "test_db",
            Username = "test_user",
            Password = "test_password"
        };

        await using var connection = new PostgreSqlConnection(options);
        await connection.OpenAsync();

        // Créer les fonctions de test
        await CreateTestFunctionsAsync(connection);

        // Utiliser les fonctions
        await CalculerTvaAsync(connection, 100.00m);
        await CalculerRemiseAsync(connection, 150.00m, 10);

        // Appeler une procédure
        try
        {
            await CreerCommandeAsync(connection, 1, 299.99m);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Note: L'exemple nécessite des données de test. {ex.Message}");
        }
    }
}

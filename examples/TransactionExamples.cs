// ============================================================================
// NDXPostgreSQL - Exemples de Transactions
// ============================================================================
// Ce fichier contient des exemples d'utilisation des transactions
// avec différents niveaux d'isolation et cas d'usage.
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
/// Exemples d'utilisation des transactions PostgreSQL.
/// </summary>
public static class TransactionExamples
{
    // ========================================================================
    // Transactions de base
    // ========================================================================

    /// <summary>
    /// Transaction simple avec commit.
    /// </summary>
    public static async Task SimpleTransactionAsync(IPostgreSqlConnection connection)
    {
        // Démarrer la transaction
        await connection.BeginTransactionAsync();

        try
        {
            // Effectuer les opérations
            await connection.ExecuteNonQueryAsync(
                "INSERT INTO clients (nom, email) VALUES (@nom, @email)",
                new { nom = "Client Test", email = "test@example.com" });

            await connection.ExecuteNonQueryAsync(
                "INSERT INTO logs (action, message) VALUES (@action, @message)",
                new { action = "CREATE_CLIENT", message = "Nouveau client créé" });

            // Valider la transaction
            await connection.CommitAsync();
            Console.WriteLine("Transaction validée avec succès");
        }
        catch (Exception ex)
        {
            // Annuler en cas d'erreur
            await connection.RollbackAsync();
            Console.WriteLine($"Transaction annulée: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Transaction avec rollback explicite.
    /// </summary>
    public static async Task TransactionWithRollbackAsync(IPostgreSqlConnection connection)
    {
        await connection.BeginTransactionAsync();

        try
        {
            // Première opération
            await connection.ExecuteNonQueryAsync(
                "UPDATE comptes SET solde = solde - @montant WHERE id = @id",
                new { montant = 100.00m, id = 1 });

            // Vérifier une condition
            var solde = await connection.ExecuteScalarAsync<decimal>(
                "SELECT solde FROM comptes WHERE id = @id",
                new { id = 1 });

            if (solde < 0)
            {
                // Solde insuffisant, annuler
                await connection.RollbackAsync();
                Console.WriteLine("Transaction annulée: solde insuffisant");
                return;
            }

            // Deuxième opération
            await connection.ExecuteNonQueryAsync(
                "UPDATE comptes SET solde = solde + @montant WHERE id = @id",
                new { montant = 100.00m, id = 2 });

            await connection.CommitAsync();
            Console.WriteLine("Transfert effectué avec succès");
        }
        catch
        {
            await connection.RollbackAsync();
            throw;
        }
    }

    // ========================================================================
    // Niveaux d'isolation PostgreSQL
    // ========================================================================

    /// <summary>
    /// Transaction avec niveau d'isolation READ UNCOMMITTED.
    /// Note: PostgreSQL traite READ UNCOMMITTED comme READ COMMITTED.
    /// </summary>
    public static async Task ReadUncommittedTransactionAsync(IPostgreSqlConnection connection)
    {
        // PostgreSQL ne supporte pas vraiment READ UNCOMMITTED
        // Il est traité comme READ COMMITTED
        await connection.BeginTransactionAsync(IsolationLevel.ReadUncommitted);

        try
        {
            var data = await connection.ExecuteQueryAsync("SELECT * FROM produits WHERE stock > 0");
            Console.WriteLine($"Produits en stock: {data.Rows.Count}");
            Console.WriteLine("Note: PostgreSQL traite READ UNCOMMITTED comme READ COMMITTED");

            await connection.CommitAsync();
        }
        catch
        {
            await connection.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// Transaction avec niveau d'isolation READ COMMITTED (par défaut PostgreSQL).
    /// </summary>
    public static async Task ReadCommittedTransactionAsync(IPostgreSqlConnection connection)
    {
        await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted);

        try
        {
            // Lecture 1
            var stock1 = await connection.ExecuteScalarAsync<int>(
                "SELECT stock FROM produits WHERE id = @id",
                new { id = 1 });

            // ... traitement ...
            await Task.Delay(100);

            // Lecture 2 - peut retourner une valeur différente si une autre
            // transaction a modifié et committé entre-temps
            var stock2 = await connection.ExecuteScalarAsync<int>(
                "SELECT stock FROM produits WHERE id = @id",
                new { id = 1 });

            Console.WriteLine($"Stock initial: {stock1}, Stock actuel: {stock2}");
            Console.WriteLine("(Les valeurs peuvent différer en READ COMMITTED)");

            await connection.CommitAsync();
        }
        catch
        {
            await connection.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// Transaction avec niveau d'isolation REPEATABLE READ.
    /// Garantit des lectures répétables et évite les lectures fantômes.
    /// </summary>
    public static async Task RepeatableReadTransactionAsync(IPostgreSqlConnection connection)
    {
        await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead);

        try
        {
            // Les lectures suivantes retourneront toujours les mêmes valeurs
            var stock1 = await connection.ExecuteScalarAsync<int>(
                "SELECT stock FROM produits WHERE id = @id",
                new { id = 1 });

            // Simulation d'un traitement long
            await Task.Delay(1000);

            var stock2 = await connection.ExecuteScalarAsync<int>(
                "SELECT stock FROM produits WHERE id = @id",
                new { id = 1 });

            // stock1 == stock2 est garanti
            Console.WriteLine($"Stock (lecture répétable garantie): {stock1} == {stock2}");

            await connection.CommitAsync();
        }
        catch (Npgsql.PostgresException ex) when (ex.SqlState == "40001")
        {
            // Erreur de sérialisation possible en REPEATABLE READ
            await connection.RollbackAsync();
            Console.WriteLine($"Conflit de sérialisation: {ex.Message}");
            throw;
        }
        catch
        {
            await connection.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// Transaction avec niveau d'isolation SERIALIZABLE.
    /// Plus haut niveau d'isolation, transactions totalement isolées.
    /// </summary>
    public static async Task SerializableTransactionAsync(IPostgreSqlConnection connection)
    {
        await connection.BeginTransactionAsync(IsolationLevel.Serializable);

        try
        {
            // Les requêtes sont exécutées de manière sérialisée
            var commandes = await connection.ExecuteQueryAsync(
                "SELECT * FROM commandes WHERE statut = 'PENDING'");

            foreach (DataRow row in commandes.Rows)
            {
                await connection.ExecuteNonQueryAsync(
                    "UPDATE commandes SET statut = 'PROCESSING' WHERE id = @id",
                    new { id = row["id"] });
            }

            await connection.CommitAsync();
            Console.WriteLine($"Traité {commandes.Rows.Count} commandes en mode sérialisé");
        }
        catch (Npgsql.PostgresException ex) when (ex.SqlState == "40001")
        {
            // Erreur de sérialisation - il faut réessayer la transaction
            await connection.RollbackAsync();
            Console.WriteLine($"Conflit de sérialisation, réessayez: {ex.Message}");
            throw;
        }
        catch
        {
            await connection.RollbackAsync();
            throw;
        }
    }

    // ========================================================================
    // Savepoints (Points de sauvegarde)
    // ========================================================================

    /// <summary>
    /// Utilisation des savepoints pour des rollbacks partiels.
    /// </summary>
    public static async Task SavepointExampleAsync(IPostgreSqlConnection connection)
    {
        await connection.BeginTransactionAsync();

        try
        {
            // Première opération
            await connection.ExecuteNonQueryAsync(
                "INSERT INTO clients (nom, email) VALUES (@nom, @email)",
                new { nom = "Client 1", email = "client1@example.com" });
            Console.WriteLine("Client 1 inséré");

            // Créer un savepoint
            await connection.SaveAsync("sp1");
            Console.WriteLine("Savepoint 'sp1' créé");

            // Deuxième opération
            await connection.ExecuteNonQueryAsync(
                "INSERT INTO clients (nom, email) VALUES (@nom, @email)",
                new { nom = "Client 2", email = "client2@example.com" });
            Console.WriteLine("Client 2 inséré");

            // Créer un autre savepoint
            await connection.SaveAsync("sp2");
            Console.WriteLine("Savepoint 'sp2' créé");

            // Troisième opération qui échoue
            try
            {
                await connection.ExecuteNonQueryAsync(
                    "INSERT INTO clients (nom, email) VALUES (@nom, @email)",
                    new { nom = "Client 3", email = "client1@example.com" }); // Email en double!
            }
            catch
            {
                // Rollback jusqu'au savepoint sp2 (annule seulement Client 3)
                await connection.RollbackToSavepointAsync("sp2");
                Console.WriteLine("Rollback vers 'sp2' - Client 3 annulé");
            }

            // Le Client 1 et 2 sont toujours dans la transaction
            await connection.CommitAsync();
            Console.WriteLine("Transaction committée avec Client 1 et 2");
        }
        catch
        {
            await connection.RollbackAsync();
            throw;
        }
    }

    // ========================================================================
    // Cas d'usage courants
    // ========================================================================

    /// <summary>
    /// Transfert d'argent entre deux comptes avec verrouillage FOR UPDATE.
    /// </summary>
    public static async Task<bool> TransferMoneyAsync(
        IPostgreSqlConnection connection,
        int fromAccountId,
        int toAccountId,
        decimal amount)
    {
        if (amount <= 0)
        {
            throw new ArgumentException("Le montant doit être positif", nameof(amount));
        }

        await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead);

        try
        {
            // Verrouiller les deux comptes dans l'ordre des IDs pour éviter les deadlocks
            var minId = Math.Min(fromAccountId, toAccountId);
            var maxId = Math.Max(fromAccountId, toAccountId);

            // Verrouiller le premier compte
            await connection.ExecuteScalarAsync<decimal>(
                "SELECT solde FROM comptes WHERE id = @id FOR UPDATE",
                new { id = minId });

            // Verrouiller le second compte
            await connection.ExecuteScalarAsync<decimal>(
                "SELECT solde FROM comptes WHERE id = @id FOR UPDATE",
                new { id = maxId });

            // Vérifier le solde du compte source
            var sourceBalance = await connection.ExecuteScalarAsync<decimal>(
                "SELECT solde FROM comptes WHERE id = @id",
                new { id = fromAccountId });

            if (sourceBalance < amount)
            {
                await connection.RollbackAsync();
                Console.WriteLine("Transfert refusé: solde insuffisant");
                return false;
            }

            // Débiter le compte source
            await connection.ExecuteNonQueryAsync(
                "UPDATE comptes SET solde = solde - @amount, date_modification = NOW() WHERE id = @id",
                new { amount, id = fromAccountId });

            // Créditer le compte destination
            await connection.ExecuteNonQueryAsync(
                "UPDATE comptes SET solde = solde + @amount, date_modification = NOW() WHERE id = @id",
                new { amount, id = toAccountId });

            // Enregistrer la transaction financière
            await connection.ExecuteNonQueryAsync(
                @"INSERT INTO transactions_financieres
                  (compte_source, compte_destination, montant, date_transaction, type)
                  VALUES (@from, @to, @amount, NOW(), 'TRANSFER')",
                new { from = fromAccountId, to = toAccountId, amount });

            await connection.CommitAsync();
            Console.WriteLine($"Transfert de {amount:C} effectué avec succès");
            return true;
        }
        catch (Npgsql.PostgresException ex) when (ex.SqlState == "40001" || ex.SqlState == "40P01")
        {
            await connection.RollbackAsync();
            Console.WriteLine($"Conflit de transaction: {ex.Message}");
            throw;
        }
        catch (Exception ex)
        {
            await connection.RollbackAsync();
            Console.WriteLine($"Erreur lors du transfert: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Création d'une commande avec mise à jour du stock (transaction complète).
    /// </summary>
    public static async Task<int> CreateOrderWithStockUpdateAsync(
        IPostgreSqlConnection connection,
        int clientId,
        List<(int ProductId, int Quantity)> items)
    {
        await connection.BeginTransactionAsync();

        try
        {
            // 1. Vérifier la disponibilité du stock avec verrouillage
            foreach (var (productId, quantity) in items)
            {
                var stock = await connection.ExecuteScalarAsync<int>(
                    "SELECT stock FROM produits WHERE id = @id FOR UPDATE",
                    new { id = productId });

                if (stock < quantity)
                {
                    await connection.RollbackAsync();
                    throw new InvalidOperationException(
                        $"Stock insuffisant pour le produit {productId}");
                }
            }

            // 2. Créer la commande avec RETURNING pour récupérer l'ID
            var orderId = await connection.ExecuteScalarAsync<int>(
                @"INSERT INTO commandes (client_id, date_commande, statut, montant_total)
                  VALUES (@clientId, NOW(), 'PENDING', 0)
                  RETURNING id",
                new { clientId });

            // 3. Ajouter les lignes de commande et calculer le total
            decimal total = 0;

            foreach (var (productId, quantity) in items)
            {
                // Récupérer le prix du produit
                var price = await connection.ExecuteScalarAsync<decimal>(
                    "SELECT prix FROM produits WHERE id = @id",
                    new { id = productId });

                var lineTotal = price * quantity;
                total += lineTotal;

                // Ajouter la ligne de commande
                await connection.ExecuteNonQueryAsync(
                    @"INSERT INTO lignes_commande
                      (commande_id, produit_id, quantite, prix_unitaire, total_ligne)
                      VALUES (@orderId, @productId, @quantity, @price, @lineTotal)",
                    new { orderId, productId, quantity, price, lineTotal });

                // Décrémenter le stock
                await connection.ExecuteNonQueryAsync(
                    "UPDATE produits SET stock = stock - @quantity WHERE id = @id",
                    new { quantity, id = productId });
            }

            // 4. Mettre à jour le total de la commande
            await connection.ExecuteNonQueryAsync(
                "UPDATE commandes SET montant_total = @total WHERE id = @orderId",
                new { total, orderId });

            // 5. Valider la transaction
            await connection.CommitAsync();

            Console.WriteLine($"Commande #{orderId} créée - Total: {total:C}");
            return orderId;
        }
        catch
        {
            await connection.RollbackAsync();
            throw;
        }
    }

    // ========================================================================
    // Opérations en masse avec batch
    // ========================================================================

    /// <summary>
    /// Insertion en masse avec transaction et COPY (optimisé PostgreSQL).
    /// </summary>
    public static async Task<int> BulkInsertAsync(
        IPostgreSqlConnection connection,
        List<(string Nom, string Email)> clients)
    {
        await connection.BeginTransactionAsync();

        try
        {
            var inserted = 0;

            // Pour des insertions très volumineuses, utiliser COPY serait plus efficace
            // Ici on utilise une approche par batch avec des INSERT multiples
            const int batchSize = 100;

            for (int i = 0; i < clients.Count; i += batchSize)
            {
                var batch = clients.Skip(i).Take(batchSize).ToList();

                foreach (var (nom, email) in batch)
                {
                    var result = await connection.ExecuteNonQueryAsync(
                        "INSERT INTO clients (nom, email, date_inscription) VALUES (@nom, @email, NOW())",
                        new { nom, email });
                    inserted += result;
                }

                Console.WriteLine($"Batch {i / batchSize + 1}: {batch.Count} enregistrements insérés");
            }

            await connection.CommitAsync();
            Console.WriteLine($"Total inséré: {inserted}");
            return inserted;
        }
        catch
        {
            await connection.RollbackAsync();
            throw;
        }
    }

    // ========================================================================
    // Gestion des erreurs et retry
    // ========================================================================

    /// <summary>
    /// Transaction avec retry en cas de conflit de sérialisation.
    /// </summary>
    public static async Task<T> ExecuteWithRetryAsync<T>(
        IPostgreSqlConnection connection,
        Func<Task<T>> operation,
        int maxRetries = 3)
    {
        var retryCount = 0;
        var delay = TimeSpan.FromMilliseconds(100);

        while (true)
        {
            try
            {
                return await operation();
            }
            catch (Npgsql.PostgresException ex) when (
                ex.SqlState == "40001" || // serialization_failure
                ex.SqlState == "40P01")   // deadlock_detected
            {
                retryCount++;
                if (retryCount >= maxRetries)
                {
                    Console.WriteLine($"Conflit de transaction - abandon après {maxRetries} tentatives");
                    throw;
                }

                Console.WriteLine($"Conflit détecté - tentative {retryCount}/{maxRetries}");
                await Task.Delay(delay);
                delay *= 2; // Backoff exponentiel
            }
        }
    }

    // ========================================================================
    // Exemple complet
    // ========================================================================

    /// <summary>
    /// Exemple complet d'utilisation des transactions.
    /// </summary>
    public static async Task FullExampleAsync()
    {
        var options = new PostgreSqlConnectionOptions
        {
            Host = "localhost",
            Port = 5432,
            Database = "ma_base",
            Username = "mon_user",
            Password = "mon_pass"
        };

        await using var connection = new PostgreSqlConnection(options);

        // Transaction simple
        await SimpleTransactionAsync(connection);

        // Savepoints
        await SavepointExampleAsync(connection);

        // Transfert d'argent avec retry
        await ExecuteWithRetryAsync(connection, async () =>
        {
            return await TransferMoneyAsync(connection, 1, 2, 500.00m);
        });

        Console.WriteLine("Toutes les opérations terminées avec succès!");
    }
}

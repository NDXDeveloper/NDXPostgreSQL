// ============================================================================
// NDXPostgreSQL - Exemples de tâches planifiées avec pg_cron
// ============================================================================
// Ce fichier contient des exemples d'utilisation de l'extension pg_cron
// pour planifier des tâches récurrentes dans PostgreSQL.
//
// NOTE: pg_cron doit être installé et activé sur le serveur PostgreSQL.
//       Ces exemples sont fournis à titre de documentation.
//
// Auteur: Nicolas DEOUX <NDXDev@gmail.com>
// ============================================================================

using NDXPostgreSQL;

namespace NDXPostgreSQL.Examples;

/// <summary>
/// Exemples d'utilisation de pg_cron pour les tâches planifiées PostgreSQL.
/// </summary>
public static class ScheduledJobsExamples
{
    // ========================================================================
    // Vérification de pg_cron
    // ========================================================================

    /// <summary>
    /// Vérifie si l'extension pg_cron est disponible et activée.
    /// </summary>
    public static async Task<bool> IsPgCronAvailableAsync(IPostgreSqlConnection connection)
    {
        var result = await connection.ExecuteScalarAsync<bool>(
            "SELECT EXISTS(SELECT 1 FROM pg_extension WHERE extname = 'pg_cron')");

        if (result)
        {
            Console.WriteLine("pg_cron est installé et disponible.");
        }
        else
        {
            Console.WriteLine("pg_cron n'est pas installé. Pour l'installer:");
            Console.WriteLine("  1. Ajouter 'pg_cron' à shared_preload_libraries dans postgresql.conf");
            Console.WriteLine("  2. Redémarrer PostgreSQL");
            Console.WriteLine("  3. Exécuter: CREATE EXTENSION pg_cron;");
        }

        return result;
    }

    /// <summary>
    /// Installe l'extension pg_cron si elle n'est pas déjà installée.
    /// </summary>
    public static async Task InstallPgCronAsync(IPostgreSqlConnection connection)
    {
        try
        {
            await connection.ExecuteNonQueryAsync("CREATE EXTENSION IF NOT EXISTS pg_cron");
            Console.WriteLine("Extension pg_cron installée avec succès.");
        }
        catch (Npgsql.PostgresException ex)
        {
            Console.WriteLine($"Erreur lors de l'installation de pg_cron: {ex.Message}");
            Console.WriteLine("Assurez-vous que pg_cron est dans shared_preload_libraries.");
        }
    }

    // ========================================================================
    // Création de jobs planifiés
    // ========================================================================

    /// <summary>
    /// Crée un job de nettoyage des logs anciens exécuté chaque jour à minuit.
    /// </summary>
    public static async Task<long> CreateLogCleanupJobAsync(IPostgreSqlConnection connection, int retentionDays = 30)
    {
        var jobId = await connection.ExecuteScalarAsync<long>(@"
            SELECT cron.schedule(
                'cleanup_old_logs',
                '0 0 * * *',
                $$DELETE FROM logs WHERE created_at < NOW() - INTERVAL '" + retentionDays + @" days'$$
            )");

        Console.WriteLine($"Job de nettoyage des logs créé avec l'ID: {jobId}");
        Console.WriteLine($"Planification: Chaque jour à minuit");
        Console.WriteLine($"Rétention: {retentionDays} jours");
        return jobId;
    }

    /// <summary>
    /// Crée un job de mise à jour des statistiques exécuté toutes les heures.
    /// </summary>
    public static async Task<long> CreateStatsUpdateJobAsync(IPostgreSqlConnection connection)
    {
        var jobId = await connection.ExecuteScalarAsync<long>(@"
            SELECT cron.schedule(
                'update_stats_hourly',
                '0 * * * *',
                $$
                    INSERT INTO statistiques_horaires (heure, nombre_commandes, montant_total)
                    SELECT
                        date_trunc('hour', NOW()),
                        COUNT(*),
                        COALESCE(SUM(montant_total), 0)
                    FROM commandes
                    WHERE date_commande >= date_trunc('hour', NOW())
                      AND date_commande < date_trunc('hour', NOW()) + INTERVAL '1 hour'
                    ON CONFLICT (heure) DO UPDATE SET
                        nombre_commandes = EXCLUDED.nombre_commandes,
                        montant_total = EXCLUDED.montant_total
                $$
            )");

        Console.WriteLine($"Job de mise à jour des stats créé avec l'ID: {jobId}");
        return jobId;
    }

    /// <summary>
    /// Crée un job d'archivage des anciennes commandes exécuté chaque semaine.
    /// </summary>
    public static async Task<long> CreateArchiveJobAsync(IPostgreSqlConnection connection)
    {
        var jobId = await connection.ExecuteScalarAsync<long>(@"
            SELECT cron.schedule(
                'archive_old_orders',
                '0 2 * * 0',
                $$
                    WITH archived AS (
                        DELETE FROM commandes
                        WHERE date_commande < NOW() - INTERVAL '1 year'
                          AND statut = 'terminee'
                        RETURNING *
                    )
                    INSERT INTO commandes_archive
                    SELECT *, NOW() AS archived_at
                    FROM archived
                $$
            )");

        Console.WriteLine($"Job d'archivage créé avec l'ID: {jobId}");
        Console.WriteLine("Planification: Chaque dimanche à 2h du matin");
        return jobId;
    }

    /// <summary>
    /// Crée un job exécuté toutes les minutes (utile pour les tests).
    /// </summary>
    public static async Task<long> CreateMinuteJobAsync(IPostgreSqlConnection connection, string jobName, string sqlCommand)
    {
        var jobId = await connection.ExecuteScalarAsync<long>($@"
            SELECT cron.schedule(
                @jobName,
                '* * * * *',
                @sqlCommand
            )", new { jobName, sqlCommand });

        Console.WriteLine($"Job '{jobName}' créé avec l'ID: {jobId}");
        Console.WriteLine("Planification: Chaque minute");
        return jobId;
    }

    // ========================================================================
    // Gestion des jobs
    // ========================================================================

    /// <summary>
    /// Liste tous les jobs planifiés.
    /// </summary>
    public static async Task ListJobsAsync(IPostgreSqlConnection connection)
    {
        var result = await connection.ExecuteQueryAsync(@"
            SELECT
                jobid,
                schedule,
                command,
                nodename,
                nodeport,
                database,
                username,
                active
            FROM cron.job
            ORDER BY jobid");

        Console.WriteLine("Jobs planifiés:");
        Console.WriteLine(new string('-', 80));

        foreach (System.Data.DataRow row in result.Rows)
        {
            Console.WriteLine($"ID: {row["jobid"]}");
            Console.WriteLine($"  Schedule: {row["schedule"]}");
            Console.WriteLine($"  Database: {row["database"]}");
            Console.WriteLine($"  Active: {row["active"]}");
            Console.WriteLine($"  Command: {row["command"]}");
            Console.WriteLine();
        }
    }

    /// <summary>
    /// Affiche l'historique d'exécution des jobs.
    /// </summary>
    public static async Task ShowJobHistoryAsync(IPostgreSqlConnection connection, int limit = 20)
    {
        var result = await connection.ExecuteQueryAsync($@"
            SELECT
                j.jobid,
                j.command,
                r.runid,
                r.job_pid,
                r.status,
                r.return_message,
                r.start_time,
                r.end_time,
                (r.end_time - r.start_time) AS duration
            FROM cron.job j
            LEFT JOIN cron.job_run_details r ON j.jobid = r.jobid
            ORDER BY r.start_time DESC NULLS LAST
            LIMIT @limit", new { limit });

        Console.WriteLine("Historique d'exécution des jobs:");
        Console.WriteLine(new string('-', 80));

        foreach (System.Data.DataRow row in result.Rows)
        {
            Console.WriteLine($"Job {row["jobid"]} - Run {row["runid"]}");
            Console.WriteLine($"  Status: {row["status"]}");
            Console.WriteLine($"  Start: {row["start_time"]}");
            Console.WriteLine($"  Duration: {row["duration"]}");
            if (row["return_message"] != DBNull.Value)
            {
                Console.WriteLine($"  Message: {row["return_message"]}");
            }
            Console.WriteLine();
        }
    }

    /// <summary>
    /// Désactive un job sans le supprimer.
    /// </summary>
    public static async Task DeactivateJobAsync(IPostgreSqlConnection connection, long jobId)
    {
        await connection.ExecuteNonQueryAsync(
            "UPDATE cron.job SET active = false WHERE jobid = @jobId",
            new { jobId });

        Console.WriteLine($"Job {jobId} désactivé.");
    }

    /// <summary>
    /// Réactive un job désactivé.
    /// </summary>
    public static async Task ActivateJobAsync(IPostgreSqlConnection connection, long jobId)
    {
        await connection.ExecuteNonQueryAsync(
            "UPDATE cron.job SET active = true WHERE jobid = @jobId",
            new { jobId });

        Console.WriteLine($"Job {jobId} réactivé.");
    }

    /// <summary>
    /// Supprime un job par son nom.
    /// </summary>
    public static async Task DeleteJobByNameAsync(IPostgreSqlConnection connection, string jobName)
    {
        await connection.ExecuteNonQueryAsync(
            "SELECT cron.unschedule(@jobName)",
            new { jobName });

        Console.WriteLine($"Job '{jobName}' supprimé.");
    }

    /// <summary>
    /// Supprime un job par son ID.
    /// </summary>
    public static async Task DeleteJobByIdAsync(IPostgreSqlConnection connection, long jobId)
    {
        await connection.ExecuteNonQueryAsync(
            "SELECT cron.unschedule(@jobId)",
            new { jobId });

        Console.WriteLine($"Job {jobId} supprimé.");
    }

    // ========================================================================
    // Patterns de planification courants
    // ========================================================================

    /// <summary>
    /// Exemples de syntaxe cron courantes.
    /// </summary>
    public static void ShowCronExamples()
    {
        Console.WriteLine("Exemples de syntaxe cron pour pg_cron:");
        Console.WriteLine(new string('-', 60));
        Console.WriteLine("* * * * *       - Chaque minute");
        Console.WriteLine("*/5 * * * *     - Toutes les 5 minutes");
        Console.WriteLine("0 * * * *       - Chaque heure (à :00)");
        Console.WriteLine("0 0 * * *       - Chaque jour à minuit");
        Console.WriteLine("0 0 * * 0       - Chaque dimanche à minuit");
        Console.WriteLine("0 0 1 * *       - Le premier de chaque mois");
        Console.WriteLine("0 2 * * 1-5     - À 2h, du lundi au vendredi");
        Console.WriteLine("30 4 1,15 * *   - À 4h30, les 1er et 15 du mois");
        Console.WriteLine();
        Console.WriteLine("Format: minute heure jour_du_mois mois jour_de_la_semaine");
        Console.WriteLine("  - minute: 0-59");
        Console.WriteLine("  - heure: 0-23");
        Console.WriteLine("  - jour_du_mois: 1-31");
        Console.WriteLine("  - mois: 1-12");
        Console.WriteLine("  - jour_de_la_semaine: 0-6 (0 = dimanche)");
    }

    // ========================================================================
    // Jobs avancés
    // ========================================================================

    /// <summary>
    /// Crée un job qui exécute une fonction stockée.
    /// </summary>
    public static async Task<long> CreateFunctionJobAsync(
        IPostgreSqlConnection connection,
        string jobName,
        string schedule,
        string functionName)
    {
        var jobId = await connection.ExecuteScalarAsync<long>($@"
            SELECT cron.schedule(
                @jobName,
                @schedule,
                $$SELECT {functionName}()$$
            )", new { jobName, schedule });

        Console.WriteLine($"Job pour la fonction '{functionName}' créé avec l'ID: {jobId}");
        return jobId;
    }

    /// <summary>
    /// Crée un job avec notification en cas d'erreur.
    /// </summary>
    public static async Task<long> CreateJobWithNotificationAsync(IPostgreSqlConnection connection)
    {
        // D'abord, créer une fonction de notification
        await connection.ExecuteNonQueryAsync(@"
            CREATE OR REPLACE FUNCTION notify_job_failure()
            RETURNS TRIGGER AS $$
            BEGIN
                IF NEW.status = 'failed' THEN
                    PERFORM pg_notify('job_failed',
                        json_build_object(
                            'jobid', NEW.jobid,
                            'runid', NEW.runid,
                            'message', NEW.return_message,
                            'time', NEW.end_time
                        )::text
                    );
                END IF;
                RETURN NEW;
            END;
            $$ LANGUAGE plpgsql");

        // Créer le trigger sur la table d'historique
        await connection.ExecuteNonQueryAsync(@"
            DROP TRIGGER IF EXISTS job_failure_notification ON cron.job_run_details;
            CREATE TRIGGER job_failure_notification
                AFTER INSERT OR UPDATE ON cron.job_run_details
                FOR EACH ROW
                EXECUTE FUNCTION notify_job_failure()");

        Console.WriteLine("Système de notification pour les échecs de jobs configuré.");
        Console.WriteLine("Écoutez le canal 'job_failed' pour recevoir les notifications.");

        return 0;
    }

    // ========================================================================
    // Exemple d'utilisation complète
    // ========================================================================

    /// <summary>
    /// Exemple complet de gestion des tâches planifiées.
    /// </summary>
    public static async Task FullExampleAsync()
    {
        var options = new PostgreSqlConnectionOptions
        {
            Host = "localhost",
            Port = 5432,
            Database = "test_db",
            Username = "postgres",
            Password = "password"
        };

        await using var connection = new PostgreSqlConnection(options);
        await connection.OpenAsync();

        // Vérifier si pg_cron est disponible
        var isAvailable = await IsPgCronAvailableAsync(connection);

        if (!isAvailable)
        {
            Console.WriteLine("pg_cron n'est pas disponible. Fin de l'exemple.");
            return;
        }

        // Afficher les exemples de syntaxe
        ShowCronExamples();

        // Créer quelques jobs
        await CreateLogCleanupJobAsync(connection, 30);
        await CreateStatsUpdateJobAsync(connection);

        // Lister les jobs
        await ListJobsAsync(connection);

        // Afficher l'historique
        await ShowJobHistoryAsync(connection);
    }
}

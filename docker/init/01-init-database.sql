-- ============================================================================
-- NDXPostgreSQL - Script d'initialisation de la base de données
-- ============================================================================
-- Ce script est exécuté automatiquement au premier démarrage du conteneur.
-- Il crée les tables et données de test nécessaires.
--
-- Auteur: Nicolas DEOUX <NDXDev@gmail.com>
-- ============================================================================

-- ============================================================================
-- Extensions utiles
-- ============================================================================

-- Extension pour les UUID
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- Extension pour les fonctions cryptographiques
CREATE EXTENSION IF NOT EXISTS "pgcrypto";

-- ============================================================================
-- Table de test simple pour les tests unitaires
-- ============================================================================
CREATE TABLE IF NOT EXISTS test_table (
    id SERIAL PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    value DECIMAL(10, 2) DEFAULT 0.00,
    is_active BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- Index pour les performances
CREATE INDEX IF NOT EXISTS idx_test_table_name ON test_table(name);
CREATE INDEX IF NOT EXISTS idx_test_table_created_at ON test_table(created_at);

-- Trigger pour mettre à jour updated_at automatiquement
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = CURRENT_TIMESTAMP;
    RETURN NEW;
END;
$$ language 'plpgsql';

DROP TRIGGER IF EXISTS update_test_table_updated_at ON test_table;
CREATE TRIGGER update_test_table_updated_at
    BEFORE UPDATE ON test_table
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

-- ============================================================================
-- Données de test initiales
-- ============================================================================
INSERT INTO test_table (name, value, is_active) VALUES
    ('Test Alpha', 100.50, TRUE),
    ('Test Beta', 200.75, TRUE),
    ('Test Gamma', 300.00, FALSE),
    ('Test Delta', 450.25, TRUE)
ON CONFLICT DO NOTHING;

-- ============================================================================
-- Table pour tests de transactions
-- ============================================================================
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'transaction_status') THEN
        CREATE TYPE transaction_status AS ENUM ('pending', 'completed', 'cancelled');
    END IF;
END$$;

CREATE TABLE IF NOT EXISTS transaction_test (
    id SERIAL PRIMARY KEY,
    operation VARCHAR(50) NOT NULL,
    amount DECIMAL(10, 2) NOT NULL,
    status VARCHAR(20) DEFAULT 'pending' CHECK (status IN ('pending', 'completed', 'cancelled')),
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- ============================================================================
-- Table pour tests JSONB
-- ============================================================================
CREATE TABLE IF NOT EXISTS jsonb_test (
    id SERIAL PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    metadata JSONB DEFAULT '{}',
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- Index GIN pour les requêtes JSONB
CREATE INDEX IF NOT EXISTS idx_jsonb_test_metadata ON jsonb_test USING GIN (metadata);

-- Données de test JSONB
INSERT INTO jsonb_test (name, metadata) VALUES
    ('User Admin', '{"role": "admin", "permissions": ["read", "write", "delete"], "preferences": {"theme": "dark"}}'),
    ('User Standard', '{"role": "user", "permissions": ["read"], "preferences": {"theme": "light"}}')
ON CONFLICT DO NOTHING;

-- ============================================================================
-- Fonction stockée de test
-- ============================================================================
CREATE OR REPLACE FUNCTION fn_get_active_items()
RETURNS TABLE (
    id INT,
    name VARCHAR(100),
    value DECIMAL(10, 2),
    created_at TIMESTAMP WITH TIME ZONE
) AS $$
BEGIN
    RETURN QUERY
    SELECT t.id, t.name, t.value, t.created_at
    FROM test_table t
    WHERE t.is_active = TRUE
    ORDER BY t.name;
END;
$$ LANGUAGE plpgsql;

-- ============================================================================
-- Fonction avec paramètres OUT
-- ============================================================================
CREATE OR REPLACE FUNCTION fn_add_item(
    p_name VARCHAR(100),
    p_value DECIMAL(10, 2),
    OUT p_id INT
) AS $$
BEGIN
    INSERT INTO test_table (name, value)
    VALUES (p_name, p_value)
    RETURNING id INTO p_id;
END;
$$ LANGUAGE plpgsql;

-- ============================================================================
-- Procédure stockée (PostgreSQL 11+)
-- ============================================================================
CREATE OR REPLACE PROCEDURE sp_update_item_value(
    p_id INT,
    p_new_value DECIMAL(10, 2)
)
LANGUAGE plpgsql
AS $$
BEGIN
    UPDATE test_table
    SET value = p_new_value, updated_at = CURRENT_TIMESTAMP
    WHERE id = p_id;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'Item with ID % not found', p_id;
    END IF;
END;
$$;

-- ============================================================================
-- Vue de test
-- ============================================================================
CREATE OR REPLACE VIEW v_active_items AS
SELECT id, name, value, created_at
FROM test_table
WHERE is_active = TRUE;

-- ============================================================================
-- Table pour tests de performances avec beaucoup de données
-- ============================================================================
CREATE TABLE IF NOT EXISTS large_test_table (
    id SERIAL PRIMARY KEY,
    uuid UUID DEFAULT uuid_generate_v4(),
    name VARCHAR(255) NOT NULL,
    description TEXT,
    value DECIMAL(15, 4),
    category VARCHAR(50),
    tags TEXT[],
    metadata JSONB,
    is_active BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- Index pour les tests de performances
CREATE INDEX IF NOT EXISTS idx_large_test_category ON large_test_table(category);
CREATE INDEX IF NOT EXISTS idx_large_test_tags ON large_test_table USING GIN(tags);
CREATE INDEX IF NOT EXISTS idx_large_test_metadata ON large_test_table USING GIN(metadata);

-- ============================================================================
-- Fonction pour générer des données de test en masse
-- ============================================================================
CREATE OR REPLACE FUNCTION fn_generate_test_data(num_rows INT)
RETURNS INT AS $$
DECLARE
    i INT;
    categories TEXT[] := ARRAY['Electronics', 'Books', 'Clothing', 'Food', 'Sports'];
BEGIN
    FOR i IN 1..num_rows LOOP
        INSERT INTO large_test_table (name, description, value, category, tags, metadata)
        VALUES (
            'Item ' || i,
            'Description for item ' || i,
            (random() * 1000)::DECIMAL(15, 4),
            categories[1 + (random() * 4)::INT],
            ARRAY['tag' || (random() * 10)::INT, 'tag' || (random() * 20)::INT],
            jsonb_build_object('index', i, 'random', random(), 'timestamp', CURRENT_TIMESTAMP)
        );
    END LOOP;

    RETURN num_rows;
END;
$$ LANGUAGE plpgsql;

-- ============================================================================
-- Affichage de confirmation
-- ============================================================================
DO $$
BEGIN
    RAISE NOTICE 'Base de données NDXPostgreSQL initialisée avec succès!';
END $$;

SELECT 'Base de données NDXPostgreSQL initialisée avec succès!' AS message;
SELECT COUNT(*) AS total_test_items FROM test_table;

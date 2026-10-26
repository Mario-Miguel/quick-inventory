-- Usuario con el que se conecta la aplicación en los equipos del museo.
-- Puede leer y escribir datos, pero no cambiar el esquema: las migraciones se aplican
-- con el usuario propietario (ver README), que no se instala en los equipos.
--
-- Ejecutar una vez, conectado a la base de la app con el usuario propietario, por ejemplo:
--   psql "host=... dbname=museo user=propietario sslmode=verify-full" -v password="'...'" -f usuario-app.sql
-- Volver a ejecutarlo no hace daño: solo repite los permisos.

DO $$
BEGIN
    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'museo_app') THEN
        CREATE ROLE museo_app LOGIN;
    END IF;
END
$$;

ALTER ROLE museo_app PASSWORD :password;

-- La base en la que se ejecuta el script, se llame como se llame.
DO $$
BEGIN
    EXECUTE format('GRANT CONNECT ON DATABASE %I TO museo_app', current_database());
END
$$;
GRANT USAGE ON SCHEMA public TO museo_app;

-- Las tablas y secuencias que ya existen...
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO museo_app;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO museo_app;

-- ...y las que creen las migraciones futuras (las crea el propietario, que es quien ejecuta esto).
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO museo_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT USAGE, SELECT ON SEQUENCES TO museo_app;

-- La aplicación solo necesita saber qué migraciones hay aplicadas, no cambiarlas.
REVOKE INSERT, UPDATE, DELETE ON "__EFMigrationsHistory" FROM museo_app;

-- Cuando existan los registros de VERI*FACTU (fase 2), se añadirá aquí:
-- REVOKE UPDATE, DELETE ON "VerifactuRecords" FROM museo_app;

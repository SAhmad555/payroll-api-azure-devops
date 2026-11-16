-- One-time step after the first infrastructure deployment.
-- Run against the payroll database while signed in as a member of the SQL Entra admin group, e.g.
--   sqlcmd -S <sqlServerFqdn> -d sqldb-payroll -G -i infra/scripts/grant-sql-access.sql -v IdentityName="id-payroll-dev"
--
-- Creates a contained database user for the App Service's user-assigned managed identity.
-- db_ddladmin is only needed because the demo creates its schema on startup (EnsureCreated);
-- with a migrations-bundle pipeline step you would grant only reader/writer to the app.

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'$(IdentityName)')
BEGIN
    CREATE USER [$(IdentityName)] FROM EXTERNAL PROVIDER;
END;

ALTER ROLE db_datareader ADD MEMBER [$(IdentityName)];
ALTER ROLE db_datawriter ADD MEMBER [$(IdentityName)];
ALTER ROLE db_ddladmin  ADD MEMBER [$(IdentityName)];

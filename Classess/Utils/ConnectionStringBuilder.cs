using Npgsql;
using Oracle.ManagedDataAccess.Client;

namespace ARS.Classess.Utils
{
    /// <summary>
    /// Handles building and testing connection strings for PostgreSQL and Oracle databases.
    /// Passwords are only used transiently to build connection strings and are never persisted.
    /// </summary>
    public static class ConnectionStringBuilder
    {
        /// <summary>
        /// Builds a plain connection string from the provided parameters.
        /// Password is included in the returned string but is never stored in the database.
        /// </summary>
        public static string BuildPlainConnectionString(
            string databaseType,
            string host,
            int port,
            string databaseName,
            string username,
            string password)
        {
            if (string.IsNullOrWhiteSpace(password))
                throw new ArgumentException("Password is required to build the connection string.", nameof(password));

            return databaseType.ToLowerInvariant() switch
            {
                "postgresql" or "postgres" =>
                    $"Host={host};Port={port};Database={databaseName};Username={username};Password={password};SSL Mode=Prefer;Trust Server Certificate=true;",

                "oracle" =>
                    $"User Id={username};Password={password};Data Source={host}:{port}/{databaseName};",

                _ => throw new NotSupportedException($"Unsupported database type: '{databaseType}'. Supported types are PostgreSQL and Oracle.")
            };
        }

        /// <summary>
        /// Builds an encrypted connection string from the provided parameters.
        /// The password is embedded in the connection string and then the entire
        /// string is encrypted. The password itself is never stored separately.
        /// </summary>
        public static string BuildEncryptedConnectionString(
            string databaseType,
            string host,
            int port,
            string databaseName,
            string username,
            string password)
        {
            var plain = BuildPlainConnectionString(databaseType, host, port, databaseName, username, password);
            return Cryptor.Encrypt(plain, useHashing: true);
        }

        /// <summary>
        /// Decrypts an encrypted connection string for use at runtime.
        /// </summary>
        public static string DecryptConnectionString(string encryptedConnectionString)
        {
            return Cryptor.Decrypt(encryptedConnectionString, useHashing: true);
        }

        /// <summary>
        /// Tests a database connection using the provided parameters.
        /// Opens and immediately closes a connection to validate connectivity.
        /// </summary>
        public static async Task<(bool Success, string? Error)> TestConnectionAsync(
            string databaseType,
            string host,
            int port,
            string databaseName,
            string username,
            string password)
        {
            try
            {
                var connectionString = BuildPlainConnectionString(databaseType, host, port, databaseName, username, password);

                return databaseType.ToLowerInvariant() switch
                {
                    "postgresql" or "postgres" => await TestPostgresAsync(connectionString),
                    "oracle" => await TestOracleAsync(connectionString),
                    _ => (false, $"Unsupported database type: '{databaseType}'.")
                };
            }
            catch (Exception ex)
            {
                return (false, $"Connection test failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Tests a connection using an already-encrypted connection string.
        /// Decrypts the string first, then tests connectivity.
        /// </summary>
        public static async Task<(bool Success, string? Error)> TestEncryptedConnectionAsync(string encryptedConnectionString)
        {
            try
            {
                var plainConnectionString = DecryptConnectionString(encryptedConnectionString);

                // Infer database type from connection string pattern
                var dbType = InferDatabaseType(plainConnectionString);

                return dbType.ToLowerInvariant() switch
                {
                    "postgresql" or "postgres" => await TestPostgresAsync(plainConnectionString),
                    "oracle" => await TestOracleAsync(plainConnectionString),
                    _ => (false, "Could not determine database type from connection string.")
                };
            }
            catch (Exception ex)
            {
                return (false, $"Connection test failed: {ex.Message}");
            }
        }

        private static async Task<(bool Success, string? Error)> TestPostgresAsync(string connectionString)
        {
            try
            {
                await using var conn = new NpgsqlConnection(connectionString);
                await conn.OpenAsync();
                await using var cmd = new NpgsqlCommand("SELECT 1", conn);
                await cmd.ExecuteScalarAsync();
                await conn.CloseAsync();
                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, $"PostgreSQL connection failed: {ex.Message}");
            }
        }

        private static async Task<(bool Success, string? Error)> TestOracleAsync(string connectionString)
        {
            try
            {
                await using var conn = new OracleConnection(connectionString);
                await conn.OpenAsync();
                await using var cmd = new OracleCommand("SELECT 1 FROM DUAL", conn);
                await cmd.ExecuteScalarAsync();
                await conn.CloseAsync();
                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, $"Oracle connection failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Attempts to infer the database type from a plain connection string.
        /// </summary>
        private static string InferDatabaseType(string connectionString)
        {
            if (connectionString.Contains("Host=", StringComparison.OrdinalIgnoreCase))
                return "postgresql";
            if (connectionString.Contains("Data Source=", StringComparison.OrdinalIgnoreCase))
                return "oracle";
            return "unknown";
        }
    }
}

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace Prometheus
{
    public static class SysProc
    {
        private static string? _connectionString;

        /// <summary>
        /// Optional: admin email to notify on errors. Set at startup if desired.
        /// </summary>
        public static string AdminEmail { get; set; } = "admin@yourdomain.com";

        public static void SetConnectionString(string connectionString)
        {
            // Normalize connection string ONCE at startup to ensure pooling works
            // Connection pooling requires EXACT string match - any variation prevents pooling
            var connStr = connectionString;
            if (!connStr.Contains("Pooling=", StringComparison.OrdinalIgnoreCase))
            {
                // Add pooling parameters if not present
                var separator = connStr.EndsWith(";") ? "" : ";";
                connStr = $"{connStr}{separator}Pooling=true;Max Pool Size=200;Min Pool Size=0;";
            }
            else if (!connStr.Contains("Max Pool Size=", StringComparison.OrdinalIgnoreCase))
            {
                // Pooling is enabled but no max pool size - add it
                var separator = connStr.EndsWith(";") ? "" : ";";
                connStr = $"{connStr}{separator}Max Pool Size=200;Min Pool Size=0;";
            }

            _connectionString = connStr; // Store normalized version
        }

        public static string GetConnectionString()
        {
            if (string.IsNullOrWhiteSpace(_connectionString))
                throw new InvalidOperationException("Connection string not set. Call SetConnectionString() at startup.");

            // Return the normalized connection string (pooling parameters added at SetConnectionString time)
            // This ensures the connection string is ALWAYS the same, allowing pooling to work
            return _connectionString;
        }

        /// <summary>
        /// Executes embedded SQL with optional logging.
        /// Returns rows affected, or new ID if SQL includes SCOPE_IDENTITY().
        /// All user inputs should be previously sanitized.
        /// Set logOperation=true only for significant business events that need audit trail.
        /// </summary>
        public static int SysDbExecute(string pSQL, string pUser, SqlConnection? pConn = null, bool logOperation = false)
        {
            int tResult = 0;
            bool tShouldClose = false;
            string tLogMessage = "";
            var tConn = pConn ?? new SqlConnection(GetConnectionString());

            try
            {
                if (tConn.State != ConnectionState.Open)
                {
                    tConn.Open();
                    tShouldClose = true;
                }
                using var cmd = new SqlCommand(pSQL, tConn);
                if (pSQL.Trim().ToUpper().Contains("SCOPE_IDENTITY()"))
                {
                    object? scalarResult = cmd.ExecuteScalar();
                    if (scalarResult != null && int.TryParse(scalarResult.ToString(), out int newId))
                        tResult = newId;
                    if (logOperation)
                        tLogMessage = $"New ID={tResult}: {pSQL}";
                }
                else
                {
                    // Execute non-query for UPDATE/DELETE/other operations
                    tResult = cmd.ExecuteNonQuery();
                    if (logOperation)
                        tLogMessage = $"RowsAffected={tResult}: {pSQL}";
                }

                // Only log if explicitly requested - routine operations don't need audit trail
                // Errors are always logged via async logging below
                if (logOperation && !string.IsNullOrEmpty(tLogMessage))
                {
                    _ = SysLogItAsync(tLogMessage, pUser);
                }
            }
            catch (Exception ex)
            {
                // Always log errors - these are significant events (fire-and-forget)
                _ = SysLogItAsync($"ERROR: SQL Error: {pSQL} | Exception: {ex.Message}", pUser);
                SysSendAdminAlert($"SQL Error: {pSQL} | Exception: {ex.Message}", pUser);
                return -1; // Return -1 on error instead of throwing
            }
            finally
            {
                if (tShouldClose)
                    tConn.Close();
            }
            return tResult;
        }


        /// <summary>
        /// [OBSOLETE] Use SysLogItAsync instead. This synchronous version is deprecated and will be removed.
        /// </summary>
        [Obsolete("Use SysLogItAsync instead. This synchronous version blocks the calling thread.")]
        public static void SysLogIt(string pMessage, string pUser, SqlConnection? pConn = null)
        {
            // If a connection is provided, use it (caller manages lifecycle)
            if (pConn != null)
            {
                try
                {
                    string tSQL = @"INSERT INTO SysLog (LogUser, LogTime, LogData) VALUES (@user, GETDATE(), @logData)";
                    using var cmd = new SqlCommand(tSQL, pConn);
                    cmd.Parameters.AddWithValue("@user", pUser);
                    cmd.Parameters.AddWithValue("@logData", pMessage);
                    cmd.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    // Log to console only - don't fail the application if logging fails
                    Console.WriteLine($"[SYSLOG ERROR] Failed to log: {ex.Message}");
                }
                return;
            }

            // For new connections, add retry logic for transient failures
            const int maxRetries = 2;
            for (int attempt = 0; attempt <= maxRetries; attempt++)
            {
                SqlConnection? tConn = null;
                try
                {
                    tConn = new SqlConnection(GetConnectionString());
                    tConn.Open();

                    string tSQL = @"INSERT INTO SysLog (LogUser, LogTime, LogData) VALUES (@user, GETDATE(), @logData)";
                    using var cmd = new SqlCommand(tSQL, tConn);
                    cmd.Parameters.AddWithValue("@user", pUser);
                    cmd.Parameters.AddWithValue("@logData", pMessage);
                    cmd.ExecuteNonQuery();

                    // Success - exit retry loop
                    return;
                }
                catch (SqlException sqlEx) when (attempt < maxRetries && IsTransientError(sqlEx))
                {
                    // Transient error - retry after brief delay
                    if (tConn != null)
                    {
                        try { tConn.Close(); } catch { }
                        try { tConn.Dispose(); } catch { }
                    }
                    System.Threading.Thread.Sleep(100 * (attempt + 1)); // Exponential backoff: 100ms, 200ms
                    continue;
                }
                catch (Exception ex)
                {
                    // Non-transient error or final attempt failed
                    if (tConn != null)
                    {
                        try { tConn.Close(); } catch { }
                        try { tConn.Dispose(); } catch { }
                    }

                    // Only log to console on final attempt to avoid spam
                    if (attempt == maxRetries)
                    {
                        Console.WriteLine($"[SYSLOG ERROR] Failed to log after {maxRetries + 1} attempts: {ex.Message}");
                    }
                    return; // Don't retry non-transient errors
                }
            }
        }

        /// <summary>
        /// Async version of SysLogIt - fire-and-forget logging that doesn't block the caller
        /// </summary>
        public static async Task SysLogItAsync(string pMessage, string pUser)
        {
            // For new connections, add retry logic for transient failures
            const int maxRetries = 2;
            for (int attempt = 0; attempt <= maxRetries; attempt++)
            {
                SqlConnection? tConn = null;
                try
                {
                    tConn = new SqlConnection(GetConnectionString());
                    await tConn.OpenAsync();

                    string tSQL = @"INSERT INTO SysLog (LogUser, LogTime, LogData) VALUES (@user, GETDATE(), @logData)";
                    using var cmd = new SqlCommand(tSQL, tConn);
                    cmd.Parameters.AddWithValue("@user", pUser);
                    cmd.Parameters.AddWithValue("@logData", pMessage);
                    await cmd.ExecuteNonQueryAsync();

                    // Success - exit retry loop
                    return;
                }
                catch (SqlException sqlEx) when (attempt < maxRetries && IsTransientError(sqlEx))
                {
                    // Transient error - retry after brief delay
                    if (tConn != null)
                    {
                        try { tConn.Close(); } catch { }
                        try { tConn.Dispose(); } catch { }
                    }
                    await Task.Delay(100 * (attempt + 1)); // Exponential backoff: 100ms, 200ms
                    continue;
                }
                catch (Exception ex)
                {
                    // Non-transient error or final attempt failed
                    if (tConn != null)
                    {
                        try { tConn.Close(); } catch { }
                        try { tConn.Dispose(); } catch { }
                    }

                    // Only log to console on final attempt to avoid spam
                    if (attempt == maxRetries)
                    {
                        Console.WriteLine($"[SYSLOG ERROR] Failed to log after {maxRetries + 1} attempts: {ex.Message}");
                    }
                    return; // Don't retry non-transient errors
                }
            }
        }

        /// <summary>
        /// Check if a SQL exception is a transient error that might succeed on retry
        /// </summary>
        private static bool IsTransientError(SqlException ex)
        {
            // SQL Server error codes for transient errors
            // -2: Timeout
            // 20: Instance failure
            // 64: Connection failure
            // 233: Connection initialization error
            // 10053: Transport-level error
            // 10054: Connection forcibly closed
            // 10060: Network error
            var transientErrorCodes = new[] { -2, 20, 64, 233, 10053, 10054, 10060 };
            return transientErrorCodes.Contains(ex.Number);
        }

        private static void SysSendAdminAlert(string pMessage, string pUser)
        {
            if (string.IsNullOrWhiteSpace(AdminEmail))
                return;

            try
            {
                var mail = new MailMessage
                {
                    From = new MailAddress("noreply@yourdomain.com"),
                    Subject = $"[Prometheus] ERROR reported by {pUser}",
                    Body = pMessage
                };

                mail.To.Add(AdminEmail);

                using var smtp = new SmtpClient("your.smtp.server");
                smtp.Send(mail);
            }
            catch (Exception)
            {
                // Console.WriteLine($"Failed to send admin alert: {ex.Message}");
            }
        }

        /// <summary>
        /// Executes SQL and returns a SqlDataReader. Caller must dispose reader.
        /// </summary>
        public static SqlDataReader SysGetReader(string pSQL, SqlConnection? pConn = null)
        {
            var tConn = pConn ?? new SqlConnection(GetConnectionString());

            if (tConn.State != ConnectionState.Open)
                tConn.Open();

            var cmd = new SqlCommand(pSQL, tConn);
            return cmd.ExecuteReader(CommandBehavior.CloseConnection);
        }

        /// <summary>
        /// Enhanced input validation and sanitization for security
        /// </summary>
        public static class SecurityValidation
        {
            /// <summary>
            /// Validates and sanitizes text input with length limits
            /// </summary>
            public static string ValidateText(string? input, int maxLength = 255, string fieldName = "text")
            {
                if (string.IsNullOrWhiteSpace(input))
                    return string.Empty;

                if (input.Length > maxLength)
                    throw new ArgumentException($"{fieldName} cannot exceed {maxLength} characters.");

                // Remove null characters and control characters
                input = new string(input.Where(c => c >= 32 || c == 9 || c == 10 || c == 13).ToArray());

                return input.Trim();
            }

            /// <summary>
            /// Validates email format
            /// </summary>
            public static string ValidateEmail(string? input, int maxLength = 200)
            {
                if (string.IsNullOrWhiteSpace(input))
                    return string.Empty;

                var sanitized = ValidateText(input, maxLength, "email");

                if (!string.IsNullOrEmpty(sanitized) && !IsValidEmail(sanitized))
                    throw new ArgumentException("Invalid email format.");

                return sanitized;
            }

            /// <summary>
            /// Validates status field (Y/N only)
            /// </summary>
            public static char ValidateStatus(string? input)
            {
                if (string.IsNullOrWhiteSpace(input))
                    return 'Y';

                var status = input.Trim().ToUpper();
                if (status != "Y" && status != "N")
                    throw new ArgumentException("Status must be 'Y' or 'N'.");

                return status[0];
            }

            /// <summary>
            /// Validates integer ID
            /// </summary>
            public static int ValidateId(int input, string fieldName = "ID")
            {
                if (input < 0)
                    throw new ArgumentException($"{fieldName} cannot be negative.");

                return input;
            }

            /// <summary>
            /// Validates hospital ID with tenant context
            /// </summary>
            public static int ValidateHospitalId(int hospitalId, int currentUserHospitalId)
            {
                if (hospitalId <= 0)
                    return currentUserHospitalId;

                if (hospitalId != currentUserHospitalId)
                    throw new ArgumentException("Access denied: Hospital ID mismatch.");

                return hospitalId;
            }

            /// <summary>
            /// Validates and sanitizes for SQL injection prevention
            /// </summary>
            public static string ValidateSqlSafe(string? input, int maxLength = 255, string fieldName = "text")
            {
                var sanitized = ValidateText(input, maxLength, fieldName);

                // Additional SQL injection prevention
                if (sanitized.Contains("--") || sanitized.Contains("/*") || sanitized.Contains("*/"))
                    throw new ArgumentException($"{fieldName} contains invalid characters.");

                return sanitized;
            }

            /// <summary>
            /// Validates HTML content for XSS prevention
            /// </summary>
            public static string ValidateHtmlSafe(string? input, int maxLength = 1000, string fieldName = "HTML")
            {
                var sanitized = ValidateText(input, maxLength, fieldName);

                // Basic XSS prevention - remove script tags and dangerous attributes
                sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, @"<script[^>]*>.*?</script>", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);
                sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, @"javascript:", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, @"on\w+\s*=", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                return sanitized;
            }

            /// <summary>
            /// Simple email validation
            /// </summary>
            private static bool IsValidEmail(string email)
            {
                try
                {
                    var addr = new System.Net.Mail.MailAddress(email);
                    return addr.Address == email;
                }
                catch
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// Legacy sanitization - DEPRECATED: Use SecurityValidation instead
        /// </summary>
        public static string SysSanitize(string pInput)
        {
            if (string.IsNullOrWhiteSpace(pInput))
                return string.Empty;

            return pInput
                .Replace("<", ((char)8249).ToString())   // ‹ single left-pointing angle quote
                .Replace(">", ((char)8250).ToString())   // › single right-pointing angle quote
                .Replace("&", ((char)65286).ToString())  // ＆ full-width ampersand
                .Replace("'", ((char)8217).ToString())   // ' curly single quote
                .Replace("\"", ((char)8221).ToString())  // " curly double quote
                .Replace(";", ((char)65307).ToString())  // ； full-width semicolon
                .Replace("%", ((char)65285).ToString())  // ％ full-width percent
                .Replace("_", ((char)65343).ToString())  // ＿ full-width underscore
                .Replace("=", ((char)65309).ToString())  // ＝ full-width equals sign
                .Replace("/", ((char)65295).ToString())  // ／ full-width solidus
                .Replace("\\", ((char)65292).ToString()) // ＼ full-width backslash
                .Replace("\t", "    ")                   // tab to 4 spaces
                .Trim();
        }


        public static string SysEncodeHTML(string? pInput)
        {
            if (string.IsNullOrEmpty(pInput))
                return string.Empty;

            return pInput
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&#39;")
                .Replace("/", "&#x2F;");
        }



        /// <summary>
        /// Executes SQL and returns single scalar as string, or defaultValue if null/DBNull.
        /// </summary>
        public static string SysGetScalar(string pSQL, SqlConnection? pConn = null, string pDefaultValue = "")
        {
            bool tShouldClose = false;
            var tConn = pConn ?? new SqlConnection(GetConnectionString());

            try
            {
                if (tConn.State != ConnectionState.Open)
                {
                    tConn.Open();
                    tShouldClose = true;
                }

                using var cmd = new SqlCommand(pSQL, tConn);
                var result = cmd.ExecuteScalar();

                if (result == null || result == DBNull.Value) return pDefaultValue;

                return result.ToString() ?? pDefaultValue;
            }
            finally
            {
                if (tShouldClose)
                    tConn.Close();
            }
        }

        // TagEvent-specific data access moved to Features.TagEventRepository to keep SysProc portable.
    }
}

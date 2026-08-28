using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using KaijensonIventory_SalesMotorShopWeb.Data;
using Microsoft.Extensions.Options;
using KaijensonIventory_SalesMotorShopWeb.Models;
using Microsoft.Extensions.Logging;

namespace KaijensonIventory_SalesMotorShopWeb.Services
{
    public class BackupService : IBackupService
    {
        private const int DescriptionMaxLength = 500;
        private readonly ApplicationDbContext _context;
        private readonly IActivityLogService _activityLog;
        private readonly string _connectionString;
        private readonly IOptionsMonitor<BackupSettings> _backupSettings;
        private readonly Microsoft.Extensions.Hosting.IHostEnvironment _env;
        private readonly ILogger<BackupService> _logger;

        public BackupService(ApplicationDbContext context, IActivityLogService activityLog, Microsoft.Extensions.Hosting.IHostEnvironment env, IOptionsMonitor<BackupSettings> backupSettings, ILogger<BackupService> logger)
        {
            _context = context;
            _activityLog = activityLog;
            _env = env;
            _backupSettings = backupSettings;
            _connectionString = context.Database.GetDbConnection().ConnectionString;
            _logger = logger;
        }

        private string GetDatabaseName()
        {
            var builder = new SqlConnectionStringBuilder(_connectionString);
            return builder.InitialCatalog;
        }

        private string GenerateBackupFileName(string? suffix = null)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
            var dbName = GetDatabaseName();
            var safeDb = dbName.Replace(" ", "_");
            var extra = suffix != null ? $"_{suffix}" : "";
            return $"{safeDb}_{timestamp}{extra}.bak";
        }

        private string GetBackupRoot()
        {
            var custom = _backupSettings.CurrentValue.BackupDirectory;
            if (!string.IsNullOrWhiteSpace(custom))
            {
                var resolvedPath = Path.IsPathRooted(custom)
                    ? Path.GetFullPath(custom)
                    : Path.GetFullPath(Path.Combine(_env.ContentRootPath, custom));
                Directory.CreateDirectory(resolvedPath);
                return resolvedPath;
            }
            // Fallback to a directory that SQL Server service account typically has access to
            var fallback = Path.Combine("C:\\Backup", "Kaijenson");
            try
            {
                Directory.CreateDirectory(fallback);
            }
            catch (Exception ex)
            {
                // Log and fallback to content root if creation fails
                _logger?.LogError(ex, "Failed to create backup directory at {Path}, falling back to content root.", fallback);
                fallback = Path.Combine(_env.ContentRootPath, "App_Data", "Backups");
                Directory.CreateDirectory(fallback);
            }
            return fallback;
        }

        private string GetBackupFilePath(string fileName) => Path.Combine(GetBackupRoot(), fileName);

        public async Task<DatabaseBackup> CreateBackupAsync(int staffId)
        {
            var dbName = GetDatabaseName();
            var fileName = GenerateBackupFileName();
            var filePath = GetBackupFilePath(fileName);
            var sql = $"BACKUP DATABASE [{dbName}] TO DISK = N'{filePath}' WITH INIT, STATS = 10";
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                using var cmd = new SqlCommand(sql, conn);
                await cmd.ExecuteNonQueryAsync();

                bool fileExists = File.Exists(filePath);
                long fileSize = 0;
                string status = "Failed";
                if (fileExists)
                {
                    var info = new FileInfo(filePath);
                    fileSize = info.Length;
                    if (fileSize > 0) status = "Successful";
                }
                _logger.LogInformation("SQL Backup executed on instance {DataSource}, database {Database}. File exists: {Exists}, size: {Size}", new SqlConnectionStringBuilder(_connectionString).DataSource, dbName, fileExists, fileSize);

                var backup = new DatabaseBackup
                {
                    FileName = fileName,
                    FilePath = filePath,
                    FileSize = fileSize,
                    CreatedAt = DateTime.Now,
                    CreatedBy = staffId,
                    BackupType = "Manual",
                    Status = status,
                    Description = null
                };
                _context.DatabaseBackups.Add(backup);
                await _context.SaveChangesAsync();
                await _activityLog.LogAsync("Create Database Backup", "System", $"Backup ID: {backup.BackupId}, Status: {status}", staffId);
                return backup;
            }
            catch (Exception ex)
            {
                string errorDesc = ex.Message;
                if (ex is SqlException sqlEx)
                {
                    errorDesc = $"SQL Error {sqlEx.Number}: {sqlEx.Message} (Database: {dbName}, Instance: {new SqlConnectionStringBuilder(_connectionString).DataSource}, Path: {filePath})";
                }
                await _activityLog.LogAsync("Create Database Backup", "System", $"Failed: {errorDesc}", staffId);
                var failed = new DatabaseBackup
                {
                    FileName = fileName,
                    FilePath = filePath,
                    FileSize = 0,
                    CreatedAt = DateTime.Now,
                    CreatedBy = staffId,
                    BackupType = "Manual",
                    Status = "Failed",
                    Description = errorDesc.Length > DescriptionMaxLength ? errorDesc.Substring(0, DescriptionMaxLength) : errorDesc
                };
                _context.DatabaseBackups.Add(failed);
                await _context.SaveChangesAsync();
                return failed;
            }
        }

        public async Task<DatabaseBackup> CreateAutomaticBackupAsync()
        {
            var dbName = GetDatabaseName();
            var fileName = GenerateBackupFileName("Automatic");
            var filePath = GetBackupFilePath(fileName);
            var sql = $"BACKUP DATABASE [{dbName}] TO DISK = N'{filePath}' WITH INIT, STATS = 10";
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                using var cmd = new SqlCommand(sql, conn);
                await cmd.ExecuteNonQueryAsync();

                bool fileExists = File.Exists(filePath);
                long fileSize = 0;
                string status = "Failed";
                if (fileExists)
                {
                    var info = new FileInfo(filePath);
                    fileSize = info.Length;
                    if (fileSize > 0) status = "Successful";
                }
                var backup = new DatabaseBackup
                {
                    FileName = fileName,
                    FilePath = filePath,
                    FileSize = fileSize,
                    CreatedAt = DateTime.Now,
                    CreatedBy = null,
                    BackupType = "Automatic",
                    Status = status,
                    Description = null
                };
                _context.DatabaseBackups.Add(backup);
                await _context.SaveChangesAsync();
                return backup;
            }
            catch (Exception ex)
            {
                var backup = new DatabaseBackup
                {
                    FileName = fileName,
                    FilePath = filePath,
                    FileSize = 0,
                    CreatedAt = DateTime.Now,
                    CreatedBy = null,
                    BackupType = "Automatic",
                    Status = "Failed",
                    Description = ex.Message
                };
                _context.DatabaseBackups.Add(backup);
                await _context.SaveChangesAsync();
                return backup;
            }
        }

        public async Task<DatabaseBackup> CreatePreRestoreBackupAsync(int staffId)
        {
            var dbName = GetDatabaseName();
            var fileName = GenerateBackupFileName("PreRestore");
            var filePath = GetBackupFilePath(fileName);
            var sql = $"BACKUP DATABASE [{dbName}] TO DISK = N'{filePath}' WITH INIT, STATS = 10";
            DatabaseBackup? backup = null;
            bool backupSaved = false;
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                using var cmd = new SqlCommand(sql, conn);
                await cmd.ExecuteNonQueryAsync();

                bool fileExists = File.Exists(filePath);
                long fileSize = 0;
                string status = "Failed";
                string description = "Safety backup failed to create";
                if (fileExists)
                {
                    var info = new FileInfo(filePath);
                    fileSize = info.Length;
                    if (fileSize > 0)
                    {
                        status = "Successful";
                        description = "Safety backup created before restore";
                    }
                }

                backup = new DatabaseBackup
                {
                    FileName = fileName,
                    FilePath = filePath,
                    FileSize = fileSize,
                    CreatedAt = DateTime.Now,
                    CreatedBy = staffId,
                    BackupType = "Pre-Restore Safety Backup",
                    Status = status,
                    Description = description
                };
                _context.DatabaseBackups.Add(backup);
                await _context.SaveChangesAsync();
                backupSaved = true;

                await _activityLog.LogAsync("Create Pre-Restore Backup", "System", $"Backup ID: {backup.BackupId}, Status: {status}", staffId);

                if (status != "Successful")
                    throw new InvalidOperationException("Pre‑restore safety backup failed.");

                return backup;
            }
            catch (Exception ex)
            {
                // If backup not yet saved, create failed record and log
                if (!backupSaved)
                {
                    var failed = new DatabaseBackup
                    {
                        FileName = fileName,
                        FilePath = filePath,
                        FileSize = 0,
                        CreatedAt = DateTime.Now,
                        CreatedBy = staffId,
                        BackupType = "Pre-Restore Safety Backup",
                        Status = "Failed",
Description = ex.Message.Length > DescriptionMaxLength ? ex.Message.Substring(0, DescriptionMaxLength) : ex.Message
                    };
                    _context.DatabaseBackups.Add(failed);
                    await _context.SaveChangesAsync();
                    await _activityLog.LogAsync("Create Pre-Restore Backup", "System", $"Failed: {ex.Message}", staffId);
                }
                else
                {
                    // backup already logged; just rethrow
                }
                throw;
            }
        }

        public async Task<DatabaseBackup> GetBackupAsync(int backupId)
        {
            return await _context.DatabaseBackups.FindAsync(backupId) ?? null!;
        }

        public async Task<List<DatabaseBackup>> GetBackupHistoryAsync()
        {
            return await _context.DatabaseBackups
                .Include(b => b.CreatedByStaff)
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync();
        }

        public async Task<bool> ValidateBackupAsync(int backupId)
        {
            var backup = await GetBackupAsync(backupId);
            if (backup == null || backup.Status != "Successful" || !File.Exists(backup.FilePath))
                return false;
            var fileInfo = new FileInfo(backup.FilePath);
            if (fileInfo.Length == 0)
            {
                backup.Status = "Invalid";
                await _context.SaveChangesAsync();
                return false;
            }
            var expectedDbName = GetDatabaseName();
            var sqlHeader = $"RESTORE HEADERONLY FROM DISK = N'{backup.FilePath}'";
            var sqlVerify = $"RESTORE VERIFYONLY FROM DISK = N'{backup.FilePath}'";
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                using var cmdHeader = new SqlCommand(sqlHeader, conn);
                using var reader = await cmdHeader.ExecuteReaderAsync();
                if (!reader.HasRows) { backup.Status = "Invalid"; await _context.SaveChangesAsync(); return false; }
                string? backupDbName = null;
                if (await reader.ReadAsync())
                {
                    backupDbName = reader["DatabaseName"] as string;
                }
                if (backupDbName == null || !backupDbName.Equals(expectedDbName, StringComparison.OrdinalIgnoreCase))
                {
                    backup.Status = "Invalid";
                    await _context.SaveChangesAsync();
                    return false;
                }
                using var cmdVerify = new SqlCommand(sqlVerify, conn);
                await cmdVerify.ExecuteNonQueryAsync();
                return true;
            }
            catch
            {
                backup.Status = "Invalid";
                await _context.SaveChangesAsync();
                return false;
            }
        }

        public async Task<string> GetDatabaseStatusAsync()
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                using var cmd = new SqlCommand("SELECT 1", conn);
                await cmd.ExecuteScalarAsync();
                return "Connected";
            }
            catch
            {
                return "Unavailable";
            }
        }

public async Task<bool> VerifyDatabaseAsync()
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                // Get distinct schema/table pairs from EF Core metadata
                var entityPairs = _context.Model.GetEntityTypes()
                    .Select(e => new {
                        Table = e.GetTableName(),
                        Schema = e.GetSchema() ?? "dbo"
                    })
                    .Where(p => !string.IsNullOrEmpty(p.Table))
                    .Distinct()
                    .ToList();
                foreach (var pair in entityPairs)
                {
                    var sql = "SELECT 1 FROM sys.tables t INNER JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = @t AND s.name = @s";
                    using var checkCmd = new SqlCommand(sql, conn);
                    checkCmd.Parameters.AddWithValue("@t", pair.Table);
                    checkCmd.Parameters.AddWithValue("@s", pair.Schema);
                    var exists = await checkCmd.ExecuteScalarAsync();
                    if (exists == null)
                    {
                        _logger?.LogWarning("Verification failed: table {Table} in schema {Schema} does not exist after restore.", pair.Table, pair.Schema);
                        return false;
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error during database verification after restore.");
                return false;
            }
        }

public async Task<bool> RestoreBackupAsync(int backupId, int staffId)
        {
            var backup = await GetBackupAsync(backupId);
            if (backup == null || backup.Status != "Successful" || !File.Exists(backup.FilePath))
            {
                _logger?.LogWarning("Restore aborted: backup {BackupId} not found, not successful, or file missing.", backupId);
                return false;
            }
            var dbName = GetDatabaseName();
            _logger?.LogInformation("[Restore Start] BackupId={BackupId}, TargetDb={DbName}, BackupPath={Path}", backupId, dbName, backup.FilePath);
            _logger?.LogInformation("[Stage] Validation - backup existence and status verified");

            // Safety backup before restore
            try
            {
                _logger?.LogInformation("[Safety Backup] Creating pre‑restore safety backup for staff {StaffId}", staffId);
                await CreatePreRestoreBackupAsync(staffId);
                _logger?.LogInformation("[Safety Backup] Completed successfully.");
            }
            catch (Exception ex)
            {
                await _activityLog.LogAsync("Create Pre‑Restore Backup", "System", $"Failed: {ex.Message}", staffId);
                _logger?.LogError(ex, "[Safety Backup] Failed before restore. BackupId={BackupId}", backupId);
                return false;
            }

            // Build a connection string that targets the master database
            var adminBuilder = new SqlConnectionStringBuilder(_connectionString)
            {
                InitialCatalog = "master"
            };
            var adminConnectionString = adminBuilder.ConnectionString;
            _logger?.LogDebug("[Admin Connection] Using master connection string for restore.");

            bool restoreSucceeded = false;
            bool multiUserSucceeded = false;
            bool verificationSucceeded = false;

            try
            {
                // Set SINGLE_USER and restore using master connection
                var sqlSingleUserAndRestore = $@"
ALTER DATABASE [{dbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
RESTORE DATABASE [{dbName}] FROM DISK = N'{backup.FilePath}' WITH REPLACE;";
                using var conn = new SqlConnection(adminConnectionString);
                await conn.OpenAsync();
                using var cmd = new SqlCommand(sqlSingleUserAndRestore, conn);
                _logger?.LogInformation("[Restore] Executing SINGLE_USER and RESTORE for database {DbName}", dbName);
                await cmd.ExecuteNonQueryAsync();
                restoreSucceeded = true;
                _logger?.LogInformation("[Restore] RESTORE command completed for backup {BackupId}", backupId);
            }
            catch (Exception ex)
            {
                // Capture SQL error details if available
                if (ex is SqlException sqlEx)
                {
                    await _activityLog.LogAsync("Database Restore", "System", $"SQL Error {sqlEx.Number}: {sqlEx.Message}", staffId);
                    _logger?.LogError(sqlEx, "[Restore] SQL failure (Error {ErrorNumber}) for backup {BackupId}", sqlEx.Number, backupId);
                }
                else
                {
                    await _activityLog.LogAsync("Database Restore", "System", $"Failed: {ex.Message}", staffId);
                    _logger?.LogError(ex, "[Restore] Unexpected failure for backup {BackupId}", backupId);
                }
                // Continue to finally to attempt MULTI_USER but overall success will be false
            }
            finally
            {
                // Ensure MULTI_USER state regardless of restore outcome using master connection
                try
                {
                    using var connMulti = new SqlConnection(adminConnectionString);
                    await connMulti.OpenAsync();
                    var sqlMulti = $"ALTER DATABASE [{dbName}] SET MULTI_USER;";
                    using var cmdMulti = new SqlCommand(sqlMulti, connMulti);
                    _logger?.LogInformation("[Post‑Restore] Setting database {DbName} back to MULTI_USER", dbName);
                    await cmdMulti.ExecuteNonQueryAsync();
                    multiUserSucceeded = true;
                }
                catch (Exception exMulti)
                {
                    await _activityLog.LogAsync("Database Restore", "System", $"Failed to ensure MULTI_USER: {exMulti.Message}", staffId);
                    _logger?.LogError(exMulti, "[Post‑Restore] Failed to set MULTI_USER for database {DbName}", dbName);
                    multiUserSucceeded = false;
                }
            }

            // Verify restored database using a new connection
            _logger?.LogInformation("[Verification] Starting verification of restored database {DbName}", dbName);
            try
            {
                // Open a new connection (not reusing any previous) to verify DB name and tables
                using var verifyConn = new SqlConnection(_connectionString);
                await verifyConn.OpenAsync();
                using var cmdDbName = new SqlCommand("SELECT DB_NAME()", verifyConn);
                var currentDb = (await cmdDbName.ExecuteScalarAsync()) as string ?? string.Empty;
                if (!string.Equals(currentDb, dbName, StringComparison.OrdinalIgnoreCase))
                {
                    _logger?.LogError("[Verification] Connected to unexpected database {CurrentDb} after restore, expected {ExpectedDb}", currentDb, dbName);
                }
                else
                {
                    verificationSucceeded = await VerifyDatabaseAsync();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[Verification] Exception during verification for database {DbName}", dbName);
                verificationSucceeded = false;
            }

            var overallSuccess = restoreSucceeded && multiUserSucceeded && verificationSucceeded;
            await _activityLog.LogAsync("Database Restore", "System", overallSuccess ? "Successful" : "Failed", staffId);
            _logger?.LogInformation("[Restore End] BackupId={BackupId}, TargetDb={DbName}, Success={Success}", backupId, dbName, overallSuccess);
            return overallSuccess;
        }
    }
}

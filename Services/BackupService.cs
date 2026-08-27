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
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;

namespace KaijensonIventory_SalesMotorShopWeb.Services
{
    public class BackupService : IBackupService
    {
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
            // No automatic validation on construction; call explicitly if needed.
        }

        private string GetDatabaseName()
        {
            var builder = new SqlConnectionStringBuilder(_connectionString);
            return builder.InitialCatalog;
        }

        private string GenerateBackupFileName(string suffix = null)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
            var dbName = GetDatabaseName();
            var safeDb = dbName.Replace(" ", "_");
            var extra = suffix != null ? $"_{suffix}" : "";
            return $"{safeDb}_{timestamp}{extra}.bak";
        }

        // Determine backup directory: custom config or controlled app folder
        private string GetBackupRoot()
        {
            var custom = _backupSettings.CurrentValue.BackupDirectory;
            if (!string.IsNullOrWhiteSpace(custom))
            {
                // Resolve relative paths against the content root, keep absolute as‑is
                string resolvedPath;
                if (Path.IsPathRooted(custom))
                {
                    resolvedPath = Path.GetFullPath(custom);
                }
                else
                {
                    resolvedPath = Path.GetFullPath(Path.Combine(_env.ContentRootPath, custom));
                }
                if (!Directory.Exists(resolvedPath))
                    Directory.CreateDirectory(resolvedPath);
                return resolvedPath;
            }

            // Default fallback – always use the app's content root
            var fallback = Path.Combine(_env.ContentRootPath, "App_Data", "Backups");
            if (!Directory.Exists(fallback))
                Directory.CreateDirectory(fallback);
            return fallback;
        }

        // Validate that the application can read/write the backup directory at startup
        private async Task ValidateBackupDirectoryAccessAsync()
        {
            var dir = GetBackupRoot();
            try
            {
                // Ensure directory exists (already ensured by GetBackupRoot)
                // Attempt to create a temporary file to verify write permission
                var testPath = Path.Combine(dir, $"_hermes_backup_test_{Guid.NewGuid()}.tmp");
                await File.WriteAllTextAsync(testPath, "test");
                // Verify file exists and can be read
                var content = await File.ReadAllTextAsync(testPath);
                if (content != "test")
                    throw new IOException("Test file content mismatch.");
                // Cleanup
                File.Delete(testPath);
                // Log success (debug level)
                // Assuming a logger is available via _activityLog or other, we log via activity log for diagnostics
                await _activityLog.LogAsync("Backup Directory Validation", "System", $"Backup directory '{dir}' is accessible for read/write.", staffId: null);
            }
            catch (Exception ex)
            {
                // Log detailed error but do not throw to avoid breaking app start
                await _activityLog.LogAsync("Backup Directory Validation", "System", $"Failed to access backup directory '{dir}': {ex.Message}", staffId: null);
            }
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

                var info = new FileInfo(filePath);
                var backup = new DatabaseBackup
                {
                    FileName = fileName,
                    FilePath = filePath,
                    FileSize = info.Length,
                    CreatedAt = DateTime.Now,
                    CreatedBy = staffId,
                    BackupType = "Manual",
                    Status = "Successful",
                    Description = null
                };
                _context.DatabaseBackups.Add(backup);
                await _context.SaveChangesAsync();
                await _activityLog.LogAsync("Create Database Backup", "System", $"Backup ID: {backup.BackupId}", staffId);
                return backup;
            }
            catch (Exception ex)
            {
                await _activityLog.LogAsync("Create Database Backup", "System", $"Failed: {ex.Message}", staffId);
                var failed = new DatabaseBackup
                {
                    FileName = fileName,
                    FilePath = filePath,
                    FileSize = 0,
                    CreatedAt = DateTime.Now,
                    CreatedBy = staffId,
                    BackupType = "Manual",
                    Status = "Failed",
                    Description = ex.Message
                };
                _context.DatabaseBackups.Add(failed);
                await _context.SaveChangesAsync();
                throw;
            }
        }

        public async Task<DatabaseBackup> CreatePreRestoreBackupAsync(int staffId)
        {
            var dbName = GetDatabaseName();
            var fileName = GenerateBackupFileName("PreRestore");
            var filePath = GetBackupFilePath(fileName);
            var sql = $"BACKUP DATABASE [{dbName}] TO DISK = N'{filePath}' WITH INIT, STATS = 10";
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                using var cmd = new SqlCommand(sql, conn);
                await cmd.ExecuteNonQueryAsync();

                var info = new FileInfo(filePath);
                var backup = new DatabaseBackup
                {
                    FileName = fileName,
                    FilePath = filePath,
                    FileSize = info.Length,
                    CreatedAt = DateTime.Now,
                    CreatedBy = staffId,
                    BackupType = "Pre-Restore Safety Backup",
                    Status = "Successful",
                    Description = "Safety backup created before restore"
                };
                _context.DatabaseBackups.Add(backup);
                await _context.SaveChangesAsync();
                await _activityLog.LogAsync("Create Pre-Restore Backup", "System", $"Backup ID: {backup.BackupId}", staffId);
                return backup;
            }
            catch (Exception ex)
            {
                await _activityLog.LogAsync("Create Pre-Restore Backup", "System", $"Failed: {ex.Message}", staffId);
                var failed = new DatabaseBackup
                {
                    FileName = fileName,
                    FilePath = filePath,
                    FileSize = 0,
                    CreatedAt = DateTime.Now,
                    CreatedBy = staffId,
                    BackupType = "Pre-Restore Safety Backup",
                    Status = "Failed",
                    Description = ex.Message
                };
                _context.DatabaseBackups.Add(failed);
                await _context.SaveChangesAsync();
                throw;
            }
        }

        public async Task<DatabaseBackup> GetBackupAsync(int backupId)
        {
            return await _context.DatabaseBackups.FindAsync(backupId);
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
                if (!reader.HasRows)
                {
                    backup.Status = "Invalid";
                    await _context.SaveChangesAsync();
                    return false;
                }
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

        // Verify core tables after restore
        public async Task<bool> VerifyDatabaseAsync()
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                var checks = new[]
                {
                    "SELECT TOP 1 StaffId FROM Staff",
                    "SELECT TOP 1 ProductId FROM Products",
                    "SELECT TOP 1 SaleId FROM SalesTransactions",
                    "SELECT TOP 1 ServiceJobId FROM ServiceJobs"
                };
                foreach (var sql in checks)
                {
                    using var cmd = new SqlCommand(sql, conn);
                    await cmd.ExecuteScalarAsync();
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        // Automatic Backup Method
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

                var info = new FileInfo(filePath);
                var backup = new DatabaseBackup
                {
                    FileName = fileName,
                    FilePath = filePath,
                    FileSize = info.Length,
                    CreatedAt = DateTime.Now,
                    CreatedBy = null,
                    BackupType = "Automatic",
                    Status = "Successful",
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

        public async Task<bool> RestoreBackupAsync(int backupId, int staffId)
        {
            var backup = await GetBackupAsync(backupId);
            if (backup == null || backup.Status != "Successful" || !File.Exists(backup.FilePath))
                return false;

            var dbName = GetDatabaseName();

            // 1. Create pre‑restore safety backup
            try
            {
                await CreatePreRestoreBackupAsync(staffId);
            }
            catch (Exception ex)
            {
                await _activityLog.LogAsync("Create Pre‑Restore Backup", "System", $"Failed: {ex.Message}", staffId);
                return false;
            }

            var sql = $@"
                USE [master];
                ALTER DATABASE [{dbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                RESTORE DATABASE [{dbName}] FROM DISK = N'{backup.FilePath}' WITH REPLACE;
                ALTER DATABASE [{dbName}] SET MULTI_USER;";

            try
            {
                await using var connRestore = new SqlConnection(_connectionString);
                await connRestore.OpenAsync();
                using var cmdRestore = new SqlCommand(sql, connRestore);
                await cmdRestore.ExecuteNonQueryAsync();
                await _activityLog.LogAsync("Restore Database", "System", $"Restored from Backup ID: {backupId}", staffId);

                var verified = await VerifyDatabaseAsync();
                if (!verified)
                {
                    await _activityLog.LogAsync("Restore Verification", "System", "Verification failed after restore", staffId);
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                await _activityLog.LogAsync("Restore Database", "System", $"Failed: {ex.Message}", staffId);
                return false;
            }
            finally
            {
                try
                {
                    var sqlMulti = $"ALTER DATABASE [{dbName}] SET MULTI_USER;";
                    await using var connMulti = new SqlConnection(_connectionString);
                    await connMulti.OpenAsync();
                    using var cmdMulti = new SqlCommand(sqlMulti, connMulti);
                    await cmdMulti.ExecuteNonQueryAsync();
                }
                catch { /* ignore */ }
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using KaijensonIventory_SalesMotorShopWeb.Data;
using KaijensonIventory_SalesMotorShopWeb.Models;
using Microsoft.Extensions.Hosting;

namespace KaijensonIventory_SalesMotorShopWeb.Services
{
    public class BackupService : IBackupService
    {
        private readonly ApplicationDbContext _context;
        private readonly IActivityLogService _activityLog;
        private readonly string _backupRoot;
        private readonly string _connectionString;

        public BackupService(ApplicationDbContext context, IActivityLogService activityLog, Microsoft.Extensions.Hosting.IHostEnvironment env)
        {
            _context = context;
            _activityLog = activityLog;
            // Ensure backup directory exists under App_Data/Backups
            _backupRoot = Path.Combine(env.ContentRootPath, "App_Data", "Backups");
            Directory.CreateDirectory(_backupRoot);
            _connectionString = context.Database.GetDbConnection().ConnectionString;
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

        private string GetBackupFilePath(string fileName)
        {
            return Path.Combine(_backupRoot, fileName);
        }

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
                    BackupType = "Full Database Backup",
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
                // Log failure
                await _activityLog.LogAsync("Create Database Backup", "System", $"Failed: {ex.Message}", staffId);
                var failed = new DatabaseBackup
                {
                    FileName = fileName,
                    FilePath = filePath,
                    FileSize = 0,
                    CreatedAt = DateTime.Now,
                    CreatedBy = staffId,
                    BackupType = "Full Database Backup",
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

        public async Task<DatabaseBackup?> GetBackupAsync(int backupId)
        {
            return await _context.DatabaseBackups.FindAsync(backupId);
        }

        public async Task<List<DatabaseBackup>> GetBackupHistoryAsync()
        {
            // Include staff navigation for name display
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

            // Ensure file is not empty
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
                // Run HEADERONLY and read DatabaseName
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
                    // Column name may be "DatabaseName" per SQL Server output
                    backupDbName = reader["DatabaseName"] as string;
                }
                if (backupDbName == null || !backupDbName.Equals(expectedDbName, StringComparison.OrdinalIgnoreCase))
                {
                    backup.Status = "Invalid";
                    await _context.SaveChangesAsync();
                    return false;
                }
                // Verify only
                using var cmdVerify = new SqlCommand(sqlVerify, conn);
                await cmdVerify.ExecuteNonQueryAsync();
                // All checks passed – keep status as Successful
                return true;
            }
            catch
            {
                // On any error mark as Invalid
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
                // Simple checks for core tables existence & queryability
                var checks = new[] {
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
                // Safety backup failed – abort restore
                await _activityLog.LogAsync("Create Pre‑Restore Backup", "System", $"Failed: {ex.Message}", staffId);
                // Mark the attempted restore as failed in activity log (already logged above)
                return false;
            }

            var sql = $@"USE [master];
                ALTER DATABASE [{dbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                RESTORE DATABASE [{dbName}] FROM DISK = N'{backup.FilePath}' WITH REPLACE;
                ALTER DATABASE [{dbName}] SET MULTI_USER;";

            try
            {
                await ExecuteNonQueryAsync(sql);
                await _activityLog.LogAsync("Restore Database", "System", $"Restored from Backup ID: {backupId}", staffId);
                // Post‑restore verification
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
                // Ensure database is returned to MULTI_USER even if restore fails
                try
                {
                    var sqlMulti = $"ALTER DATABASE [{dbName}] SET MULTI_USER;";
                    await ExecuteNonQueryAsync(sqlMulti);
                }
                catch { /* ignore – best‑effort */ }
            }
        }
    }
}

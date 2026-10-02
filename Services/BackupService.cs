using System.Data;
using System.IO;
using KaijensonIventory_SalesMotorShopWeb.Data;
using KaijensonIventory_SalesMotorShopWeb.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace KaijensonIventory_SalesMotorShopWeb.Services
{
    public class BackupService : IBackupService
    {
        private readonly ApplicationDbContext _context;
        private readonly IActivityLogService _activityLog;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<BackupService> _logger;

        public BackupService(
            ApplicationDbContext context,
            IActivityLogService activityLog,
            IConfiguration configuration,
            IWebHostEnvironment environment,
            ILogger<BackupService> logger)
        {
            _context = context;
            _activityLog = activityLog;
            _configuration = configuration;
            _environment = environment;
            _logger = logger;
        }

        public async Task<DatabaseBackup> CreateBackupAsync(int staffId)
        {
            var fileName = $"DatabaseBackup_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.bak";
            var backup = new DatabaseBackup
            {
                FileName = fileName,
                FilePath = string.Empty,
                CreatedAt = DateTime.Now,
                CreatedBy = staffId,
                BackupType = DatabaseBackup.FullBackupType,
                Status = DatabaseBackup.FailedStatus
            };
            string? backupRoot = null;

            try
            {
                await EnsureBackupMetadataTableAsync();
                backupRoot = GetBackupRoot(createIfMissing: true);
                var filePath = Path.GetFullPath(Path.Combine(backupRoot, fileName));
                if (filePath.Length > 260)
                    throw new InvalidOperationException("The configured backup directory is too long for the backup metadata path.");

                backup.FilePath = filePath;
                await using (var connection = new SqlConnection(GetMasterConnectionString()))
                {
                    await connection.OpenAsync();
                    await using var command = new SqlCommand(
                        $"BACKUP DATABASE {QuoteIdentifier(GetDatabaseName())} TO DISK = {SqlString(filePath)} WITH INIT, CHECKSUM, STATS = 10;",
                        connection)
                    {
                        CommandTimeout = 0
                    };
                    await command.ExecuteNonQueryAsync();
                }

                var fileInfo = new FileInfo(filePath);
                if (!fileInfo.Exists || fileInfo.Length <= 0)
                    throw new IOException("SQL Server completed the backup command but no readable backup file was found.");

                await using (var verifyConnection = new SqlConnection(GetMasterConnectionString()))
                {
                    await verifyConnection.OpenAsync();
                    await using var verifyCommand = new SqlCommand(
                        $"RESTORE VERIFYONLY FROM DISK = {SqlString(filePath)} WITH CHECKSUM;",
                        verifyConnection)
                    {
                        CommandTimeout = 0
                    };
                    await verifyCommand.ExecuteNonQueryAsync();
                }

                backup.FileSize = fileInfo.Length;
                backup.Status = DatabaseBackup.SuccessfulStatus;
                backup.Description = null;
            }
            catch (Exception exception)
            {
                backup.Status = DatabaseBackup.FailedStatus;
                backup.Description = Limit(exception.Message, 500);
                if (backup.FilePath.Length > 260)
                    backup.FilePath = string.Empty;

                _logger.LogError(exception, "Database backup failed for backup file {FileName}.", fileName);
                await TryDeletePartialBackupAsync(backupRoot, backup.FilePath);
            }

            try
            {
                _context.DatabaseBackups.Add(backup);
                await _context.SaveChangesAsync();
            }
            catch (Exception exception)
            {
                _context.Entry(backup).State = EntityState.Detached;
                if (backup.Status == DatabaseBackup.SuccessfulStatus)
                    await TryDeletePartialBackupAsync(backupRoot, backup.FilePath);

                _logger.LogError(exception, "Could not save metadata for database backup {FileName}.", fileName);
                await TryLogActivityAsync(
                    "Database Backup Failed",
                    $"Backup file {fileName} was not retained because its metadata could not be saved.",
                    staffId);
                throw new InvalidOperationException("Backup metadata could not be saved. The operation was not recorded as a usable backup.", exception);
            }

            if (backup.Status == DatabaseBackup.SuccessfulStatus)
            {
                await TryLogActivityAsync(
                    "Database Backup Created",
                    $"Backup ID {backup.BackupId}; file {backup.FileName}; size {backup.FileSize} bytes.",
                    staffId);
            }
            else
            {
                await TryLogActivityAsync(
                    "Database Backup Failed",
                    $"Backup ID {backup.BackupId}; file {backup.FileName}; {backup.Description ?? "Backup failed."}",
                    staffId);
            }

            return backup;
        }

        public async Task<List<DatabaseBackup>> GetBackupHistoryAsync()
        {
            return await _context.DatabaseBackups
                .AsNoTracking()
                .Include(backup => backup.CreatedByStaff)
                .OrderByDescending(backup => backup.CreatedAt)
                .ToListAsync();
        }

        public async Task<DatabaseBackup?> GetBackupAsync(int backupId)
        {
            return await _context.DatabaseBackups
                .FirstOrDefaultAsync(backup => backup.BackupId == backupId);
        }

        public async Task<bool> ValidateBackupAsync(int backupId)
        {
            var backup = await GetBackupAsync(backupId);
            if (backup == null || backup.Status != DatabaseBackup.SuccessfulStatus)
                return false;

            string? backupPath;
            try
            {
                backupPath = ResolveBackupPath(backup);
            }
            catch (Exception exception)
            {
                return await MarkBackupInvalidAsync(backup, exception.Message);
            }

            if (backupPath == null || !File.Exists(backupPath))
            {
                backup.Status = DatabaseBackup.MissingStatus;
                backup.Description = "The stored backup file is unavailable in the configured backup directory.";
                await _context.SaveChangesAsync();
                return false;
            }

            var fileInfo = new FileInfo(backupPath);
            if (fileInfo.Length <= 0)
                return await MarkBackupInvalidAsync(backup, "The backup file is empty.");

            try
            {
                await using var connection = new SqlConnection(GetMasterConnectionString());
                await connection.OpenAsync();

                string? backupDatabaseName;
                int backupType;
                bool backupHasChecksums;
                await using (var headerCommand = new SqlCommand(
                    $"RESTORE HEADERONLY FROM DISK = {SqlString(backupPath)};",
                    connection)
                {
                    CommandTimeout = 0
                })
                await using (var reader = await headerCommand.ExecuteReaderAsync())
                {
                    if (!await reader.ReadAsync())
                        return await MarkBackupInvalidAsync(backup, "SQL Server found no backup set in the file.");

                    backupDatabaseName = reader["DatabaseName"] as string;
                    backupType = Convert.ToInt32(reader["BackupType"]);
                    backupHasChecksums = Convert.ToBoolean(reader["HasBackupChecksums"]);
                }

                if (!string.Equals(backupDatabaseName, GetDatabaseName(), StringComparison.OrdinalIgnoreCase) || backupType != 1)
                    return await MarkBackupInvalidAsync(backup, "The backup does not contain a full backup of the configured database.");

                var checksumOption = backupHasChecksums ? " WITH CHECKSUM" : string.Empty;
                await using var verifyCommand = new SqlCommand(
                    $"RESTORE VERIFYONLY FROM DISK = {SqlString(backupPath)}{checksumOption};",
                    connection)
                {
                    CommandTimeout = 0
                };
                await verifyCommand.ExecuteNonQueryAsync();
                return true;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Backup validation failed for backup ID {BackupId}.", backupId);
                return await MarkBackupInvalidAsync(backup, "SQL Server could not validate this backup file.");
            }
        }

        public async Task<bool> RestoreBackupAsync(int backupId, int staffId)
        {
            DatabaseBackup? backup = null;
            var targetDatabase = string.Empty;
            var singleUserModeMayHaveBeenSet = false;
            var restoreCompleted = false;
            string? failureReason = null;

            try
            {
                backup = await GetBackupAsync(backupId);
                if (backup == null || backup.Status != DatabaseBackup.SuccessfulStatus)
                    throw new InvalidOperationException("The selected backup is not available for restore.");

                if (!await ValidateBackupAsync(backupId))
                    throw new InvalidOperationException("The selected backup failed file, database identity, or integrity validation.");

                backup = await GetBackupAsync(backupId);
                if (backup == null)
                    throw new InvalidOperationException("The selected backup record is no longer available.");

                var backupPath = ResolveBackupPath(backup);
                if (backupPath == null)
                    throw new InvalidOperationException("The selected backup is outside the configured backup directory.");

                targetDatabase = GetDatabaseName();
                await using (var connection = new SqlConnection(GetMasterConnectionString()))
                {
                    await connection.OpenAsync();

                    await using (var singleUserCommand = new SqlCommand(
                        $"ALTER DATABASE {QuoteIdentifier(targetDatabase)} SET SINGLE_USER WITH ROLLBACK IMMEDIATE;",
                        connection)
                    {
                        CommandTimeout = 0
                    })
                    {
                        singleUserModeMayHaveBeenSet = true;
                        await singleUserCommand.ExecuteNonQueryAsync();
                    }

                    await using var restoreCommand = new SqlCommand(
                        $"RESTORE DATABASE {QuoteIdentifier(targetDatabase)} FROM DISK = {SqlString(backupPath)} WITH REPLACE;",
                        connection)
                    {
                        CommandTimeout = 0
                    };
                    await restoreCommand.ExecuteNonQueryAsync();
                    restoreCompleted = true;
                }
            }
            catch (Exception exception)
            {
                failureReason = exception.Message;
                _logger.LogError(exception, "Database restore failed for backup ID {BackupId}.", backupId);
            }
            finally
            {
                if (singleUserModeMayHaveBeenSet && !await TrySetMultiUserModeAsync(targetDatabase))
                {
                    restoreCompleted = false;
                    failureReason ??= "SQL Server could not return the database to multi-user mode.";
                }
            }

            if (restoreCompleted)
            {
                try
                {
                    await using var verificationConnection = new SqlConnection(GetApplicationConnectionString());
                    await verificationConnection.OpenAsync();
                    await using var verificationCommand = new SqlCommand("SELECT DB_NAME();", verificationConnection);
                    var actualDatabase = await verificationCommand.ExecuteScalarAsync() as string;
                    if (!string.Equals(actualDatabase, targetDatabase, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("The restored database could not be verified as the configured target database.");
                }
                catch (Exception exception)
                {
                    restoreCompleted = false;
                    failureReason = exception.Message;
                    _logger.LogError(exception, "Database verification failed after restoring backup ID {BackupId}.", backupId);
                }
            }

            if (restoreCompleted)
            {
                var activityStaffId = await GetRestoredActivityStaffIdAsync(staffId);
                await TryLogActivityAsync(
                    "Database Restore Succeeded",
                    $"Restored by session Staff ID {staffId}; backup ID {backupId} ({backup?.FileName}).",
                    activityStaffId);
                return true;
            }

            await TryLogActivityAsync(
                "Database Restore Failed",
                $"Restore failed for backup ID {backupId}. {failureReason ?? "The database restore did not complete."}",
                staffId);
            return false;
        }

        public async Task<bool> DeleteBackupAsync(int backupId, int staffId)
        {
            DatabaseBackup? backup;
            try
            {
                backup = await GetBackupAsync(backupId);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Could not load backup ID {BackupId} for deletion.", backupId);
                await TryLogActivityAsync("Database Backup Deletion Failed", $"Could not load backup ID {backupId} for deletion.", staffId);
                return false;
            }

            if (backup == null || backup.Status == DatabaseBackup.DeletedStatus)
            {
                await TryLogActivityAsync("Database Backup Deletion Failed", $"Backup ID {backupId} was not available for deletion.", staffId);
                return false;
            }

            string? backupPath;
            try
            {
                backupPath = ResolveBackupPath(backup);
            }
            catch (Exception exception)
            {
                await TryLogActivityAsync("Database Backup Deletion Failed", $"Backup ID {backupId} could not be safely resolved for deletion.", staffId);
                _logger.LogWarning(exception, "Could not safely resolve backup ID {BackupId} for deletion.", backupId);
                return false;
            }

            if (backupPath == null)
            {
                await TryLogActivityAsync("Database Backup Deletion Failed", $"Backup ID {backupId} is outside the configured backup directory.", staffId);
                return false;
            }

            if (!File.Exists(backupPath))
            {
                backup.Status = DatabaseBackup.MissingStatus;
                backup.Description = "The backup file was missing when deletion was requested.";
                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Could not record the missing state of backup ID {BackupId}.", backupId);
                }

                await TryLogActivityAsync("Database Backup Deletion Failed", $"Backup ID {backupId} could not be deleted because its file was missing.", staffId);
                return false;
            }

            try
            {
                File.Delete(backupPath);
                backup.Status = DatabaseBackup.DeletedStatus;
                await _context.SaveChangesAsync();
                await TryLogActivityAsync("Database Backup Deleted", $"Deleted backup ID {backupId} ({backup.FileName}).", staffId);
                return true;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Could not delete or record deletion of backup ID {BackupId}.", backupId);
                if (!File.Exists(backupPath))
                {
                    backup.Status = DatabaseBackup.DeletedStatus;
                    try
                    {
                        await _context.SaveChangesAsync();
                        await TryLogActivityAsync("Database Backup Deleted", $"Deleted backup ID {backupId} ({backup.FileName}); metadata save was retried.", staffId);
                        return true;
                    }
                    catch (Exception saveException)
                    {
                        _logger.LogError(saveException, "Could not record deletion of backup ID {BackupId}.", backupId);
                    }
                }

                await TryLogActivityAsync("Database Backup Deletion Failed", $"Deletion failed for backup ID {backupId}.", staffId);
                return false;
            }
        }

        public async Task<string> GetDatabaseStatusAsync()
        {
            try
            {
                await using var connection = new SqlConnection(GetApplicationConnectionString());
                await connection.OpenAsync();
                await using var command = new SqlCommand("SELECT 1;", connection);
                await command.ExecuteScalarAsync();
                return "Connected";
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Could not verify database connectivity for the backup page.");
                return "Unavailable";
            }
        }

        private async Task EnsureBackupMetadataTableAsync()
        {
            var connection = _context.Database.GetDbConnection();
            var closeWhenFinished = connection.State != ConnectionState.Open;
            try
            {
                if (closeWhenFinished)
                    await _context.Database.OpenConnectionAsync();

                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT OBJECT_ID(N'dbo.DatabaseBackups', N'U');";
                var result = await command.ExecuteScalarAsync();
                if (result == null || result == DBNull.Value)
                    throw new InvalidOperationException("The DatabaseBackups table is not present. Verify the database migration state before using backup operations.");
            }
            finally
            {
                if (closeWhenFinished)
                    await _context.Database.CloseConnectionAsync();
            }
        }

        private string GetDatabaseName()
        {
            var databaseName = new SqlConnectionStringBuilder(GetApplicationConnectionString()).InitialCatalog;
            if (string.IsNullOrWhiteSpace(databaseName))
                throw new InvalidOperationException("The configured SQL Server connection does not specify a database.");
            return databaseName;
        }

        private string GetApplicationConnectionString()
        {
            return _context.Database.GetDbConnection().ConnectionString;
        }

        private string GetMasterConnectionString()
        {
            var builder = new SqlConnectionStringBuilder(GetApplicationConnectionString());
            builder.InitialCatalog = "master";
            return builder.ConnectionString;
        }

        private string GetBackupRoot(bool createIfMissing)
        {
            var configuredDirectory = _configuration["BackupStorage:Directory"];
            if (string.IsNullOrWhiteSpace(configuredDirectory) || !Path.IsPathFullyQualified(configuredDirectory))
                throw new InvalidOperationException("Set BackupStorage:Directory to an absolute directory outside wwwroot before using backups.");

            var root = Path.GetFullPath(configuredDirectory);
            var webRoot = Path.GetFullPath(_environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot"));
            if (IsSameOrWithin(webRoot, root))
                throw new InvalidOperationException("BackupStorage:Directory must be outside wwwroot.");

            if (createIfMissing)
                Directory.CreateDirectory(root);

            var resolvedRoot = ResolveDirectoryPath(root);
            var resolvedWebRoot = Directory.Exists(webRoot) ? ResolveDirectoryPath(webRoot) : webRoot;
            if (IsSameOrWithin(resolvedWebRoot, resolvedRoot))
                throw new InvalidOperationException("BackupStorage:Directory must be outside wwwroot.");

            return resolvedRoot;
        }

        private string? ResolveBackupPath(DatabaseBackup backup)
        {
            var root = GetBackupRoot(createIfMissing: false);
            if (string.IsNullOrWhiteSpace(backup.FilePath) || !Path.IsPathFullyQualified(backup.FilePath))
                return null;

            var fullPath = Path.GetFullPath(backup.FilePath);
            if (!IsWithin(root, fullPath) ||
                !string.Equals(Path.GetFileName(fullPath), backup.FileName, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(Path.GetExtension(fullPath), ".bak", StringComparison.OrdinalIgnoreCase))
                return null;

            if (File.Exists(fullPath) && (File.GetAttributes(fullPath) & FileAttributes.ReparsePoint) != 0)
                return null;

            return fullPath;
        }

        private static string ResolveDirectoryPath(string path)
        {
            var directory = new DirectoryInfo(path);
            return Path.GetFullPath(directory.ResolveLinkTarget(returnFinalTarget: true)?.FullName ?? directory.FullName);
        }

        private static bool IsSameOrWithin(string root, string candidate)
        {
            return string.Equals(Path.TrimEndingDirectorySeparator(root), Path.TrimEndingDirectorySeparator(candidate), PathComparison()) || IsWithin(root, candidate);
        }

        private static bool IsWithin(string root, string candidate)
        {
            var relativePath = Path.GetRelativePath(Path.GetFullPath(root), Path.GetFullPath(candidate));
            return !Path.IsPathRooted(relativePath) &&
                   !string.Equals(relativePath, "..", PathComparison()) &&
                   !relativePath.StartsWith(".." + Path.DirectorySeparatorChar, PathComparison()) &&
                   !relativePath.StartsWith(".." + Path.AltDirectorySeparatorChar, PathComparison());
        }

        private static StringComparison PathComparison()
        {
            return OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        }

        private static string QuoteIdentifier(string identifier)
        {
            return $"[{identifier.Replace("]", "]]")}]";
        }

        private static string SqlString(string value)
        {
            return $"N'{value.Replace("'", "''")}'";
        }

        private async Task<bool> MarkBackupInvalidAsync(DatabaseBackup backup, string description)
        {
            backup.Status = DatabaseBackup.InvalidStatus;
            backup.Description = Limit(description, 500);
            await _context.SaveChangesAsync();
            return false;
        }

        private async Task<bool> TrySetMultiUserModeAsync(string databaseName)
        {
            try
            {
                await using var connection = new SqlConnection(GetMasterConnectionString());
                await connection.OpenAsync();
                await using var command = new SqlCommand(
                    $"ALTER DATABASE {QuoteIdentifier(databaseName)} SET MULTI_USER WITH ROLLBACK IMMEDIATE;",
                    connection)
                {
                    CommandTimeout = 0
                };
                await command.ExecuteNonQueryAsync();
                return true;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Could not return database {DatabaseName} to multi-user mode after restore.", databaseName);
                return false;
            }
        }

        private Task TryDeletePartialBackupAsync(string? backupRoot, string filePath)
        {
            if (string.IsNullOrWhiteSpace(backupRoot) || string.IsNullOrWhiteSpace(filePath))
                return Task.CompletedTask;

            try
            {
                if (IsWithin(backupRoot, filePath) && File.Exists(filePath))
                    File.Delete(filePath);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Could not remove incomplete backup file {FileName}.", Path.GetFileName(filePath));
            }

            return Task.CompletedTask;
        }

        private async Task<int?> GetRestoredActivityStaffIdAsync(int staffId)
        {
            try
            {
                await using var connection = new SqlConnection(GetApplicationConnectionString());
                await connection.OpenAsync();
                await using var command = new SqlCommand("SELECT 1 FROM dbo.Staff WHERE StaffId = @StaffId;", connection);
                command.Parameters.Add("@StaffId", SqlDbType.Int).Value = staffId;
                return await command.ExecuteScalarAsync() == null ? null : staffId;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "The restoring staff record is not available in the restored database.");
                return null;
            }
        }

        private async Task TryLogActivityAsync(string action, string description, int? staffId)
        {
            try
            {
                await _activityLog.LogAsync(action, "Backup", Limit(description, 500), staffId);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Could not write backup activity log entry for {Action}.", action);
            }
        }

        private static string Limit(string value, int length)
        {
            return value.Length <= length ? value : value[..length];
        }
    }
}

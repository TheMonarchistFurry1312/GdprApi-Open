using GdprConfigurations;
using Microsoft.Extensions.Logging;
using Models.AuditLog;
using MongoDB.Driver;
using Repositories.Interfaces;

namespace Repositories
{
    public class AuditLogsRepository : IAuditLogsRepository
    {
        private readonly IMongoCollection<AuditLog> _auditLogCollection;
        private readonly ILogger<AuditLogsRepository> _logger;

        public AuditLogsRepository(
            IMongoClient mongoClient,
            IMongoDbSettings settings,
            ILogger<AuditLogsRepository> logger)
        {
            var database = mongoClient.GetDatabase(settings.DatabaseName);
            _auditLogCollection = database.GetCollection<AuditLog>("AuditLogs");
            _logger = logger;
        }

        public async Task<string> CreateAsync(AuditLog auditLog)
        {
            try
            {
                await _auditLogCollection.InsertOneAsync(auditLog);
                _logger.LogInformation("Audit log created successfully with ID: {Id}, ActionType: {ActionType}, TenantId: {TenantId}",
                    auditLog.Id, auditLog.ActionType, auditLog.TenantId);
                return auditLog.Id;
            }
            catch (MongoException ex)
            {
                _logger.LogError(ex, "Failed to create audit log for ActionType: {ActionType}, TenantId: {TenantId}",
                    auditLog.ActionType, auditLog.TenantId);
                throw new InvalidOperationException("An error occurred while creating the audit log.", ex);
            }
        }

        public async Task<bool> ExistsByIdAsync(string auditLogId)
        {
            var idFilter = Builders<AuditLog>.Filter.Eq(t => t.Id, auditLogId);
            return await _auditLogCollection.Find(idFilter).AnyAsync();
        }
    }
}
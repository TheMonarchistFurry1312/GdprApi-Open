using Microsoft.Extensions.Logging;
using Models.AuditLog;
using Models.Enums;
using Repositories.Interfaces;
using System.ComponentModel.DataAnnotations;

namespace GdprServices.AuditLogs
{
    public class AuditLogsService : IAuditLogs
    {
        private readonly IAuditLogsRepository _repository;
        private readonly ILogger<AuditLogsService> _logger;

        public AuditLogsService(
            IAuditLogsRepository repository,
            ILogger<AuditLogsService> logger)
        {
            _repository = repository;
            _logger = logger;
        }

        public async Task<string> CreateAsync(AuditLog auditLog)
        {
            // Validate input
            if (auditLog == null)
            {
                _logger.LogWarning("CreateAsync called with null audit log.");
                throw new ArgumentNullException(nameof(auditLog), "Audit log cannot be null.");
            }

            var validationContext = new ValidationContext(auditLog);
            Validator.ValidateObject(auditLog, validationContext, validateAllProperties: true);

            // Ensure TimestampUtc is set
            if (auditLog.TimestampUtc == default)
            {
                auditLog.TimestampUtc = DateTime.UtcNow;
            }

            // Validate PerformedBy for user/admin actions
            if (auditLog.ActorType is ActorType.User or ActorType.Admin && string.IsNullOrEmpty(auditLog.PerformedBy))
            {
                _logger.LogWarning("PerformedBy is required for ActorType {ActorType}.", auditLog.ActorType);
                throw new ValidationException("PerformedBy is required for User or Admin actions.");
            }

            // Check for duplicate ID
            if (await _repository.ExistsByIdAsync(auditLog.Id))
            {
                _logger.LogWarning("Attempt to create audit log with existing ID: {Id}", auditLog.Id);
                throw new InvalidOperationException("An audit log with this ID already exists.");
            }

            // Compute integrity hash
            auditLog.ComputeIntegrityHash();

            // Insert into database via repository
            return await _repository.CreateAsync(auditLog);
        }
    }
}
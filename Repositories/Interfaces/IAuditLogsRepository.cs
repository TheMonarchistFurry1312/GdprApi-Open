using Models.AuditLog;

namespace Repositories.Interfaces
{
    public interface IAuditLogsRepository
    {
        Task<string> CreateAsync(AuditLog auditLog);
        Task<bool> ExistsByIdAsync(string auditLogId);
    }
}

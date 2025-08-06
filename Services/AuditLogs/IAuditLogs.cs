using Models.AuditLog;

namespace GdprServices.AuditLogs
{
    public interface IAuditLogs
    {
        /// <summary>
        /// Creates a new audit log entry asynchronously and returns its unique identifier.
        /// </summary>
        /// <param name="auditLog">The audit log entry to create.</param>
        /// <returns>A task that represents the asynchronous operation, returning the ID of the created audit log.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the audit log is null.</exception>
        /// <exception cref="ValidationException">Thrown when the audit log fails validation.</exception>
        /// <exception cref="InvalidOperationException">Thrown when an audit log with the same ID already exists or creation fails.</exception>
        Task<string> CreateAsync(AuditLog auditLog);
    }
}

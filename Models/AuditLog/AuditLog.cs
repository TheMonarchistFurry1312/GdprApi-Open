using Models.Enums;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Models.AuditLog
{
    public class AuditLog
    {
        /// <summary>
        /// Unique identifier for this audit record, stored as an ObjectId in MongoDB.
        /// </summary>
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = null!;

        /// <summary>
        /// Identifier of the tenant to which this audit log belongs, ensuring multi-tenant segregation.
        /// </summary>
        [Required(ErrorMessage = "TenantId is required.")]
        [StringLength(100, ErrorMessage = "TenantId cannot exceed 100 characters.")]
        public string TenantId { get; set; } = null!;

        /// <summary>
        /// Identifier of the user, admin, or system that performed the action (e.g., user ID, system name).
        /// Required for user-initiated actions to ensure GDPR accountability (Article 5(2)).
        /// </summary>
        [StringLength(100, ErrorMessage = "PerformedBy cannot exceed 100 characters.")]
        public string? PerformedBy { get; set; }

        /// <summary>
        /// Type of actor who performed the action (e.g., User, Admin, System, Anonymous).
        /// Clarifies the context of PerformedBy for better traceability.
        /// </summary>
        public ActorType ActorType { get; set; }

        /// <summary>
        /// Type of action performed (e.g., Create, Update, Delete, ConsentGiven).
        /// Uses enum to ensure consistent categorization of actions.
        /// </summary>
        public AuditActionType ActionType { get; set; }

        /// <summary>
        /// The entity affected by the action (e.g., Tenant, User, Document).
        /// Uses enum to prevent typos and ensure valid entity types for GDPR compliance.
        /// </summary>
        [Required(ErrorMessage = "TargetEntity is required.")]
        public TargetEntityType TargetEntity { get; set; }

        /// <summary>
        /// Identifier of the specific entity instance affected (e.g., user ID, document ID).
        /// Optional, as not all actions target a specific instance.
        /// </summary>
        [StringLength(100, ErrorMessage = "TargetEntityId cannot exceed 100 characters.")]
        public string? TargetEntityId { get; set; }

        /// <summary>
        /// The exact time when the action was performed, stored in UTC for consistency.
        /// Set at persistence time to ensure accuracy.
        /// </summary>
        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime TimestampUtc { get; set; }

        /// <summary>
        /// Hashed or anonymized IP address of the client performing the action.
        /// Supports GDPR data minimization by avoiding raw IP storage.
        /// </summary>
        [StringLength(64, ErrorMessage = "ClientIpAddress cannot exceed 64 characters.")]
        public string? ClientIpAddress { get; set; }

        /// <summary>
        /// Type of device or client (e.g., "Browser", "Mobile", "API").
        /// Structured to avoid collecting excessive data in ClientInfo.
        /// </summary>
        [StringLength(50, ErrorMessage = "DeviceType cannot exceed 50 characters.")]
        public string? DeviceType { get; set; }

        /// <summary>
        /// Structured details about the action (e.g., changed fields, consent details).
        /// Stored as a dictionary to enforce schema and support querying.
        /// </summary>
        public Dictionary<string, object>? Details { get; set; }

        /// <summary>
        /// Indicates if the action is relevant to GDPR compliance (e.g., consent, data erasure).
        /// Simplifies compliance reporting and filtering.
        /// </summary>
        public bool IsGdprRelevant { get; set; }

        /// <summary>
        /// Date when the audit log should be deleted, based on retention policy.
        /// Supports GDPR storage limitation (Article 5(1)(e)).
        /// </summary>
        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime? RetentionExpiryUtc { get; set; }

        /// <summary>
        /// Unique identifier to correlate this action across systems or services.
        /// Enhances traceability in distributed systems.
        /// </summary>
        [StringLength(100, ErrorMessage = "CorrelationId cannot exceed 100 characters.")]
        public string? CorrelationId { get; set; }

        /// <summary>
        /// Indicates whether the action was successful.
        /// Useful for auditing failed attempts (e.g., LoginFailed).
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// SHA-256 hash of the audit log's critical fields to ensure immutability.
        /// Computed on creation to detect tampering.
        /// </summary>
        [StringLength(64, ErrorMessage = "IntegrityHash cannot exceed 64 characters.")]
        public string? IntegrityHash { get; set; }

        public string? Comments { get; set; }

        /// <summary>
        /// Computes the integrity hash for the audit log to ensure immutability.
        /// </summary>
        public void ComputeIntegrityHash()
        {
            var data = $"{Id}{TenantId}{PerformedBy}{ActorType}{ActionType}{TargetEntity}{TargetEntityId}{TimestampUtc:yyyy-MM-ddTHH:mm:ssZ}{ClientIpAddress}{DeviceType}{JsonSerializer.Serialize(Details)}{IsGdprRelevant}{RetentionExpiryUtc?.ToString("yyyy-MM-ddTHH:mm:ssZ")}{CorrelationId}{IsSuccess}";
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(data);
            var hash = sha256.ComputeHash(bytes);
            IntegrityHash = Convert.ToBase64String(hash);
        }
    }
}
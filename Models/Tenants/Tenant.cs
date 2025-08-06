#nullable enable
using Models.Enums;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;

namespace Models.Tenants
{
    /// <summary>
    /// Represents a tenant in the GDPR-compliant multi-tenant system, storing user-related data
    /// with pseudonymized personal data and retention policies to comply with GDPR Articles 5, 25, and 32.
    /// </summary>
    public class Tenant
    {
        /// <summary>
        /// Unique identifier for the tenant, stored as an ObjectId in MongoDB.
        /// </summary>
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = null!;

        /// <summary>
        /// Pseudonymized full name of the tenant user (e.g., hashed to reduce identifiability).
        /// Complies with GDPR data minimization and pseudonymization (Article 25).
        /// </summary>
        [Required(ErrorMessage = "FullName is required.")]
        [StringLength(100, ErrorMessage = "FullName cannot exceed 100 characters.")]
        public string FullName { get; set; } = null!;

        /// <summary>
        /// Pseudonymized email address of the tenant user (e.g., hashed to reduce identifiability).
        /// Complies with GDPR data minimization and pseudonymization (Article 25).
        /// </summary>
        [Required(ErrorMessage = "Email is required.")]
        [StringLength(100, ErrorMessage = "Email cannot exceed 100 characters.")]
        public string Email { get; set; } = null!;

        /// <summary>
        /// Hashed password for the tenant user, ensuring secure storage.
        /// Required for GDPR security of processing (Article 32).
        /// </summary>
        [Required]
        public byte[] PasswordHash { get; set; } = null!;

        /// <summary>
        /// Salt used for password hashing, ensuring secure password storage.
        /// Required for GDPR security of processing (Article 32).
        /// </summary>
        [Required]
        public byte[] PasswordSalt { get; set; } = null!;

        /// <summary>
        /// Username chosen by the tenant user, used for identification within the system.
        /// </summary>
        [Required(ErrorMessage = "UserName is required.")]
        [StringLength(50, ErrorMessage = "UserName cannot exceed 50 characters.")]
        public string UserName { get; set; } = null!;

        /// <summary>
        /// Type of account (e.g., Basic, Premium), determining access levels or features.
        /// </summary>
        public AccountType AccountType { get; set; }

        /// <summary>
        /// Role of the user within the tenant (e.g., Owner, Admin), defining permissions.
        /// </summary>
        public UserRole Role { get; set; }

        /// <summary>
        /// Indicates whether the tenant's email address has been confirmed.
        /// Defaults to false until confirmation is completed.
        /// </summary>
        public bool EmailConfirmed { get; set; } = false;

        /// <summary>
        /// UTC timestamp when the tenant was created, supporting auditability (GDPR Article 30).
        /// </summary>
        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime CreatedAtUtc { get; set; }

        /// <summary>
        /// UTC timestamp when the tenant was updated, supporting auditability (GDPR Article 30).
        /// </summary>
        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime UpdatedAtUtc { get; set; }

        /// <summary>
        /// URL of the tenant's website, if provided.
        /// Optional field to avoid unnecessary data collection (GDPR Article 5(1)(c)).
        /// </summary>
        [StringLength(200, ErrorMessage = "WebsiteUrl cannot exceed 200 characters.")]
        public string? WebsiteUrl { get; set; }

        /// <summary>
        /// Unique identifier for tracking the account creation request.
        /// Supports auditability and traceability (GDPR Article 30).
        /// </summary>
        [StringLength(36, ErrorMessage = "AccountRequestId cannot exceed 36 characters.")]
        public string? AccountRequestId { get; set; }

        /// <summary>
        /// Indicates whether the tenant has accepted the necessary consent for data processing.
        /// Required for GDPR compliance with consent conditions (Article 7).
        /// </summary>
        public bool ConsentAccepted { get; set; }

        /// <summary>
        /// UTC timestamp when consent was accepted, ensuring auditable consent records (GDPR Article 7).
        /// </summary>
        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime ConsentAcceptedUtcDate { get; set; }

        /// <summary>
        /// UTC timestamp when the tenant data should be deleted, enforcing GDPR storage limitation (Article 5(1)(e)).
        /// </summary>
        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime? RetentionExpiryUtc { get; set; }

        /// <summary>
        /// Unique client identifier for the tenant, used for authentication and integration with external identity providers.
        /// Optional field to avoid unnecessary data collection (GDPR Article 5(1)(c)).
        /// </summary>
        [StringLength(100, ErrorMessage = "ClientId cannot exceed 100 characters.")]
        public string? ClientId { get; set; } // TODO: Pseudonymized the Client ID
    }
}
#nullable disable
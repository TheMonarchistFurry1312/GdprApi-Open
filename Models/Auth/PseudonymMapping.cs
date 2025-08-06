using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;

namespace Models.Auth
{
    /// <summary>
    /// Stores encrypted mappings of pseudonymized data (e.g., hashed FullName, Email) to original values,
    /// enabling GDPR-compliant data subject access (Article 15) while ensuring security (Article 32).
    /// </summary>
    public class PseudonymMapping
    {
        /// <summary>
        /// Unique identifier for the mapping, stored as an ObjectId in MongoDB.
        /// </summary>
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = null!;

        /// <summary>
        /// The tenant ID associated with this mapping, ensuring tenant isolation.
        /// </summary>
        [Required(ErrorMessage = "TenantId is required.")]
        [StringLength(100, ErrorMessage = "TenantId cannot exceed 100 characters.")]
        public string TenantId { get; set; } = null!;

        /// <summary>
        /// The hashed value (e.g., SHA-256) of the original data (FullName or Email).
        /// </summary>
        [Required(ErrorMessage = "HashedValue is required.")]
        [StringLength(100, ErrorMessage = "HashedValue cannot exceed 100 characters.")]
        public string HashedValue { get; set; } = null!;

        /// <summary>
        /// The original data (e.g., FullName, Email), encrypted with a secure algorithm (e.g., AES).
        /// </summary>
        [Required(ErrorMessage = "EncryptedOriginalValue is required.")]
        public byte[] EncryptedOriginalValue { get; set; } = null!;

        /// <summary>
        /// The field type (e.g., FullName, Email) to distinguish mapped data.
        /// </summary>
        [Required(ErrorMessage = "FieldType is required.")]
        [StringLength(50, ErrorMessage = "FieldType cannot exceed 50 characters.")]
        public string FieldType { get; set; } = null!;

        /// <summary>
        /// UTC timestamp when the mapping should be deleted, enforcing GDPR storage limitation (Article 5(1)(e)).
        /// </summary>
        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime? RetentionExpiryUtc { get; set; }
    }
}
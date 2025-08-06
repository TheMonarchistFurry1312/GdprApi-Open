using Models.Enums;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Models.Auth
{
    public class User
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = default!;

        public string Email { get; set; } = default!;
        public string PasswordHash { get; set; } = default!;
        public bool EmailConfirmed { get; set; } = false;

        [BsonRepresentation(BsonType.ObjectId)]
        public string TenantId { get; set; } = default!;

        public UserRole Role { get; set; } = UserRole.Member;
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}

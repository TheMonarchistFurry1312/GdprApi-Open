using MongoDB.Bson;

namespace Models.Auth
{
    public class RefreshToken
    {
        public string Id { get; set; } = ObjectId.GenerateNewId().ToString();
        public string TenantId { get; set; }
        public string Token { get; set; } // Guarda token seguro, no el JWT
        public DateTime ExpiresAtUtc { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public string CreatedByIp { get; set; } = null!;
        public bool IsRevoked { get; set; }
        public DateTime? RevokedAtUtc { get; set; }
        public string? RevokedByIp { get; set; }
        public string? ReplacedByToken { get; set; }
    }
}

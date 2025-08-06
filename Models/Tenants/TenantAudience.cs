using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Models.Tenants
{
    public class TenantAudience
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }
        public required string TenantId { get; set; }
        public Dictionary<string, object>? Details { get; set; }
    }
}

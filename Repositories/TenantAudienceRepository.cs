using GdprConfigurations;
using Models.Auth;
using Models.Tenants;
using MongoDB.Driver;
using Repositories.Interfaces;

namespace Repositories
{
    public class TenantAudienceRepository : ITenantAudienceRepository
    {
        private readonly IMongoDatabase _mongoDatabase;
        private readonly IMongoCollection<Tenant> _tenantsCollection;
        private readonly IMongoCollection<PseudonymMapping> _pseudonymMappingsCollection;
        private readonly IMongoCollection<TenantAudience> _tenantAudienceCollection;

        public TenantAudienceRepository(IMongoClient mongoClient, IMongoDbSettings settings)
        {
            _mongoDatabase = mongoClient.GetDatabase(settings.DatabaseName);
            _tenantsCollection = _mongoDatabase.GetCollection<Tenant>("Tenants");
            _pseudonymMappingsCollection = _mongoDatabase.GetCollection<PseudonymMapping>("PseudonymMappings");
            _tenantAudienceCollection = _mongoDatabase.GetCollection<TenantAudience>("TenantAudience");
        }

        public async Task<Tenant> GetTenantByIdAsync(string tenantId)
        {
            var filter = Builders<Tenant>.Filter.Eq(t => t.Id, tenantId);
            return await _tenantsCollection.Find(filter).FirstOrDefaultAsync();
        }

        public async Task InsertTenantAudienceAsync(TenantAudience tenantAudience)
        {
            await _tenantAudienceCollection.InsertOneAsync(tenantAudience);
        }

        public async Task<List<TenantAudience>> GetTenantAudiencesByTenantIdAsync(string tenantId, int skip, int take)
        {
            var filter = Builders<TenantAudience>.Filter.Eq(t => t.TenantId, tenantId);
            return await _tenantAudienceCollection
                .Find(filter)
                .Skip(skip)
                .Limit(take)
                .ToListAsync();
        }

        public void CreatePseudonymMappingIndex()
        {
            var indexKeys = Builders<PseudonymMapping>.IndexKeys
                .Ascending(x => x.TenantId)
                .Ascending(x => x.FieldType);
            _pseudonymMappingsCollection.Indexes.CreateOne(new CreateIndexModel<PseudonymMapping>(indexKeys));
        }
    }
}

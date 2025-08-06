using GdprConfigurations;
using Microsoft.Extensions.Logging;
using Models.Auth;
using Models.Tenants;
using MongoDB.Driver;
using Repositories.Interfaces;

namespace Repositories
{
    public class TenantRepository : ITenantRepository
    {
        private readonly IMongoCollection<Tenant> _tenantsCollection;
        private readonly IMongoCollection<PseudonymMapping> _pseudonymMappingsCollection;
        private readonly IMongoCollection<RefreshToken> _refreshTokensCollection;
        private readonly ILogger<TenantRepository> _logger;

        public TenantRepository(
            IMongoClient mongoClient,
            IMongoDbSettings settings,
            ILogger<TenantRepository> logger)
        {
            var database = mongoClient.GetDatabase(settings.DatabaseName);
            _tenantsCollection = database.GetCollection<Tenant>("Tenants");
            _pseudonymMappingsCollection = database.GetCollection<PseudonymMapping>("PseudonymMappings");
            _refreshTokensCollection = database.GetCollection<RefreshToken>("RefreshTokens");
            _logger = logger;

            // Create index on PseudonymMapping for efficient retrieval
            var indexKeys = Builders<PseudonymMapping>.IndexKeys
                .Ascending(x => x.TenantId)
                .Ascending(x => x.FieldType);
            _pseudonymMappingsCollection.Indexes.CreateOne(new CreateIndexModel<PseudonymMapping>(indexKeys));
        }

        public async Task<bool> ExistsByEmailAsync(string hashedEmail)
        {
            var emailFilter = Builders<Tenant>.Filter.Eq(t => t.Email, hashedEmail);
            return await _tenantsCollection.Find(emailFilter).AnyAsync();
        }

        public async Task CreateTenantAsync(Tenant tenant, IEnumerable<PseudonymMapping> mappings)
        {
            try
            {
                await _tenantsCollection.InsertOneAsync(tenant);
                await _pseudonymMappingsCollection.InsertManyAsync(mappings);
                _logger.LogInformation("Tenant created successfully with ID: {Id}, Email: {Email}", tenant.Id, tenant.Email);
            }
            catch (MongoException ex)
            {
                _logger.LogError(ex, "Failed to create tenant for email: {Email}", tenant.Email);
                throw new InvalidOperationException("An error occurred while creating the tenant.", ex);
            }
        }

        public async Task<Tenant> GetByEmailAsync(string hashedEmail)
        {
            var filter = Builders<Tenant>.Filter.Eq(t => t.Email, hashedEmail);
            return await _tenantsCollection.Find(filter).FirstOrDefaultAsync();
        }

        public async Task<Tenant> GetByIdAsync(string tenantId)
        {
            var filter = Builders<Tenant>.Filter.Eq(t => t.Id, tenantId);
            return await _tenantsCollection.Find(filter).FirstOrDefaultAsync();
        }

        public async Task<RefreshToken> GetRefreshTokenAsync(string token)
        {
            var filter = Builders<RefreshToken>.Filter.Eq(rt => rt.Token, token);
            return await _refreshTokensCollection.Find(filter).FirstOrDefaultAsync();
        }

        public async Task UpdateRefreshTokenAsync(string token, RefreshToken updateData)
        {
            var filter = Builders<RefreshToken>.Filter.Eq(rt => rt.Token, token);
            var update = Builders<RefreshToken>.Update
                .Set(rt => rt.IsRevoked, true)
                .Set(rt => rt.RevokedAtUtc, DateTime.UtcNow)
                .Set(rt => rt.RevokedByIp, updateData.CreatedByIp)
                .Set(rt => rt.ReplacedByToken, updateData.Token);
            await _refreshTokensCollection.UpdateOneAsync(filter, update);
        }

        public async Task CreateRefreshTokenAsync(RefreshToken refreshToken)
        {
            await _refreshTokensCollection.InsertOneAsync(refreshToken);
        }
    }
}
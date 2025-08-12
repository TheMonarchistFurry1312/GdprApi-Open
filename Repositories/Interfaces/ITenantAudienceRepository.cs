using Models.Tenants;

namespace Repositories.Interfaces
{
    public interface ITenantAudienceRepository
    {
        Task<Tenant> GetTenantByIdAsync(string tenantId);
        Task InsertTenantAudienceAsync(TenantAudience tenantAudience);
        Task<List<TenantAudience>> GetTenantAudiencesByTenantIdAsync(string tenantId, int skip, int take);
        void CreatePseudonymMappingIndex();
    }
}

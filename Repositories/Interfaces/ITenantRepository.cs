using Models.Auth;
using Models.Tenants;

namespace Repositories.Interfaces
{
    public interface ITenantRepository
    {
        Task<bool> ExistsByEmailAsync(string hashedEmail);
        Task CreateTenantAsync(Tenant tenant, IEnumerable<PseudonymMapping> mappings);
        Task<Tenant> GetByEmailAsync(string hashedEmail);
        Task<RefreshToken> GetRefreshTokenAsync(string token);
        Task<Tenant> GetByIdAsync(string tenantId);
        Task UpdateRefreshTokenAsync(string token, RefreshToken updateData);
        Task CreateRefreshTokenAsync(RefreshToken refreshToken);
        Task<PseudonymMapping> GetPseudonymMappingByTenantIdAndFieldTypeAsync(string tenantId);
    }
}
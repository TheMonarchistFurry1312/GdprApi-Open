using Models.Tenants;

namespace GdprServices.Users
{
    public interface ITenantService
    {
        /// <summary>
        /// Updates a tenant asynchronously based on the provided registration request.
        /// </summary>
        /// <param name="request">The registration request containing tenant details such as email, password, account type, tenant name, username, and full name.</param>
        /// <returns>A task that resolves to a string confirming the tenant creation with the unique AccountRequestId.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the request is null.</exception>
        /// <exception cref="ValidationException">Thrown when the request fails validation, such as mismatched passwords.</exception>
        /// <exception cref="InvalidOperationException">Thrown when a tenant with the same email already exists or a database error occurs.</exception>
        Task<string> UpdateTenantAsync(string tenantId, UpdateTenantRequest request, string clientIdFromHeader);

        /// <summary>
        /// Retrieves tenant data by ID, returning original (unpseudonymized) values for authorized users.
        /// Logs access for GDPR compliance (Article 15, 30).
        /// </summary>
        /// <param name="tenantId"></param>
        /// <returns></returns>
        Task<TenantResponse> GetTenantDataAsync(string tenantId, string clientIdFromHeader);

        /// <summary>
        /// Retrieves tenant data, including tenant details and associated pseudonym mappings, 
        /// for the specified tenant ID, ensuring compliance with GDPR Article 15 (Right of Access).
        /// </summary>
        /// <param name="tenantId"></param>
        /// <param name="clientIdFromHeader"></param>
        /// <param name="format"></param>
        /// <returns></returns>
        Task<string> DownloadTenantDataAsync(string tenantId, string clientIdFromHeader, string format);
    }
}
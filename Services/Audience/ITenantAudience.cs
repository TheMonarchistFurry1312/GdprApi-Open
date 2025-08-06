using Models.Tenants;

namespace GdprServices.Audience
{
    /// <summary>
    /// Defines methods for managing tenant audience data in a GDPR-compliant manner.
    /// Provides functionality to save and retrieve tenant audience records, ensuring
    /// proper authorization, consent validation, and encryption of sensitive data.
    /// </summary>
    public interface ITenantAudience
    {
        /// <summary>
        /// Saves tenant audience data, including arbitrary details with nested objects, to the database.
        /// Encrypts the values in the Details dictionary as byte arrays for secure storage.
        /// Logs the action for GDPR compliance (Article 30).
        /// </summary>
        /// <param name="tenantAudience">The tenant audience data, including a dictionary of details to be encrypted.</param>
        /// <param name="clientIdFromHeader">The client ID from the request header for authorization.</param>
        /// <returns>A task representing the asynchronous operation, returning a success message.</returns>
        /// <exception cref="ArgumentNullException">Thrown when tenantAudience or clientIdFromHeader is null or empty.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the tenant is not found, consent is missing, or saving fails.</exception>
        /// <exception cref="UnauthorizedAccessException">Thrown when the client ID is invalid.</exception>
        Task<string> SaveTenantAudienceAsync(TenantAudience tenantAudience, string clientIdFromHeader);

        /// <summary>
        /// Retrieves all tenant audience records for a given tenant ID.
        /// Decrypts and deserializes the encrypted byte array values in the Details dictionary,
        /// returning them with camelCase keys for JSON compatibility.
        /// Logs the access for GDPR compliance (Articles 15 and 30).
        /// </summary>
        /// <param name="tenantId">The ID of the tenant whose audience data is to be retrieved.</param>
        /// <param name="clientIdFromHeader">The client ID from the request header for authorization.</param>
        /// <returns>A task representing the asynchronous operation, returning a list of tenant audience data.</returns>
        /// <exception cref="ArgumentNullException">Thrown when tenantId or clientIdFromHeader is null or empty.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the tenant is not found, consent is missing, or decryption/deserialization fails.</exception>
        /// <exception cref="UnauthorizedAccessException">Thrown when the client ID is invalid.</exception>
        Task<List<TenantAudience>> GetTenantAudiencesByTenantIdAsync(string tenantId, string clientIdFromHeader);
    }
}
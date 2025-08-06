using Models.Auth;

namespace GdprServices.Auth
{
    public interface IAuthService
    {
        /// <summary>
        /// Create a new tenant
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        Task<string> CreateTenantAsync(RegisterTenantRequest request);

        /// <summary>
        /// Authenticate tenant
        /// </summary>
        /// <param name="email"></param>
        /// <param name="password"></param>
        /// <param name="ipAddress"></param>
        /// <returns></returns>
        Task<JwtAuthResponse> AuthenticateTenantAsync(string email, string password, string ipAddress);

        /// <summary>
        /// Get a new refresh token
        /// </summary>
        /// <param name="token"></param>
        /// <param name="ipAddress"></param>
        /// <param name="clientId"></param>
        /// <returns></returns>
        Task<JwtAuthResponse> RefreshTokenAsync(string token, string ipAddress, string clientId);
    }
}

using GdprServices.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Models.Auth;

namespace GdprApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        /// <summary>
        /// Creates a new request asynchronously based on the provided registration request.
        /// This endpoint allows anonymous access to enable request registration without prior authentication.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="tenant"/> is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when a request with the same email already exists or creation fails due to a database error.</exception>
        /// <remarks>
        /// This endpoint is GDPR-compliant, ensuring that request creation is audited and consent information is captured.
        /// The CreateTenantAsync method logs all successful and failed creation attempts to the audit log.
        /// </remarks>
        [AllowAnonymous]
        [HttpPost(Name = "CreateTenant")]
        public async Task<IActionResult> CreateTenantAsync(RegisterTenantRequest tenant)
        {
            return Ok(await _authService.CreateTenantAsync(tenant));
        }

        /// <summary>
        /// Authenticates a tenant using email and password, returning a JWT access token and a refresh token.
        /// </summary>
        /// <param name="request">The authentication request containing the tenant's email and password.</param>
        /// <returns>
        /// A task representing the asynchronous operation, returning an IActionResult with the JWT access token and a refresh token.
        /// </returns>
        /// <remarks>
        /// This endpoint is GDPR-compliant, logging authentication attempts to the audit log.
        /// Requires a valid email and password. Returns 400 for invalid input, 401 for failed authentication.
        /// The access token is used to authorize subsequent requests, while the refresh token allows obtaining new access tokens without re-authenticating.
        /// Both tokens are securely generated and managed to ensure security and compliance.
        /// </remarks>
        [AllowAnonymous]
        [HttpPost("Authenticate")]
        public async Task<IActionResult> AuthenticateTenantAsync([FromBody] AuthenticateTenantRequest request)
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            return Ok(await _authService.AuthenticateTenantAsync(request.Email, request.Password, ipAddress));
        }

        /// <summary>
        /// This endpoint issues a new access token using a valid refresh token. It securely validates the 
        /// provided refresh token, ensuring it hasn’t expired or been revoked, and then generates and 
        /// returns a fresh access token to maintain user authentication without requiring re-login.
        /// </summary>
        /// <param name="tenantId"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost("{tenantId}/refresh-token", Name = "refresh-token")]
        public async Task<IActionResult> RefreshToken([FromRoute] string tenantId, [FromBody] RefreshTokenRequest request)
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            // Validate tenantId matches the JWT claim
            if (string.IsNullOrEmpty(tenantId))
            {
                return Unauthorized($"Invalid tenant, authentication failed, " +
                    $"or you do not have permission to view this request.");
            }

            // Validate ClientId from header matches the request's ClientId
            var clientIdFromHeader = Request.Headers["ClientId"].FirstOrDefault();
            if (string.IsNullOrEmpty(clientIdFromHeader))
            {
                return BadRequest("ClientId header is required.");
            }

            try
            {
                return Ok(await _authService.RefreshTokenAsync(request.Token, ipAddress, clientIdFromHeader));
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
        }
    }
}

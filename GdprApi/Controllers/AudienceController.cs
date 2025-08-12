using GdprServices.Audience;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Models.Tenants;

namespace GdprApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AudienceController : ControllerBase
    {
        private readonly ITenantAudience _tenantService;

        public AudienceController(ITenantAudience audienceSevice)
        {
            _tenantService = audienceSevice;
        }

        /// <summary>
        /// Saves tenant audience data, including arbitrary details with nested objects, in a GDPR-compliant manner.
        /// This endpoint supports GDPR Article 6 (lawful basis) by ensuring tenant consent and auditing actions.
        /// </summary>
        /// <param name="tenantId">The ID of the tenant associated with the audience data.</param>
        /// <param name="tenantAudience">The tenant audience data, including a dictionary of details.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="tenantId"/> or <paramref name="tenantAudience"/> is null or empty.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the tenant is not found, consent is missing, or saving fails.</exception>
        /// <exception cref="UnauthorizedAccessException">Thrown when client ID is invalid.</exception>
        /// <remarks>
        /// This endpoint is GDPR-compliant, ensuring that audience data storage is audited and restricted to authorized users.
        /// The SaveTenantAudienceAsync method logs all save attempts to the audit log.
        /// Requires a valid JWT with a tenantId claim matching the requested tenant.
        /// </remarks>
        [Authorize(Policy = "TenantAccessWithClientId")]
        [HttpPost("{tenantId}/audience", Name = "SaveTenantAudience")]
        public async Task<IActionResult> SaveTenantAudienceAsync([FromRoute] string tenantId, [FromBody] TenantAudience tenantAudience)
        {
            // Validate tenantId matches the JWT claim
            var userTenantId = User.FindFirst("tenantId")?.Value;
            if (string.IsNullOrEmpty(userTenantId) || userTenantId != tenantId)
            {
                return Unauthorized($"Invalid tenant, authentication failed, or you do not have permission to save audience data for this tenant.");
            }

            // Validate tenantAudience and TenantId
            if (tenantAudience == null || string.IsNullOrEmpty(tenantAudience.TenantId))
            {
                return BadRequest("TenantAudience and TenantId cannot be null or empty.");
            }

            // Ensure tenantId from route matches tenantAudience.TenantId
            if (tenantId != tenantAudience.TenantId)
            {
                return BadRequest("TenantId in route must match TenantId in request body.");
            }

            // Validate ClientId from header
            var clientIdFromHeader = Request.Headers["ClientId"].FirstOrDefault();
            if (string.IsNullOrEmpty(clientIdFromHeader))
            {
                return BadRequest("ClientId header is required.");
            }

            try
            {
                var response = await _tenantService.SaveTenantAudienceAsync(tenantAudience, clientIdFromHeader);
                return Ok(response);
            }
            catch (ArgumentNullException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(ex.Message);
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized("Invalid ClientId.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, "An unexpected error occurred while saving tenant audience data.");
            }
        }

        /// <summary>
        /// Retrieves all tenant audience records for a given TenantId, supporting GDPR Article 15 (right of access). Example:
        /// curl -X GET "api/audience/1234567890abcdef/audience?pageNumber=2&pageSize=10"
        /// </summary>
        /// <param name="tenantId">The ID of the tenant whose audience data is to be retrieved.</param>
        /// <param name="pageNumber">Page number.</param>
        /// <param name="pageSize">Page size.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="tenantId"/> is null or empty.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the tenant is not found or consent is missing.</exception>
        /// <exception cref="UnauthorizedAccessException">Thrown when client ID is invalid.</exception>
        /// <remarks>
        /// This endpoint is GDPR-compliant, ensuring data access is audited and restricted to authorized users.
        /// The GetTenantAudiencesByTenantIdAsync method logs all access attempts to the audit log.
        /// Requires a valid JWT with a tenantId claim matching the requested tenant.
        /// </remarks>
        [Authorize(Policy = "TenantAccessWithClientId")]
        [HttpGet("{tenantId}/audience", Name = "GetTenantAudiencesByTenantId")]
        public async Task<IActionResult> GetTenantAudiencesByTenantIdAsync(
            [FromRoute] string tenantId,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20)
        {
            // Validate tenantId matches the JWT claim
            var userTenantId = User.FindFirst("tenantId")?.Value;
            if (string.IsNullOrEmpty(userTenantId) || userTenantId != tenantId)
            {
                return Unauthorized($"Invalid tenant, authentication failed, or you do not have permission to access this tenant's audience data.");
            }

            // Validate ClientId from header
            var clientIdFromHeader = Request.Headers["ClientId"].FirstOrDefault();
            if (string.IsNullOrEmpty(clientIdFromHeader))
            {
                return BadRequest("ClientId header is required.");
            }

            try
            {
                var response = await _tenantService.GetTenantAudiencesByTenantIdAsync(
                    tenantId,
                    clientIdFromHeader,
                    pageNumber,
                    pageSize);
                return Ok(response);
            }
            catch (ArgumentNullException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(ex.Message);
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized("Invalid ClientId.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, "An unexpected error occurred while retrieving tenant audience data.");
            }
        }
    }
}

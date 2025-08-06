using GdprServices.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Models.Tenants;
using System.ComponentModel.DataAnnotations;

namespace GdprApi.Controllers
{
    /// <summary>
    /// Controller for managing request-related operations in a GDPR-compliant multi-request system.
    /// Provides endpoints for request creation and other request management functionalities.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class TenantController : ControllerBase
    {
        private readonly ITenantService _tenantService;

        /// <summary>
        /// Initializes a new instance of the <see cref="TenantController"/> class.
        /// </summary>
        /// <param name="tenantService">The request service used to handle request-related operations.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="tenantService"/> is null.</exception>
        public TenantController(ITenantService tenantService)
        {
            _tenantService = tenantService ?? throw new ArgumentNullException(nameof(tenantService), "Tenant service cannot be null.");
        }

        /// <summary>
        /// Updates asynchronously based on the provided registration request.
        /// This endpoint allows to update tenant information.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is null.</exception>
        /// <exception cref="ValidationException">Thrown when the request request fails validation (e.g., mismatched passwords).</exception>
        /// <exception cref="InvalidOperationException">Thrown when a request with the same email already exists or creation fails due to a database error.</exception>
        /// <remarks>
        /// This endpoint is GDPR-compliant, ensuring that request creation is audited and consent information is captured.
        /// The CreateTenantAsync method logs all successful and failed creation attempts to the audit log.
        /// </remarks>
        [Authorize(Policy = "TenantAccessWithClientId")]
        [HttpPut("{tenantId}", Name = "UpdateTenant")]
        public async Task<IActionResult> UpdateTenantAsync([FromRoute] string tenantId, UpdateTenantRequest request)
        {
            // Validate tenantId matches the JWT claim
            var userTenantId = User.FindFirst("tenantId")?.Value;
            if (string.IsNullOrEmpty(userTenantId) || userTenantId != tenantId)
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

            var response = await _tenantService.UpdateTenantAsync(tenantId, request, clientIdFromHeader);
            return Ok(response);
        }

        /// <summary>
        /// Retrieves request data by ID, returning original (unpseudonymized) values for authorized users.
        /// This endpoint supports GDPR Article 15 (right of access) by providing access to personal data.
        /// </summary>
        /// <param name="tenantId">The ID of the request to retrieve.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="tenantId"/> is null or empty.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the request is not found.</exception>
        /// <remarks>
        /// This endpoint is GDPR-compliant, ensuring that data access is audited and restricted to authorized users.
        /// The GetTenantDataAsync method logs all access attempts to the audit log.
        /// Requires a valid JWT with a tenantId claim matching the requested request.
        /// </remarks>
        [Authorize(Policy = "TenantAccessWithClientId")]
        [HttpGet("{tenantId}", Name = "GetTenantData")]
        public async Task<IActionResult> GetTenantDataAsync([FromRoute] string tenantId)
        {
            // Validate tenantId matches the JWT claim
            var userTenantId = User.FindFirst("tenantId")?.Value;
            if (string.IsNullOrEmpty(userTenantId) || userTenantId != tenantId)
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

            var response = await _tenantService.GetTenantDataAsync(tenantId, clientIdFromHeader);
            return Ok(response);
        }

        /// <summary>
        /// Downloads tenant data in the specified format (JSON or CSV) for GDPR-compliant data portability (Article 20).
        /// </summary>
        /// <param name="tenantId">The ID of the tenant whose data is to be downloaded.</param>
        /// <param name="format">The desired output format ("JSON" or "CSV").</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="tenantId"/> or <paramref name="format"/> is null or empty.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="format"/> is not "JSON" or "CSV".</exception>
        /// <exception cref="InvalidOperationException">Thrown when the tenant is not found or formatting fails.</exception>
        /// <exception cref="UnauthorizedAccessException">Thrown when client ID is invalid.</exception>
        /// <remarks>
        /// This endpoint is GDPR-compliant, ensuring that data downloads are audited and restricted to authorized users.
        /// The DownloadTenantDataAsync method logs all download attempts to the audit log.
        /// Requires a valid JWT with a tenantId claim matching the requested tenant.
        /// Returns the data as a file with appropriate Content-Type and Content-Disposition headers.
        /// </remarks>
        [Authorize(Policy = "TenantAccessWithClientId")]
        [HttpGet("{tenantId}/download", Name = "DownloadTenantData")]
        public async Task<IActionResult> DownloadTenantDataAsync([FromRoute] string tenantId, [FromQuery] string format)
        {
            // Validate tenantId matches the JWT claim
            var userTenantId = User.FindFirst("tenantId")?.Value;
            if (string.IsNullOrEmpty(userTenantId) || userTenantId != tenantId)
            {
                return Unauthorized($"Invalid tenant, authentication failed, " +
                    $"or you do not have permission to download this tenant's data.");
            }

            // Validate ClientId from header matches the tenant's ClientId
            var clientIdFromHeader = Request.Headers["ClientId"].FirstOrDefault();
            if (string.IsNullOrEmpty(clientIdFromHeader))
            {
                return BadRequest("ClientId header is required.");
            }

            // Validate format parameter
            if (string.IsNullOrEmpty(format))
            {
                return BadRequest("Format query parameter is required.");
            }

            var normalizedFormat = format.ToUpperInvariant();
            if (normalizedFormat != "JSON" && normalizedFormat != "CSV")
            {
                return BadRequest("Format must be 'JSON' or 'CSV'.");
            }

            try
            {
                var data = await _tenantService.DownloadTenantDataAsync(tenantId, clientIdFromHeader, normalizedFormat);

                // Set content type and file name based on format
                var contentType = normalizedFormat == "JSON" ? "application/json" : "text/csv";
                var fileExtension = normalizedFormat == "JSON" ? "json" : "csv";
                var fileName = $"tenant-data-{tenantId}-{DateTime.UtcNow:yyyyMMddHHmmss}.{fileExtension}";

                // Return file with Content-Disposition header to trigger download
                return File(System.Text.Encoding.UTF8.GetBytes(data), contentType, fileName);
            }
            catch (ArgumentNullException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized("Invalid ClientId.");
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, "An unexpected error occurred while downloading tenant data.");
            }
        }
    }
}
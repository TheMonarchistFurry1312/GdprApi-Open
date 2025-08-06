#nullable enable
using System.ComponentModel.DataAnnotations;

namespace Models.Auth
{
    public class RegisterTenantRequest
    {
        [Required]
        public string Email { get; set; } = default!;

        [Required]
        public string Password { get; set; } = default!;

        [Required]
        public string ConfirmPassword { get; set; } = default!;

        /// <summary>
        /// Name of the Tenant
        /// </summary>
        [Required]
        public required string TenantName { get; set; }

        [Required]
        public required string UserName { get; set; }

        [Required]
        public required string FullName { get; set; }

        /// <summary>
        /// Application of service (SaaS) that will implement the GDPR Api in prod
        /// </summary>
        public string? WebsiteUrl { get; set; }
        public bool ConsentAccepted { get; set; }
    }
}
#nullable disable
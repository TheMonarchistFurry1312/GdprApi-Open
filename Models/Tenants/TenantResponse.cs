using Models.Enums;

namespace Models.Tenants
{
    /// <summary>
    /// Response model for retrieving tenant data with original (unpseudonymized) values.
    /// </summary>
    public class TenantResponse
    {
        public string Id { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string UserName { get; set; } = null!;
        public AccountType AccountType { get; set; }
        public UserRole Role { get; set; }
        public bool EmailConfirmed { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public string? WebsiteUrl { get; set; }
        public string? AccountRequestId { get; set; }
        public bool ConsentAccepted { get; set; }
        public DateTime ConsentAcceptedUtcDate { get; set; }
        public DateTime? RetentionExpiryUtc { get; set; }
    }
}

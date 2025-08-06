namespace Models.Auth
{
    public class AuthenticateTenantRequest
    {
        public string Email { get; set; } = null!;
        public string Password { get; set; } = null!;
    }
}

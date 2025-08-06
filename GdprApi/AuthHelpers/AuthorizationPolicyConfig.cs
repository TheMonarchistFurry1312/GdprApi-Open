namespace GdprApi.AuthHelpers
{
    public static class AuthorizationPolicyConfig
    {
        public static void AddTenantPolicies(this IServiceCollection services)
        {
            services.AddAuthorization(options =>
            {
                options.AddPolicy("TenantAccessWithClientId", policy =>
                {
                    policy.RequireAuthenticatedUser();
                    policy.RequireClaim("tenantId");
                });
            });
        }
    }
}

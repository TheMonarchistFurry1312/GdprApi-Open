using Microsoft.IdentityModel.Tokens;
using Models.Tenants;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace GdprConfigurations
{
    public static class JwtGenerator
    {
        public static string GenerateTenantToken(string tokenKey, string email, Tenant tenant, int hours)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(tokenKey); // Replace with secure key (e.g., from config or Key Vault)
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, tenant.Id),
                    new Claim(ClaimTypes.Email, email), // Original email
                    new Claim(ClaimTypes.Name, tenant.UserName),
                    new Claim(ClaimTypes.Role, tenant.Role.ToString()),
                    new Claim("tenantId", tenant.Id),
                    new Claim("accountType", tenant.AccountType.ToString())
                }),
                Expires = DateTime.UtcNow.AddHours(hours),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
    }
}

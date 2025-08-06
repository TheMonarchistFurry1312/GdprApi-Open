namespace GdprApi.LicenseHelpers
{
    public class LicenseValidatorMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _env;

        public LicenseValidatorMiddleware(
            RequestDelegate next,
            IConfiguration configuration,
            IWebHostEnvironment env)
        {
            _next = next;
            _configuration = configuration;
            _env = env;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (_env.IsProduction())
            {
                var licenseKey = _configuration["LicenseSettings:LicenseKey"];
                if (string.IsNullOrWhiteSpace(licenseKey) || !IsValidLicense(licenseKey))
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await context.Response.WriteAsync("Invalid or missing license. Cannot be used in production without a valid license.");
                    return;
                }
            }

            await _next(context);
        }

        private bool IsValidLicense(string licenseKey)
        {
            // Aquí pones tu lógica real para validar licencia, ej:
            //  - Verificar formato
            //  - Validar firma digital
            //  - Verificar expiración
            // Por ahora puedes dejarlo true para local.
            return true;
        }
    }
}

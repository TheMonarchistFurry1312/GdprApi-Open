using GdprConfigurations;
using GdprServices.AuditLogs;
using GdprServices.Users;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Models.AuditLog;
using Models.Auth;
using Models.Enums;
using Models.Tenants;
using MongoDB.Bson;
using Repositories.Interfaces;
using System.ComponentModel.DataAnnotations;
using System.Net.Mail;
using System.Security.Cryptography;

namespace GdprServices.Auth
{
    public class AuthService : IAuthService
    {
        private readonly ITenantRepository _repository;
        private readonly IAuditLogs _auditLogs;
        private readonly ILogger<TenantService> _logger;
        private readonly IConfiguration _configuration;
        private static readonly string Base64Key = "ASNFZ4mrze/+3LqYdlQyEBEiM0RVV2aHiZqrzN3u/wA="; // Store securely
        private static readonly string Base64MacKey = "SGVsbG8gV29ybGQgSGVsbG8gV29ybGQgSGVsbG8gV29ybGQ="; // Store securely
        private readonly byte[] EncryptionKey;
        private readonly string _tokenKey;
        private readonly int _accessTokenExpirationMinutes;
        private readonly int _refreshTokenExpirationMinutes;
        private readonly byte[] MacKey;

        public AuthService(
            ITenantRepository repository,
            IAuditLogs auditLogs,
            ILogger<TenantService> logger,
            IConfiguration configuration)
        {
            _repository = repository;
            _auditLogs = auditLogs;
            _logger = logger;
            _configuration = configuration;
            _tokenKey = _configuration["AppSettings:Token"]!;
            _accessTokenExpirationMinutes = int.Parse(_configuration["AppSettings:AccessTokenExpirationMinutes"] ?? "15");
            _refreshTokenExpirationMinutes = int.Parse(_configuration["AppSettings:RefreshTokenExpirationMinutes"] ?? "30");
            EncryptionKey = InitializeEncryptionKey();
            MacKey = Convert.FromBase64String(Base64MacKey);
        }

        public async Task<string> CreateTenantAsync(RegisterTenantRequest request)
        {
            // Validate input
            if (request == null)
            {
                await LogAuditAsync(
                    tenantId: null,
                    performedBy: null,
                    actorType: ActorType.Anonymous,
                    actionType: AuditActionType.Create,
                    targetEntity: TargetEntityType.Tenant,
                    targetEntityId: null,
                    details: new Dictionary<string, object> { { "Error", "CreateTenantAsync called with null request." } },
                    isSuccess: false,
                    logErrorContext: "tenant creation"
                );
                _logger.LogWarning("CreateTenantAsync called with null request.");
                throw new ArgumentNullException(nameof(request), "Tenant registration request cannot be null.");
            }

            var validationContext = new ValidationContext(request);
            try
            {
                Validator.ValidateObject(request, validationContext, validateAllProperties: true);
                if (!IsValidEmail(request.Email))
                {
                    throw new ArgumentNullException(nameof(request), "Email does not have correct format");
                }
            }
            catch (ValidationException ex)
            {
                await LogAuditAsync(
                    tenantId: null,
                    performedBy: request.Email,
                    actorType: ActorType.System,
                    actionType: AuditActionType.Create,
                    targetEntity: TargetEntityType.Tenant,
                    targetEntityId: null,
                    details: new Dictionary<string, object> { { "Error", $"Validation failed: {ex.Message}" } },
                    isSuccess: false,
                    logErrorContext: "tenant creation"
                );
                _logger.LogWarning("Validation failed for email: {Email}, Error: {Error}", request.Email, ex.Message);
                throw;
            }

            // Validate password confirmation
            if (request.Password != request.ConfirmPassword)
            {
                await LogAuditAsync(
                    tenantId: null,
                    performedBy: request.Email,
                    actorType: ActorType.System,
                    actionType: AuditActionType.Create,
                    targetEntity: TargetEntityType.Tenant,
                    targetEntityId: null,
                    details: new Dictionary<string, object> { { "Error", "Password and ConfirmPassword do not match." } },
                    isSuccess: false,
                    logErrorContext: "tenant creation"
                );
                _logger.LogWarning("Password and ConfirmPassword do not match for email: {Email}", request.Email);
                throw new ValidationException("Passwords do not match.");
            }

            var hashedEmail = EncryptionProvider.HashString(request.Email);
            if (await _repository.ExistsByEmailAsync(hashedEmail))
            {
                await LogAuditAsync(
                    tenantId: null,
                    performedBy: request.Email,
                    actorType: ActorType.Anonymous,
                    actionType: AuditActionType.Create,
                    targetEntity: TargetEntityType.Tenant,
                    targetEntityId: null,
                    details: new Dictionary<string, object> { { "Error", "A tenant with this email already exists." } },
                    isSuccess: false,
                    logErrorContext: "tenant creation"
                );
                _logger.LogWarning("Attempt to create tenant with existing email: {Email}", request.Email);
                throw new InvalidOperationException("A tenant with this email already exists.");
            }

            PasswordHash.CreatePasswordHash(request.Password, out byte[] passwordHash, out byte[] passwordSalt);

            var tenant = new Tenant
            {
                Id = ObjectId.GenerateNewId().ToString(),
                FullName = EncryptionProvider.HashString(request.FullName),
                Email = hashedEmail,
                PasswordHash = passwordHash,
                PasswordSalt = passwordSalt,
                UserName = request.UserName,
                AccountType = AccountType.Basic,
                Role = UserRole.Owner,
                EmailConfirmed = false,
                CreatedAtUtc = DateTime.UtcNow,
                AccountRequestId = Guid.NewGuid().ToString(),
                WebsiteUrl = request.WebsiteUrl,
                ConsentAccepted = request.ConsentAccepted,
                ConsentAcceptedUtcDate = DateTime.UtcNow,
                RetentionExpiryUtc = DateTime.UtcNow.AddYears(5),
                ClientId = Guid.NewGuid().ToString("N")
            };

            var fullNameEncrypted = EncryptionProvider.EncryptString(request.FullName, EncryptionKey);
            var emailEncrypted = EncryptionProvider.EncryptString(request.Email, EncryptionKey);

            var mappings = new[]
            {
                new PseudonymMapping
                {
                    Id = ObjectId.GenerateNewId().ToString(),
                    TenantId = tenant.Id,
                    HashedValue = tenant.FullName,
                    EncryptedOriginalValue = fullNameEncrypted,
                    FieldType = "FullName",
                    RetentionExpiryUtc = tenant.RetentionExpiryUtc
                },
                new PseudonymMapping
                {
                    Id = ObjectId.GenerateNewId().ToString(),
                    TenantId = tenant.Id,
                    HashedValue = tenant.Email,
                    EncryptedOriginalValue = emailEncrypted,
                    FieldType = "Email",
                    RetentionExpiryUtc = tenant.RetentionExpiryUtc
                }
            };

            try
            {
                await _repository.CreateTenantAsync(tenant, mappings);
                await LogAuditAsync(
                    tenantId: tenant.Id,
                    performedBy: tenant.Email,
                    actorType: ActorType.User,
                    actionType: AuditActionType.Create,
                    targetEntity: TargetEntityType.Tenant,
                    targetEntityId: tenant.Id,
                    details: new Dictionary<string, object> { { "Action", "Tenant created successfully" } },
                    isSuccess: true,
                    logErrorContext: "tenant creation"
                );
                _logger.LogInformation("Tenant created successfully with AccountRequestId: {AccountRequestId}, Email: {Email}", tenant.AccountRequestId, request.Email);
                return $"Tenant request created successfully with ID: {tenant.AccountRequestId}";
            }
            catch (InvalidOperationException ex)
            {
                await LogAuditAsync(
                    tenantId: tenant?.Id,
                    performedBy: request.Email,
                    actorType: ActorType.System,
                    actionType: AuditActionType.Create,
                    targetEntity: TargetEntityType.Tenant,
                    targetEntityId: tenant?.Id,
                    details: new Dictionary<string, object> { { "Error", $"Failed to create tenant: {ex.Message}" } },
                    isSuccess: false,
                    logErrorContext: "tenant creation"
                );
                _logger.LogError(ex, "Failed to create tenant for email: {Email}", request.Email);
                throw;
            }
            catch (Exception ex)
            {
                await LogAuditAsync(
                    tenantId: tenant?.Id,
                    performedBy: request.Email,
                    actorType: ActorType.System,
                    actionType: AuditActionType.Create,
                    targetEntity: TargetEntityType.Tenant,
                    targetEntityId: tenant?.Id,
                    details: new Dictionary<string, object> { { "Error", $"Unexpected error during tenant creation: {ex.Message}" } },
                    isSuccess: false,
                    logErrorContext: "tenant creation"
                );
                _logger.LogError(ex, "Unexpected error during tenant creation for email: {Email}", request.Email);
                throw new InvalidOperationException("An unexpected error occurred while creating the tenant.", ex);
            }
        }

        public async Task<JwtAuthResponse> AuthenticateTenantAsync(
            string email,
            string password,
            string ipAddress)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                await LogAuditAsync(
                    tenantId: null,
                    performedBy: email,
                    actorType: ActorType.System,
                    actionType: AuditActionType.Authentication,
                    targetEntity: TargetEntityType.Tenant,
                    targetEntityId: null,
                    details: new Dictionary<string, object> { { "Error", "AuthenticateTenantAsync called with null or empty email/password." } },
                    isSuccess: false,
                    logErrorContext: "tenant authentication"
                );
                _logger.LogWarning("AuthenticateTenantAsync called with null or empty email/password.");
                throw new ArgumentNullException("Email and password cannot be null or empty.");
            }

            var hashedEmail = EncryptionProvider.HashString(email);
            var tenant = await _repository.GetByEmailAsync(hashedEmail);

            // Retrieve the pseudonym mapping for the email
            var emailMapping = await _repository.GetPseudonymMappingByTenantIdAndFieldTypeAsync(tenant.Id);
            if (emailMapping == null)
            {
                throw new InvalidOperationException("Pseudonym mapping for email not found.");
            }

            try
            {
                // Decrypt and verify authenticity using AES-GCM
                string originalEmail = EncryptionProvider.DecryptString(emailMapping.EncryptedOriginalValue, EncryptionKey);
                if (originalEmail != email)
                {
                    await LogAuditAsync(
                        tenantId: tenant.Id,
                        performedBy: email,
                        actorType: ActorType.Anonymous,
                        actionType: AuditActionType.Authentication,
                        targetEntity: TargetEntityType.Tenant,
                        targetEntityId: tenant.Id,
                        details: new Dictionary<string, object> { { "Error", "Decrypted email does not match input email." } },
                        isSuccess: false,
                        logErrorContext: "tenant authentication"
                    );
                    _logger.LogWarning("Decrypted email does not match input email, TenantId: {TenantId}", tenant.Id);
                    throw new CryptographicException("Decrypted email does not match input email.");
                }
            }
            catch (CryptographicException ex)
            {
                await LogAuditAsync(
                    tenantId: tenant.Id,
                    performedBy: email,
                    actorType: ActorType.Anonymous,
                    actionType: AuditActionType.Authentication,
                    targetEntity: TargetEntityType.Tenant,
                    targetEntityId: tenant.Id,
                    details: new Dictionary<string, object> { { "Error", "AES-GCM decryption failed: Data may have been tampered with." } },
                    isSuccess: false,
                    logErrorContext: "tenant authentication"
                );
                _logger.LogWarning("AES-GCM decryption failed for email mapping, TenantId: {TenantId}", tenant.Id);
                throw new CryptographicException("AES-GCM decryption failed. Data may have been tampered with.", ex);
            }

            // Optionally, decrypt the original email if needed
            // string originalEmail = EncryptionProvider.DecryptString(emailMapping.EncryptedOriginalValue, EncryptionKey);

            if (tenant == null || !PasswordHash.VerifyPassword(password, tenant.PasswordHash, tenant.PasswordSalt))
            {
                await LogAuditAsync(
                    tenantId: null,
                    performedBy: email,
                    actorType: ActorType.User,
                    actionType: AuditActionType.Authentication,
                    targetEntity: TargetEntityType.Tenant,
                    targetEntityId: null,
                    details: new Dictionary<string, object> { { "Error", "Authentication failed: Invalid email or password." } },
                    isSuccess: false,
                    logErrorContext: "tenant authentication"
                );
                _logger.LogWarning("Authentication failed for email: {Email}", email);
                throw new InvalidOperationException("Invalid email or password.");
            }

            var accessToken = JwtGenerator.GenerateTenantToken(_tokenKey, email, tenant, _accessTokenExpirationMinutes);

            var refreshTokenValue = GenerateRefreshToken();
            var refreshToken = new RefreshToken
            {
                TenantId = tenant.Id,
                Token = refreshTokenValue,
                CreatedAtUtc = DateTime.UtcNow,
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(_refreshTokenExpirationMinutes),
                CreatedByIp = ipAddress
            };

            await _repository.CreateRefreshTokenAsync(refreshToken);

            await LogAuditAsync(
                    tenantId: tenant.Id,
                    performedBy: email,
                    actorType: ActorType.User,
                    actionType: AuditActionType.Authentication,
                    targetEntity: TargetEntityType.Tenant,
                    targetEntityId: null,
                    details: new Dictionary<string, object> { { "Action", $"User authenticated successfully from IP: {ipAddress}" } },
                    isSuccess: false,
                    logErrorContext: "tenant authentication");

            return new JwtAuthResponse
            {
                Token = accessToken,
                RefreshToken = refreshTokenValue
            };
        }

        public async Task<JwtAuthResponse> RefreshTokenAsync(string token, string ipAddress, string clientId)
        {
            var refreshToken = await _repository.GetRefreshTokenAsync(token);

            if (refreshToken == null || refreshToken.IsRevoked || refreshToken.ExpiresAtUtc <= DateTime.UtcNow)
            {
                throw new UnauthorizedAccessException("Invalid or expired refresh token.");
            }

            var tenant = await _repository.GetByIdAsync(refreshToken.TenantId);

            if (tenant == null)
            {
                await LogAuditAsync(
                    tenantId: null,
                    performedBy: null,
                    actorType: ActorType.Anonymous,
                    actionType: AuditActionType.Authentication,
                    targetEntity: TargetEntityType.Tenant,
                    targetEntityId: null,
                    details: new Dictionary<string, object> { { "Error", "Tenant not found." } },
                    isSuccess: false,
                    logErrorContext: "tenant authentication"
                );
                throw new InvalidOperationException("Tenant not found.");
            }

            if (tenant.ClientId != clientId)
            {
                await LogAuditAsync(
                    tenantId: tenant.Id,
                    performedBy: null,
                    actorType: ActorType.Anonymous,
                    actionType: AuditActionType.Authentication,
                    targetEntity: TargetEntityType.Tenant,
                    targetEntityId: tenant.Id,
                    details: new Dictionary<string, object> { { "Error", $"Unauthorized access: Invalid ClientId {clientId}." } },
                    isSuccess: false,
                    logErrorContext: "tenant authentication"
                );
                throw new UnauthorizedAccessException("Invalid ClientId.");
            }

            var newAccessToken = JwtGenerator.GenerateTenantToken(_tokenKey, tenant.Email, tenant, _accessTokenExpirationMinutes);
            var newRefreshTokenValue = GenerateRefreshToken();

            var newRefreshToken = new RefreshToken
            {
                TenantId = tenant.Id,
                Token = newRefreshTokenValue,
                CreatedAtUtc = DateTime.UtcNow,
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(_refreshTokenExpirationMinutes),
                CreatedByIp = ipAddress
            };

            await _repository.UpdateRefreshTokenAsync(token, newRefreshToken);
            await _repository.CreateRefreshTokenAsync(newRefreshToken);

            await LogAuditAsync(
                tenantId: tenant.Id,
                performedBy: tenant.Email,
                actorType: ActorType.User,
                actionType: AuditActionType.Authentication,
                targetEntity: TargetEntityType.Tenant,
                targetEntityId: tenant.Id,
                details: new Dictionary<string, object> { { "Action", "Tenant authenticated successfully using the refresh token" } },
                isSuccess: true,
                logErrorContext: "tenant authentication"
            );
            _logger.LogInformation("Tenant authenticated successfully, TenantId: {TenantId}", tenant.Id);

            return new JwtAuthResponse
            {
                Token = newAccessToken,
                RefreshToken = newRefreshTokenValue
            };
        }

        private async Task LogAuditAsync(
            string tenantId,
            string performedBy,
            ActorType actorType,
            AuditActionType actionType,
            TargetEntityType targetEntity,
            string targetEntityId,
            Dictionary<string, object> details,
            bool isSuccess,
            string logErrorContext)
        {
            var hashedPerformedBy = performedBy != null && actorType != ActorType.User
                ? EncryptionProvider.HashString(performedBy)
                : performedBy ?? "System";

            var auditLog = new AuditLog
            {
                Id = ObjectId.GenerateNewId().ToString(),
                TenantId = tenantId ?? "Unknown",
                PerformedBy = hashedPerformedBy,
                ActorType = actorType,
                ActionType = actionType,
                TargetEntity = targetEntity,
                TargetEntityId = targetEntityId,
                TimestampUtc = DateTime.UtcNow,
                ClientIpAddress = null,
                DeviceType = null,
                Details = details,
                IsGdprRelevant = true,
                RetentionExpiryUtc = DateTime.UtcNow.AddYears(5),
                CorrelationId = Guid.NewGuid().ToString(),
                IsSuccess = isSuccess
            };
            auditLog.ComputeIntegrityHash();

            try
            {
                await _auditLogs.CreateAsync(auditLog);
                if (isSuccess)
                {
                    _logger.LogInformation("{Action}, TenantId: {TenantId}", details["Action"], tenantId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log audit for {Context}, TenantId: {TenantId}", logErrorContext, tenantId);
            }
        }

        private byte[] InitializeEncryptionKey()
        {
            try
            {
                var keyBytes = Convert.FromBase64String(Base64Key);
                if (keyBytes.Length != 32)
                {
                    _logger.LogError("Invalid encryption key size: {KeyLength} bytes. Expected 32 bytes for AES-256.", keyBytes.Length);
                    throw new CryptographicException("Encryption key must be 32 bytes for AES-256.");
                }
                return keyBytes;
            }
            catch (FormatException ex)
            {
                _logger.LogError(ex, "Invalid Base64 format for encryption key.");
                throw new CryptographicException("Invalid Base64 format for encryption key.", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize encryption key.");
                throw new CryptographicException("Failed to initialize encryption key.", ex);
            }
        }

        private static bool IsValidEmail(string email)
        {
            try
            {
                var mailAddress = new MailAddress(email);
                return true;
            }
            catch (FormatException ex)
            {
                throw new ArgumentException(ex.Message);
            }
        }

        private static string GenerateRefreshToken()
        {
            var randomBytes = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomBytes);
            return Convert.ToBase64String(randomBytes);
        }
    }
}
using GdprConfigurations;
using GdprServices.AuditLogs;
using GdprServices.Auth;
using GdprServices.Users;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Models.Auth;
using Models.Tenants;
using Moq;
using Repositories.Interfaces;
using System.Security.Cryptography;

namespace UnitTests.Services
{
    [TestFixture]
    public class AuthServiceTests
    {
        private Mock<ITenantRepository> _repositoryMock;
        private Mock<IAuditLogs> _auditLogsMock;
        private Mock<ILogger<TenantService>> _loggerMock;
        private Mock<IConfiguration> _configurationMock;
        private AuthService _authService;
        private byte[] _encryptionKey;

        [SetUp]
        public void SetUp()
        {
            _repositoryMock = new Mock<ITenantRepository>();
            _auditLogsMock = new Mock<IAuditLogs>();
            _loggerMock = new Mock<ILogger<TenantService>>();
            _configurationMock = new Mock<IConfiguration>();

            // Setup configuration for EncryptionKey (32-byte key for AES-256-GCM)
            _encryptionKey = new byte[32];
            RandomNumberGenerator.Fill(_encryptionKey);
            _configurationMock.Setup(c => c["AppSettings:EncryptionKey"]).Returns(Convert.ToBase64String(_encryptionKey));
            _configurationMock.Setup(c => c["AppSettings:Token"]).Returns("test-token");
            _configurationMock.Setup(c => c["AppSettings:AccessTokenExpirationMinutes"]).Returns("15");
            _configurationMock.Setup(c => c["AppSettings:RefreshTokenExpirationMinutes"]).Returns("30");

            _authService = new AuthService(_repositoryMock.Object, _auditLogsMock.Object, _loggerMock.Object, _configurationMock.Object);
        }

        [Test]
        public async Task AuthenticateTenantAsync_TamperedEncryptedData_ThrowsCryptographicException()
        {
            // Arrange
            string email = "test@example.com";
            string password = "password123";
            string ipAddress = "127.0.0.1";
            string tenantId = "tenant123";

            // Mock tenant
            var tenant = new Tenant
            {
                Id = tenantId,
                Email = EncryptionProvider.HashString(email),
                PasswordHash = new byte[0], // Simplified for test
                PasswordSalt = new byte[0]  // Simplified for test
            };
            _repositoryMock.Setup(r => r.GetByEmailAsync(It.IsAny<string>())).ReturnsAsync(tenant);

            // Mock PasswordHash.VerifyPassword
            PasswordHash.CreatePasswordHash(password, out byte[] passwordHash, out byte[] passwordSalt);
            tenant.PasswordHash = passwordHash;
            tenant.PasswordSalt = passwordSalt;

            // Create valid encrypted email
            byte[] encryptedEmail = EncryptionProvider.EncryptString(email, _encryptionKey);
            var emailMapping = new PseudonymMapping
            {
                Id = "mapping123",
                TenantId = tenantId,
                HashedValue = tenant.Email,
                EncryptedOriginalValue = encryptedEmail,
                FieldType = "Email",
                RetentionExpiryUtc = DateTime.UtcNow.AddYears(5)
            };

            // Tamper with encrypted data (flip a bit in the ciphertext, after nonce [12 bytes] + tag [16 bytes])
            byte[] tamperedEncryptedEmail = (byte[])encryptedEmail.Clone();
            tamperedEncryptedEmail[28] ^= 0xFF; // Modify a byte in the ciphertext

            // Mock repository to return tampered data
            _repositoryMock.Setup(r => r.GetPseudonymMappingByTenantIdAndFieldTypeAsync(tenantId))
                .ReturnsAsync(new PseudonymMapping
                {
                    Id = emailMapping.Id,
                    TenantId = emailMapping.TenantId,
                    HashedValue = emailMapping.HashedValue,
                    EncryptedOriginalValue = tamperedEncryptedEmail,
                    FieldType = emailMapping.FieldType,
                    RetentionExpiryUtc = emailMapping.RetentionExpiryUtc
                });

            // Act & Assert
            var exception = Assert.ThrowsAsync<CryptographicException>(() =>
                _authService.AuthenticateTenantAsync(email, password, ipAddress));
            Assert.That(exception.Message, Is.EqualTo("AES-GCM decryption failed. Data may have been tampered with."));
        }
    }
}
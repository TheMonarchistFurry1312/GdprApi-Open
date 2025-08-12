using GdprConfigurations;
using GdprServices.Audience;
using GdprServices.AuditLogs;
using GdprServices.DataExporter;
using Microsoft.Extensions.Logging;
using Models.AuditLog;
using Models.Enums;
using Models.Tenants;
using MongoDB.Driver;
using Moq;
using Repositories.Interfaces;

namespace UnitTests.Services
{
    [TestFixture]
    public class TenantAudienceServiceTests
    {
        private Mock<ITenantAudienceRepository> _audienceRepositoryMock;
        private Mock<ILogger<TenantAudienceService>> _loggerMock;
        private Mock<IAuditLogs> _auditLogsMock;
        private Mock<IDataFormatter> _dataFormatterMock;
        private TenantAudienceService _tenantAudienceService;
        private readonly byte[] _encryptionKey = Convert.FromBase64String("ASNFZ4mrze/+3LqYdlQyEBEiM0RVV2aHiZqrzN3u/wA=");

        [SetUp]
        public void Setup()
        {
            _audienceRepositoryMock = new Mock<ITenantAudienceRepository>();
            _loggerMock = new Mock<ILogger<TenantAudienceService>>();
            _auditLogsMock = new Mock<IAuditLogs>();
            _dataFormatterMock = new Mock<IDataFormatter>();

            _audienceRepositoryMock.Setup(r => r.CreatePseudonymMappingIndex())
                .Verifiable();

            _tenantAudienceService = new TenantAudienceService(
                _audienceRepositoryMock.Object,
                _loggerMock.Object,
                _auditLogsMock.Object,
                _dataFormatterMock.Object);
        }

        private Tenant CreateTestTenant(string tenantId = "tenant123", string clientId = "client123")
        {
            return new Tenant
            {
                Id = tenantId,
                FullName = EncryptionProvider.HashString("John Doe"),
                Email = EncryptionProvider.HashString("email@example.com"),
                UserName = "testuser",
                ClientId = clientId,
                ConsentAccepted = true,
                ConsentAcceptedUtcDate = DateTime.UtcNow,
                RetentionExpiryUtc = DateTime.UtcNow.AddYears(5),
                AccountRequestId = Guid.NewGuid().ToString(),
                AccountType = AccountType.Basic,
                Role = UserRole.Owner,
                CreatedAtUtc = DateTime.UtcNow,
                WebsiteUrl = "https://example.com"
            };
        }

        private List<TenantAudience> CreateTestAudiences(string tenantId)
        {
            return new List<TenantAudience>
            {
                new TenantAudience
                {
                    Id = "audience123",
                    TenantId = tenantId,
                    Details = new Dictionary<string, object>
                    {
                        { "key", EncryptionProvider.EncryptString(System.Text.Json.JsonSerializer.Serialize("value"), _encryptionKey) }
                    }
                }
            };
        }

        // Tests for SaveTenantAudienceAsync
        [Test]
        public async Task SaveTenantAudienceAsync_ValidInput_SavesAudience()
        {
            // Arrange
            string tenantId = "tenant123";
            string clientId = "client123";
            var audience = new TenantAudience
            {
                Id = "audience123",
                TenantId = tenantId,
                Details = new Dictionary<string, object> { { "key", "value" } }
            };
            var tenant = CreateTestTenant(tenantId, clientId);

            _audienceRepositoryMock.Setup(r => r.GetTenantByIdAsync(It.IsAny<string>()))
                .ReturnsAsync(tenant);
            _audienceRepositoryMock.Setup(r => r.InsertTenantAudienceAsync(It.IsAny<TenantAudience>()))
                .Returns(Task.CompletedTask);
            _auditLogsMock.Setup(a => a.CreateAsync(It.IsAny<AuditLog>()))
                .ReturnsAsync("audit123");

            // Act
            var result = await _tenantAudienceService.SaveTenantAudienceAsync(audience, clientId);

            // Assert
            Assert.AreEqual("Tenant audience data saved successfully.", result);
            _audienceRepositoryMock.Verify(r => r.InsertTenantAudienceAsync(It.Is<TenantAudience>(a =>
                a.Id == "audience123" &&
                a.TenantId == tenantId &&
                a.Details["key"] is byte[])), Times.Once());
            _auditLogsMock.Verify(a => a.CreateAsync(It.Is<AuditLog>(log =>
                log.ActionType == AuditActionType.Create &&
                log.IsSuccess &&
                log.TenantId == tenantId &&
                log.Details["Action"].ToString() == "Tenant audience data saved")), Times.Once());
        }

        [Test]
        public async Task SaveTenantAudienceAsync_NullAudience_ThrowsArgumentNullException()
        {
            // Arrange
            string tenantId = "tenant123";
            string clientId = "client123";
            TenantAudience audience = null;

            _auditLogsMock.Setup(a => a.CreateAsync(It.IsAny<AuditLog>()))
                .ReturnsAsync("audit123");

            // Act & Assert
            var ex = Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await _tenantAudienceService.SaveTenantAudienceAsync(audience, clientId));
            Assert.AreEqual("TenantAudience and TenantId cannot be null or empty. (Parameter 'tenantAudience')", ex.Message);
            _auditLogsMock.Verify(a => a.CreateAsync(It.Is<AuditLog>(log =>
                !log.IsSuccess &&
                log.Details["Error"].ToString().Contains("null or empty TenantId"))), Times.Once());
        }

        [Test]
        public async Task SaveTenantAudienceAsync_NullClientId_ThrowsArgumentNullException()
        {
            // Arrange
            string tenantId = "tenant123";
            string clientId = null;
            var audience = new TenantAudience
            {
                Id = "audience123",
                TenantId = tenantId,
                Details = new Dictionary<string, object> { { "key", "value" } }
            };

            _auditLogsMock.Setup(a => a.CreateAsync(It.IsAny<AuditLog>()))
                .ReturnsAsync("audit123");

            // Act & Assert
            var ex = Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await _tenantAudienceService.SaveTenantAudienceAsync(audience, clientId));
            Assert.AreEqual("ClientId cannot be null or empty. (Parameter 'clientIdFromHeader')", ex.Message);
            _auditLogsMock.Verify(a => a.CreateAsync(It.Is<AuditLog>(log =>
                !log.IsSuccess &&
                log.Details["Error"].ToString().Contains("null or empty ClientId"))), Times.Once());
        }

        [Test]
        public async Task SaveTenantAudienceAsync_TenantNotFound_ThrowsInvalidOperationException()
        {
            // Arrange
            string tenantId = "tenant123";
            string clientId = "client123";
            var audience = new TenantAudience
            {
                Id = "audience123",
                TenantId = tenantId,
                Details = new Dictionary<string, object> { { "key", "value" } }
            };

            _audienceRepositoryMock.Setup(r => r.GetTenantByIdAsync(It.IsAny<string>()))
                .ReturnsAsync((Tenant)null);
            _auditLogsMock.Setup(a => a.CreateAsync(It.IsAny<AuditLog>()))
                .ReturnsAsync("audit123");

            // Act & Assert
            var ex = Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _tenantAudienceService.SaveTenantAudienceAsync(audience, clientId));
            Assert.AreEqual("Tenant not found.", ex.Message);
            _auditLogsMock.Verify(a => a.CreateAsync(It.Is<AuditLog>(log =>
                !log.IsSuccess &&
                log.Details["Error"].ToString().Contains("Tenant not found"))), Times.Once());
        }

        [Test]
        public async Task SaveTenantAudienceAsync_InvalidClientId_ThrowsUnauthorizedAccessException()
        {
            // Arrange
            string tenantId = "tenant123";
            string clientId = "wrongClient";
            var audience = new TenantAudience
            {
                Id = "audience123",
                TenantId = tenantId,
                Details = new Dictionary<string, object> { { "key", "value" } }
            };
            var tenant = CreateTestTenant(tenantId, "client123");

            _audienceRepositoryMock.Setup(r => r.GetTenantByIdAsync(It.IsAny<string>()))
                .ReturnsAsync(tenant);
            _auditLogsMock.Setup(a => a.CreateAsync(It.IsAny<AuditLog>()))
                .ReturnsAsync("audit123");

            // Act & Assert
            var ex = Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
                await _tenantAudienceService.SaveTenantAudienceAsync(audience, clientId));
            Assert.AreEqual("Invalid ClientId.", ex.Message);
            _auditLogsMock.Verify(a => a.CreateAsync(It.Is<AuditLog>(log =>
                !log.IsSuccess &&
                log.Details["Error"].ToString().Contains("Invalid ClientId"))), Times.Once());
        }

        [Test]
        public async Task SaveTenantAudienceAsync_NoConsent_ThrowsInvalidOperationException()
        {
            // Arrange
            string tenantId = "tenant123";
            string clientId = "client123";
            var audience = new TenantAudience
            {
                Id = "audience123",
                TenantId = tenantId,
                Details = new Dictionary<string, object> { { "key", "value" } }
            };
            var tenant = CreateTestTenant(tenantId, clientId);
            tenant.ConsentAccepted = false;

            _audienceRepositoryMock.Setup(r => r.GetTenantByIdAsync(It.IsAny<string>()))
                .ReturnsAsync(tenant);
            _auditLogsMock.Setup(a => a.CreateAsync(It.IsAny<AuditLog>()))
                .ReturnsAsync("audit123");

            // Act & Assert
            var ex = Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _tenantAudienceService.SaveTenantAudienceAsync(audience, clientId));
            Assert.AreEqual("Tenant consent is required for data processing.", ex.Message);
            _auditLogsMock.Verify(a => a.CreateAsync(It.Is<AuditLog>(log =>
                !log.IsSuccess &&
                log.Details["Error"].ToString().Contains("Tenant has not provided consent"))), Times.Once());
        }

        [Test]
        public async Task SaveTenantAudienceAsync_MongoException_ThrowsInvalidOperationException()
        {
            // Arrange
            string tenantId = "tenant123";
            string clientId = "client123";
            var audience = new TenantAudience
            {
                Id = "audience123",
                TenantId = tenantId,
                Details = new Dictionary<string, object> { { "key", "value" } }
            };
            var tenant = CreateTestTenant(tenantId, clientId);

            _audienceRepositoryMock.Setup(r => r.GetTenantByIdAsync(It.IsAny<string>()))
                .ReturnsAsync(tenant);
            _audienceRepositoryMock.Setup(r => r.InsertTenantAudienceAsync(It.IsAny<TenantAudience>()))
                .ThrowsAsync(new MongoException("Database error"));
            _auditLogsMock.Setup(a => a.CreateAsync(It.IsAny<AuditLog>()))
                .ReturnsAsync("audit123");

            // Act & Assert
            var ex = Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _tenantAudienceService.SaveTenantAudienceAsync(audience, clientId));
            Assert.IsTrue(ex.Message.Contains("Failed to save tenant audience data due to database error"));
            _auditLogsMock.Verify(a => a.CreateAsync(It.Is<AuditLog>(log =>
                !log.IsSuccess &&
                log.Details["Error"].ToString().Contains("MongoDB error"))), Times.Once());
        }

        // Tests for GetTenantAudiencesByTenantIdAsync
        [Test]
        public async Task GetTenantAudiencesByTenantIdAsync_ValidInput_ReturnsAudienceList()
        {
            // Arrange
            string tenantId = "tenant123";
            string clientId = "client123";
            int skip = 1;
            int take = 10;
            var tenant = CreateTestTenant(tenantId, clientId);
            var audiences = CreateTestAudiences(tenantId);

            _audienceRepositoryMock.Setup(r => r.GetTenantByIdAsync(It.IsAny<string>()))
                .ReturnsAsync(tenant);
            _audienceRepositoryMock.Setup(r => r.GetTenantAudiencesByTenantIdAsync(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<int>()))
                .ReturnsAsync(audiences);
            _auditLogsMock.Setup(a => a.CreateAsync(It.IsAny<AuditLog>()))
                .ReturnsAsync("audit123");

            // Act
            var result = await _tenantAudienceService.GetTenantAudiencesByTenantIdAsync(tenantId, clientId, skip, take);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("audience123", result[0].Id);
            Assert.AreEqual(tenantId, result[0].TenantId);
            Assert.AreEqual("value", result[0].Details["key"].ToString());
            _auditLogsMock.Verify(a => a.CreateAsync(It.Is<AuditLog>(log =>
                log.ActionType == AuditActionType.Access &&
                log.IsSuccess &&
                log.TenantId == tenantId &&
                log.Details["Action"].ToString().Contains("Retrieved 1 tenant audience records"))), Times.Once());
        }

        [Test]
        public async Task GetTenantAudiencesByTenantIdAsync_NullTenantId_ThrowsArgumentNullException()
        {
            // Arrange
            string tenantId = null;
            string clientId = "client123";
            int pageNumber = 1;
            int pageSize = 10;

            _auditLogsMock.Setup(a => a.CreateAsync(It.IsAny<AuditLog>()))
                .ReturnsAsync("audit123");

            // Act & Assert
            var ex = Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await _tenantAudienceService.GetTenantAudiencesByTenantIdAsync(tenantId, clientId, pageNumber, pageSize));
            Assert.AreEqual("Tenant ID cannot be null or empty. (Parameter 'tenantId')", ex.Message);
            _auditLogsMock.Verify(a => a.CreateAsync(It.Is<AuditLog>(log =>
                !log.IsSuccess &&
                log.Details["Error"].ToString().Contains("null or empty TenantId"))), Times.Once());
        }

        [Test]
        public async Task GetTenantAudiencesByTenantIdAsync_NullClientId_ThrowsArgumentNullException()
        {
            // Arrange
            string tenantId = "tenant123";
            string clientId = null;
            int pageNumber = 1;
            int pageSize = 10;

            _auditLogsMock.Setup(a => a.CreateAsync(It.IsAny<AuditLog>()))
                .ReturnsAsync("audit123");

            // Act & Assert
            var ex = Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await _tenantAudienceService.GetTenantAudiencesByTenantIdAsync(tenantId, clientId, pageNumber, pageSize));
            Assert.AreEqual("ClientId cannot be null or empty. (Parameter 'clientIdFromHeader')", ex.Message);
            _auditLogsMock.Verify(a => a.CreateAsync(It.Is<AuditLog>(log =>
                !log.IsSuccess &&
                log.Details["Error"].ToString().Contains("null or empty ClientId"))), Times.Once());
        }

        [Test]
        public async Task GetTenantAudiencesByTenantIdAsync_TenantNotFound_ThrowsInvalidOperationException()
        {
            // Arrange
            string tenantId = "tenant123";
            string clientId = "client123";
            int pageNumber = 1;
            int pageSize = 10;

            _audienceRepositoryMock.Setup(r => r.GetTenantByIdAsync(It.IsAny<string>()))
                .ReturnsAsync((Tenant)null);
            _auditLogsMock.Setup(a => a.CreateAsync(It.IsAny<AuditLog>()))
                .ReturnsAsync("audit123");

            // Act & Assert
            var ex = Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _tenantAudienceService.GetTenantAudiencesByTenantIdAsync(tenantId, clientId, pageNumber, pageSize));
            Assert.AreEqual("Tenant not found.", ex.Message);
            _auditLogsMock.Verify(a => a.CreateAsync(It.Is<AuditLog>(log =>
                !log.IsSuccess &&
                log.Details["Error"].ToString().Contains("Tenant not found"))), Times.Once());
        }

        [Test]
        public async Task GetTenantAudiencesByTenantIdAsync_InvalidClientId_ThrowsUnauthorizedAccessException()
        {
            // Arrange
            string tenantId = "tenant123";
            string clientId = "wrongClient";
            var tenant = CreateTestTenant(tenantId, "client123");
            int pageNumber = 1;
            int pageSize = 10;

            _audienceRepositoryMock.Setup(r => r.GetTenantByIdAsync(It.IsAny<string>()))
                .ReturnsAsync(tenant);
            _auditLogsMock.Setup(a => a.CreateAsync(It.IsAny<AuditLog>()))
                .ReturnsAsync("audit123");

            // Act & Assert
            var ex = Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
                await _tenantAudienceService.GetTenantAudiencesByTenantIdAsync(tenantId, clientId, pageNumber, pageSize));
            Assert.AreEqual("Invalid ClientId.", ex.Message);
            _auditLogsMock.Verify(a => a.CreateAsync(It.Is<AuditLog>(log =>
                !log.IsSuccess &&
                log.Details["Error"].ToString().Contains("Invalid ClientId"))), Times.Once());
        }

        [Test]
        public async Task GetTenantAudiencesByTenantIdAsync_NoConsent_ThrowsInvalidOperationException()
        {
            // Arrange
            string tenantId = "tenant123";
            string clientId = "client123";
            var tenant = CreateTestTenant(tenantId, clientId);
            tenant.ConsentAccepted = false;
            int pageNumber = 1;
            int pageSize = 10;

            _audienceRepositoryMock.Setup(r => r.GetTenantByIdAsync(It.IsAny<string>()))
                .ReturnsAsync(tenant);
            _auditLogsMock.Setup(a => a.CreateAsync(It.IsAny<AuditLog>()))
                .ReturnsAsync("audit123");

            // Act & Assert
            var ex = Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _tenantAudienceService.GetTenantAudiencesByTenantIdAsync(tenantId, clientId, pageNumber, pageSize));
            Assert.AreEqual("Tenant consent is required for data access.", ex.Message);
            _auditLogsMock.Verify(a => a.CreateAsync(It.Is<AuditLog>(log =>
                !log.IsSuccess &&
                log.Details["Error"].ToString().Contains("Tenant has not provided consent"))), Times.Once());
        }
    }
}

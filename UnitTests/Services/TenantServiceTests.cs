using GdprConfigurations;
using GdprServices.AuditLogs;
using GdprServices.DataExporter;
using GdprServices.Users;
using Microsoft.Extensions.Logging;
using Models.AuditLog;
using Models.Auth;
using Models.Enums;
using Models.Tenants;
using MongoDB.Bson;
using MongoDB.Driver;
using Moq;

namespace UnitTests.Services
{
    [TestFixture]
    public class TenantServiceTests
    {
        private Mock<IMongoDatabase> _mongoDatabaseMock;
        private Mock<IMongoCollection<Tenant>> _tenantsCollectionMock;
        private Mock<IMongoCollection<PseudonymMapping>> _pseudonymMappingsCollectionMock;
        private Mock<IAuditLogs> _auditLogsMock;
        private Mock<IDataFormatter> _dataFormatterMock;
        private Mock<ILogger<TenantService>> _loggerMock;
        private Mock<IMongoDbSettings> _settingsMock;
        private Mock<IMongoClient> _mongoClientMock;
        private TenantService _tenantService;
        private readonly byte[] _encryptionKey = Convert.FromBase64String("ASNFZ4mrze/+3LqYdlQyEBEiM0RVV2aHiZqrzN3u/wA=");

        [SetUp]
        public void Setup()
        {
            _mongoDatabaseMock = new Mock<IMongoDatabase>();
            _tenantsCollectionMock = new Mock<IMongoCollection<Tenant>>();
            _pseudonymMappingsCollectionMock = new Mock<IMongoCollection<PseudonymMapping>>();
            _auditLogsMock = new Mock<IAuditLogs>();
            _dataFormatterMock = new Mock<IDataFormatter>();
            _loggerMock = new Mock<ILogger<TenantService>>();
            _settingsMock = new Mock<IMongoDbSettings>();
            _mongoClientMock = new Mock<IMongoClient>();

            // Set up IMongoDbSettings.DatabaseName
            _settingsMock.Setup(s => s.DatabaseName).Returns("TestDatabase");

            // Set up IMongoClient.GetDatabase
            _mongoClientMock.Setup(c => c.GetDatabase("TestDatabase", null))
                .Returns(_mongoDatabaseMock.Object);

            // Set up IMongoDatabase.GetCollection
            _mongoDatabaseMock.Setup(db => db.GetCollection<Tenant>("Tenants", null))
                .Returns(_tenantsCollectionMock.Object);
            _mongoDatabaseMock.Setup(db => db.GetCollection<PseudonymMapping>("PseudonymMappings", null))
                .Returns(_pseudonymMappingsCollectionMock.Object);

            // Mock index creation with explicit null for optional parameters
            _pseudonymMappingsCollectionMock.Setup(c => c.Indexes.CreateOne(It.IsAny<CreateIndexModel<PseudonymMapping>>(), null, It.IsAny<CancellationToken>()))
                .Returns("index_created");

            _tenantService = new TenantService(
                _mongoClientMock.Object,
                _settingsMock.Object,
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

        private List<PseudonymMapping> CreateTestMappings(string tenantId)
        {
            return new List<PseudonymMapping>
            {
                new PseudonymMapping
                {
                    Id = ObjectId.GenerateNewId().ToString(),
                    TenantId = tenantId,
                    HashedValue = EncryptionProvider.HashString("John Doe"),
                    EncryptedOriginalValue = EncryptionProvider.EncryptString("John Doe", _encryptionKey),
                    FieldType = "FullName",
                    RetentionExpiryUtc = DateTime.UtcNow.AddYears(5)
                },
                new PseudonymMapping
                {
                    Id = ObjectId.GenerateNewId().ToString(),
                    TenantId = tenantId,
                    HashedValue = EncryptionProvider.HashString("email@example.com"),
                    EncryptedOriginalValue = EncryptionProvider.EncryptString("email@example.com", _encryptionKey),
                    FieldType = "Email",
                    RetentionExpiryUtc = DateTime.UtcNow.AddYears(5)
                }
            };
        }

        [Test]
        public async Task GetTenantDataAsync_ValidInput_ReturnsTenantResponse()
        {
            // Arrange
            string tenantId = "tenant123";
            string clientId = "client123";
            var tenant = CreateTestTenant(tenantId, clientId);
            var mappings = CreateTestMappings(tenantId);

            _tenantsCollectionMock.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<Tenant>>(), It.IsAny<FindOptions<Tenant, Tenant>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AsyncCursor<Tenant>(new List<Tenant> { tenant }));
            _pseudonymMappingsCollectionMock.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<PseudonymMapping>>(), It.IsAny<FindOptions<PseudonymMapping, PseudonymMapping>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AsyncCursor<PseudonymMapping>(mappings));
            _auditLogsMock.Setup(a => a.CreateAsync(It.IsAny<AuditLog>()))
                .ReturnsAsync("audit123");

            // Act
            var result = await _tenantService.GetTenantDataAsync(tenantId, clientId);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(tenantId, result.Id);
            Assert.AreEqual("John Doe", result.FullName);
            Assert.AreEqual("email@example.com", result.Email);
            Assert.AreEqual(tenant.UserName, result.UserName);
            Assert.AreEqual(tenant.AccountType, result.AccountType);
            Assert.AreEqual(tenant.Role, result.Role);
            Assert.AreEqual(tenant.EmailConfirmed, result.EmailConfirmed);
            Assert.AreEqual(tenant.CreatedAtUtc, result.CreatedAtUtc);
            Assert.AreEqual(tenant.WebsiteUrl, result.WebsiteUrl);
            Assert.AreEqual(tenant.AccountRequestId, result.AccountRequestId);
            Assert.AreEqual(tenant.ConsentAccepted, result.ConsentAccepted);
            Assert.AreEqual(tenant.ConsentAcceptedUtcDate, result.ConsentAcceptedUtcDate);
            Assert.AreEqual(tenant.RetentionExpiryUtc, result.RetentionExpiryUtc);
            _auditLogsMock.Verify(a => a.CreateAsync(It.Is<AuditLog>(log =>
                log.ActionType == AuditActionType.Access &&
                log.IsSuccess &&
                log.TenantId == tenantId &&
                log.Details["Action"].ToString() == "Tenant data accessed successfully")), Times.Once());
        }

        [Test]
        public async Task UpdateTenantAsync_NullRequest_ThrowsArgumentNullException()
        {
            // Arrange
            string tenantId = "tenant123";
            string clientId = "client123";
            UpdateTenantRequest request = null;

            _auditLogsMock.Setup(a => a.CreateAsync(It.Is<AuditLog>(log => !log.IsSuccess && log.Details["Error"].ToString().Contains("Update request cannot be null."))))
                .ReturnsAsync("audit123");

            // Act & Assert
            var ex = Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await _tenantService.UpdateTenantAsync(tenantId, request, clientId));
            Assert.AreEqual("Request must not be null. (Parameter 'request')", ex.Message);
            _auditLogsMock.Verify(a => a.CreateAsync(It.Is<AuditLog>(log => !log.IsSuccess && log.Details["Error"].ToString().Contains("Update request cannot be null."))), Times.Once());
        }

        [Test]
        public async Task UpdateTenantAsync_TenantNotFound_ThrowsInvalidOperationException()
        {
            // Arrange
            string tenantId = "tenant123";
            string clientId = "client123";
            var request = new UpdateTenantRequest { WebsiteUrl = "https://newurl.com" };

            _tenantsCollectionMock.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<Tenant>>(), It.IsAny<FindOptions<Tenant, Tenant>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AsyncCursor<Tenant>(new List<Tenant>()));
            _auditLogsMock.Setup(a => a.CreateAsync(It.IsAny<AuditLog>()))
                .ReturnsAsync("audit123");

            // Act & Assert
            var ex = Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _tenantService.UpdateTenantAsync(tenantId, request, clientId));
            Assert.AreEqual("Tenant not found.", ex.Message);
            _auditLogsMock.Verify(a => a.CreateAsync(It.Is<AuditLog>(log => !log.IsSuccess && log.Details["Error"].ToString().Contains("Tenant not found"))), Times.Once());
        }

        [Test]
        public async Task UpdateTenantAsync_MongoException_ThrowsInvalidOperationException()
        {
            // Arrange
            string tenantId = "tenant123";
            string clientId = "client123";
            var request = new UpdateTenantRequest { WebsiteUrl = "https://newurl.com" };
            var tenant = CreateTestTenant(tenantId, clientId);

            _tenantsCollectionMock.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<Tenant>>(), It.IsAny<FindOptions<Tenant, Tenant>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AsyncCursor<Tenant>(new List<Tenant> { tenant }));
            _tenantsCollectionMock.Setup(c => c.UpdateOneAsync(It.IsAny<FilterDefinition<Tenant>>(), It.IsAny<UpdateDefinition<Tenant>>(), It.IsAny<UpdateOptions>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new MongoException("Database error"));
            _auditLogsMock.Setup(a => a.CreateAsync(It.IsAny<AuditLog>()))
                .ReturnsAsync("audit123");

            // Act & Assert
            var ex = Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _tenantService.UpdateTenantAsync(tenantId, request, clientId));
            Assert.IsTrue(ex.Message.Contains("An error occurred while updating the tenant"));
            _auditLogsMock.Verify(a => a.CreateAsync(It.Is<AuditLog>(log => !log.IsSuccess && log.Details["Error"].ToString().Contains("Failed to update tenant"))), Times.Once());
        }

        [Test]
        public async Task DownloadTenantDataAsync_ValidJsonFormat_ReturnsFormattedData()
        {
            // Arrange
            string tenantId = "tenant123";
            string clientId = "client123";
            string format = "JSON";
            var tenant = CreateTestTenant(tenantId, clientId);
            var mappings = CreateTestMappings(tenantId);
            var tenantResponse = new TenantResponse
            {
                Id = tenantId,
                FullName = "John Doe",
                Email = "email@example.com",
                UserName = tenant.UserName,
                AccountType = tenant.AccountType,
                Role = tenant.Role,
                EmailConfirmed = tenant.EmailConfirmed,
                CreatedAtUtc = tenant.CreatedAtUtc,
                WebsiteUrl = tenant.WebsiteUrl,
                AccountRequestId = tenant.AccountRequestId,
                ConsentAccepted = tenant.ConsentAccepted,
                ConsentAcceptedUtcDate = tenant.ConsentAcceptedUtcDate,
                RetentionExpiryUtc = tenant.RetentionExpiryUtc
            };

            _tenantsCollectionMock.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<Tenant>>(), It.IsAny<FindOptions<Tenant, Tenant>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AsyncCursor<Tenant>(new List<Tenant> { tenant }));
            _pseudonymMappingsCollectionMock.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<PseudonymMapping>>(), It.IsAny<FindOptions<PseudonymMapping, PseudonymMapping>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AsyncCursor<PseudonymMapping>(mappings));
            _dataFormatterMock.Setup(f => f.FormatAsJson(It.IsAny<TenantResponse>()))
                .Returns("formatted_json_data");
            _auditLogsMock.Setup(a => a.CreateAsync(It.IsAny<AuditLog>()))
                .ReturnsAsync("audit123");

            // Act
            var result = await _tenantService.DownloadTenantDataAsync(tenantId, clientId, format);

            // Assert
            Assert.AreEqual("formatted_json_data", result);
            _dataFormatterMock.Verify(f => f.FormatAsJson(It.IsAny<TenantResponse>()), Times.Once());
            _auditLogsMock.Verify(a => a.CreateAsync(It.Is<AuditLog>(log =>
                log.ActionType == AuditActionType.Download &&
                log.IsSuccess &&
                log.TenantId == tenantId &&
                log.Details["Action"].ToString().Contains("JSON"))), Times.Once());
        }

        [Test]
        public async Task DownloadTenantDataAsync_ValidCsvFormat_ReturnsFormattedData()
        {
            // Arrange
            string tenantId = "tenant123";
            string clientId = "client123";
            string format = "CSV";
            var tenant = CreateTestTenant(tenantId, clientId);
            var mappings = CreateTestMappings(tenantId);
            var tenantResponse = new TenantResponse
            {
                Id = tenantId,
                FullName = "John Doe",
                Email = "email@example.com",
                UserName = tenant.UserName,
                AccountType = tenant.AccountType,
                Role = tenant.Role,
                EmailConfirmed = tenant.EmailConfirmed,
                CreatedAtUtc = tenant.CreatedAtUtc,
                WebsiteUrl = tenant.WebsiteUrl,
                AccountRequestId = tenant.AccountRequestId,
                ConsentAccepted = tenant.ConsentAccepted,
                ConsentAcceptedUtcDate = tenant.ConsentAcceptedUtcDate,
                RetentionExpiryUtc = tenant.RetentionExpiryUtc
            };

            _tenantsCollectionMock.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<Tenant>>(), It.IsAny<FindOptions<Tenant, Tenant>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AsyncCursor<Tenant>(new List<Tenant> { tenant }));
            _pseudonymMappingsCollectionMock.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<PseudonymMapping>>(), It.IsAny<FindOptions<PseudonymMapping, PseudonymMapping>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AsyncCursor<PseudonymMapping>(mappings));
            _dataFormatterMock.Setup(f => f.FormatAsCsv(It.IsAny<TenantResponse>()))
                .Returns("formatted_csv_data");
            _auditLogsMock.Setup(a => a.CreateAsync(It.IsAny<AuditLog>()))
                .ReturnsAsync("audit123");

            // Act
            var result = await _tenantService.DownloadTenantDataAsync(tenantId, clientId, format);

            // Assert
            Assert.AreEqual("formatted_csv_data", result);
            _dataFormatterMock.Verify(f => f.FormatAsCsv(It.IsAny<TenantResponse>()), Times.Once());
            _auditLogsMock.Verify(a => a.CreateAsync(It.Is<AuditLog>(log =>
                log.ActionType == AuditActionType.Download &&
                log.IsSuccess &&
                log.TenantId == tenantId &&
                log.Details["Action"].ToString().Contains("CSV"))), Times.Once());
        }

        [Test]
        public async Task DownloadTenantDataAsync_NullFormat_ThrowsArgumentNullException()
        {
            // Arrange
            string tenantId = "tenant123";
            string clientId = "client123";
            string format = null;

            _auditLogsMock.Setup(a => a.CreateAsync(It.IsAny<AuditLog>()))
                .ReturnsAsync("audit123");

            // Act & Assert
            var ex = Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await _tenantService.DownloadTenantDataAsync(tenantId, clientId, format));
            Assert.AreEqual("Format cannot be null or empty. (Parameter 'format')", ex.Message);
            _auditLogsMock.Verify(a => a.CreateAsync(It.Is<AuditLog>(log =>
                !log.IsSuccess &&
                log.Details["Error"].ToString().Contains("null or empty format"))), Times.Once());
        }

        [Test]
        public async Task DownloadTenantDataAsync_InvalidFormat_ThrowsArgumentException()
        {
            // Arrange
            string tenantId = "tenant123";
            string clientId = "client123";
            string format = "XML";

            _auditLogsMock.Setup(a => a.CreateAsync(It.IsAny<AuditLog>()))
                .ReturnsAsync("audit123");

            // Act & Assert
            var ex = Assert.ThrowsAsync<ArgumentException>(async () =>
                await _tenantService.DownloadTenantDataAsync(tenantId, clientId, format));
            Assert.AreEqual("Format must be 'JSON' or 'CSV'. (Parameter 'format')", ex.Message);
            _auditLogsMock.Verify(a => a.CreateAsync(It.Is<AuditLog>(log =>
                !log.IsSuccess &&
                log.Details["Error"].ToString().Contains("Invalid format specified: XML"))), Times.Once());
        }

        [Test]
        public async Task DownloadTenantDataAsync_FormattingFails_ThrowsInvalidOperationException()
        {
            // Arrange
            string tenantId = "tenant123";
            string clientId = "client123";
            string format = "JSON";
            var tenant = CreateTestTenant(tenantId, clientId);
            var mappings = CreateTestMappings(tenantId);
            var tenantResponse = new TenantResponse
            {
                Id = tenantId,
                FullName = "John Doe",
                Email = "email@example.com",
                UserName = tenant.UserName,
                AccountType = tenant.AccountType,
                Role = tenant.Role,
                EmailConfirmed = tenant.EmailConfirmed,
                CreatedAtUtc = tenant.CreatedAtUtc,
                WebsiteUrl = tenant.WebsiteUrl,
                AccountRequestId = tenant.AccountRequestId,
                ConsentAccepted = tenant.ConsentAccepted,
                ConsentAcceptedUtcDate = tenant.ConsentAcceptedUtcDate,
                RetentionExpiryUtc = tenant.RetentionExpiryUtc
            };

            _tenantsCollectionMock.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<Tenant>>(), It.IsAny<FindOptions<Tenant, Tenant>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AsyncCursor<Tenant>(new List<Tenant> { tenant }));
            _pseudonymMappingsCollectionMock.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<PseudonymMapping>>(), It.IsAny<FindOptions<PseudonymMapping, PseudonymMapping>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AsyncCursor<PseudonymMapping>(mappings));
            _dataFormatterMock.Setup(f => f.FormatAsJson(It.IsAny<TenantResponse>()))
                .Throws(new Exception("Formatting error"));
            _auditLogsMock.Setup(a => a.CreateAsync(It.IsAny<AuditLog>()))
                .ReturnsAsync("audit123");

            // Act & Assert
            var ex = Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _tenantService.DownloadTenantDataAsync(tenantId, clientId, format));
            Assert.IsTrue(ex.Message.Contains("Failed to format tenant data as JSON"));
            _auditLogsMock.Verify(a => a.CreateAsync(It.Is<AuditLog>(log =>
                !log.IsSuccess &&
                log.Details["Error"].ToString().Contains("Failed to format tenant data as JSON"))), Times.Once());
        }
    }
}

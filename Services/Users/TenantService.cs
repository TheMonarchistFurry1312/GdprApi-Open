using GdprConfigurations;
using GdprServices.AuditLogs;
using GdprServices.DataExporter;
using Microsoft.Extensions.Logging;
using Models.AuditLog;
using Models.Auth;
using Models.Enums;
using Models.Tenants;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Security.Cryptography;
using System.Text.Json;

namespace GdprServices.Users
{
    /// <summary>
    /// Service for managing tenant operations in a GDPR-compliant multi-tenant system.
    /// Handles tenant creation and data retrieval with secure data handling, pseudonymization, and audit logging.
    /// </summary>
    public class TenantService : ITenantService
    {
        private readonly IMongoDatabase _mongoDatabase;
        private readonly IMongoCollection<Tenant> _tenantsCollection;
        private readonly IMongoCollection<PseudonymMapping> _pseudonymMappingsCollection;
        private readonly ILogger<TenantService> _logger;
        private readonly IAuditLogs _auditLogs;
        private readonly IDataFormatter _dataFormatter;
        private static readonly string Base64Key = "ASNFZ4mrze/+3LqYdlQyEBEiM0RVV2aHiZqrzN3u/wA=";
        private readonly byte[] EncryptionKey;

        public TenantService(
            IMongoClient mongoClient,
            IMongoDbSettings settings,
            ILogger<TenantService> logger,
            IAuditLogs auditLogs,
            IDataFormatter dataFormatter)
        {
            _mongoDatabase = mongoClient.GetDatabase(settings.DatabaseName);
            _tenantsCollection = _mongoDatabase.GetCollection<Tenant>("Tenants");
            _pseudonymMappingsCollection = _mongoDatabase.GetCollection<PseudonymMapping>("PseudonymMappings");
            _logger = logger;
            _auditLogs = auditLogs;
            _dataFormatter = dataFormatter;

            // Create index on PseudonymMapping for efficient retrieval
            var indexKeys = Builders<PseudonymMapping>.IndexKeys
                .Ascending(x => x.TenantId)
                .Ascending(x => x.FieldType);
            _pseudonymMappingsCollection.Indexes.CreateOne(new CreateIndexModel<PseudonymMapping>(indexKeys));

            // Initialize EncryptionKey (use Azure Key Vault in production)
            EncryptionKey = InitializeEncryptionKey();
        }

        public async Task<TenantResponse> GetTenantDataAsync(string tenantId, string clientIdFromHeader)
        {
            var tenant = await GetAndValidateTenantAsync(tenantId, clientIdFromHeader, "get tenant data");

            // Decrypt original PII values
            var (originalFullName, originalEmail) = await GetOriginalPiiDataAsync(tenantId, tenant.FullName, tenant.Email);

            // Prepare response
            var response = new TenantResponse
            {
                Id = tenant.Id,
                FullName = originalFullName,
                Email = originalEmail,
                UserName = tenant.UserName,
                AccountType = tenant.AccountType,
                Role = tenant.Role,
                EmailConfirmed = tenant.EmailConfirmed,
                CreatedAtUtc = tenant.CreatedAtUtc,
                WebsiteUrl = tenant.WebsiteUrl,
                AccountRequestId = tenant.AccountRequestId,
                ConsentAccepted = tenant.ConsentAccepted,
                ConsentAcceptedUtcDate = tenant.ConsentAcceptedUtcDate,
                RetentionExpiryUtc = tenant.RetentionExpiryUtc,
            };

            await LogAuditSuccessAsync(tenantId, originalEmail, AuditActionType.Access, TargetEntityType.Tenant, "Tenant data accessed successfully");
            return response;
        }

        public async Task<string> UpdateTenantAsync(
            string tenantId,
            UpdateTenantRequest request,
            string clientIdFromHeader)
        {
            if (request == null)
            {
                await LogAuditFailureAsync(null, tenantId, "Update request cannot be null.", ActorType.System);
                _logger.LogWarning("UpdateTenantAsync called with null or invalid request.");
                throw new ArgumentNullException(nameof(request), "Request must not be null.");
            }

            var tenant = await GetAndValidateTenantAsync(tenantId, clientIdFromHeader, "update tenant");

            var updatesBuilder = Builders<Tenant>.Update;
            var updates = new List<UpdateDefinition<Tenant>>();

            if (!string.IsNullOrEmpty(request.WebsiteUrl))
                updates.Add(updatesBuilder.Set(t => t.WebsiteUrl, request.WebsiteUrl));

            if (!string.IsNullOrEmpty(request.UserName))
                updates.Add(updatesBuilder.Set(t => t.UserName, request.UserName));

            if (!string.IsNullOrEmpty(request.FullName))
            {
                var hashedFullName = EncryptionProvider.HashString(request.FullName);
                updates.Add(updatesBuilder.Set(t => t.FullName, hashedFullName));

                var mappingFilter = Builders<PseudonymMapping>.Filter.And(
                    Builders<PseudonymMapping>.Filter.Eq(m => m.TenantId, tenantId),
                    Builders<PseudonymMapping>.Filter.Eq(m => m.FieldType, "FullName")
                );

                var fullNameMapping = new PseudonymMapping
                {
                    Id = (await _pseudonymMappingsCollection.Find(mappingFilter).FirstOrDefaultAsync())?.Id ?? ObjectId.GenerateNewId().ToString(),
                    TenantId = tenantId,
                    HashedValue = hashedFullName,
                    EncryptedOriginalValue = EncryptionProvider.EncryptString(request.FullName, EncryptionKey),
                    FieldType = "FullName",
                    RetentionExpiryUtc = tenant.RetentionExpiryUtc
                };

                await _pseudonymMappingsCollection.ReplaceOneAsync(mappingFilter, fullNameMapping, new ReplaceOptions { IsUpsert = true });
            }

            // Always update UpdatedAtUtc
            updates.Add(updatesBuilder.Set(t => t.UpdatedAtUtc, DateTime.UtcNow));
            var combinedUpdates = updatesBuilder.Combine(updates);

            try
            {
                var updateResult = await _tenantsCollection.UpdateOneAsync(
                    Builders<Tenant>.Filter.Eq(t => t.Id, tenantId), combinedUpdates);

                if (updateResult.MatchedCount == 0)
                {
                    await LogAuditFailureAsync(tenant.Email, tenantId, "Tenant not found during update operation.", ActorType.User);
                    throw new InvalidOperationException("Tenant not found during update.");
                }

                await LogAuditSuccessAsync(tenantId, tenant.Email, AuditActionType.Update, TargetEntityType.Tenant, "Tenant data updated successfully");
                return "Tenant updated successfully.";
            }
            catch (MongoException ex)
            {
                await LogAuditFailureAsync(tenant.Email, tenantId, $"Failed to update tenant: {ex.Message}", ActorType.User);
                _logger.LogError(ex, "MongoException occurred while updating tenant: {TenantId}", tenantId);
                throw new InvalidOperationException("An error occurred while updating the tenant.", ex);
            }
            catch (Exception ex)
            {
                await LogAuditFailureAsync(tenant.Email, tenantId, $"Unexpected error during tenant update: {ex.Message}", ActorType.User);
                _logger.LogError(ex, "Unexpected error during tenant update: {TenantId}", tenantId);
                throw new InvalidOperationException("An unexpected error occurred while updating the tenant.", ex);
            }
        }

        public async Task<string> DownloadTenantDataAsync(string tenantId, string clientIdFromHeader, string format)
        {
            if (string.IsNullOrEmpty(format))
            {
                await LogAuditFailureAsync(null, tenantId, "Download tenant data called with null or empty format.", ActorType.System);
                _logger.LogWarning("DownloadTenantDataAsync called with null or empty format for TenantId: {TenantId}", tenantId);
                throw new ArgumentNullException(nameof(format), "Format cannot be null or empty.");
            }

            if (!format.Equals("JSON", StringComparison.OrdinalIgnoreCase) && !format.Equals("CSV", StringComparison.OrdinalIgnoreCase))
            {
                await LogAuditFailureAsync(null, tenantId, $"Invalid format specified: {format}.", ActorType.System);
                _logger.LogWarning("Invalid format '{Format}' specified for TenantId: {TenantId}", format, tenantId);
                throw new ArgumentException("Format must be 'JSON' or 'CSV'.", nameof(format));
            }

            // Reuse GetTenantDataAsync logic to retrieve tenant data
            TenantResponse tenantData;
            try
            {
                tenantData = await GetTenantDataAsync(tenantId, clientIdFromHeader);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve tenant data for download, TenantId: {TenantId}", tenantId);
                throw; // GetTenantDataAsync already logs specific audit failures
            }

            // Format the data
            string formattedData;
            try
            {
                formattedData = format.Equals("JSON", StringComparison.OrdinalIgnoreCase)
                    ? _dataFormatter.FormatAsJson(tenantData)
                    : _dataFormatter.FormatAsCsv(tenantData);
            }
            catch (Exception ex)
            {
                await LogAuditFailureAsync(tenantData.Email, tenantId, $"Failed to format tenant data as {format}: {ex.Message}", ActorType.User);
                _logger.LogError(ex, "Failed to format tenant data as {Format} for TenantId: {TenantId}", format, tenantId);
                throw new InvalidOperationException($"Failed to format tenant data as {format}.", ex);
            }

            await LogAuditSuccessAsync(tenantId, tenantData.Email, AuditActionType.Download, TargetEntityType.Tenant, $"Tenant data downloaded as {format}");
            return formattedData;
        }

        // --- Private Methods for Code Reuse and Clarity ---

        private async Task<Tenant> GetAndValidateTenantAsync(string tenantId, string clientIdFromHeader, string action)
        {
            if (string.IsNullOrEmpty(tenantId))
            {
                await LogAuditFailureAsync(null, null, $"Someone tried to {action} with a null or empty tenantId.", ActorType.System);
                _logger.LogWarning("{Action} called with null or empty tenantId.", action);
                throw new ArgumentNullException(nameof(tenantId), "Tenant ID cannot be null or empty.");
            }

            var tenantFilter = Builders<Tenant>.Filter.Eq(t => t.Id, tenantId);
            var tenant = await _tenantsCollection.Find(tenantFilter).FirstOrDefaultAsync();

            if (tenant == null)
            {
                await LogAuditFailureAsync(null, tenantId, $"Tenant not found for {action}.", ActorType.System);
                _logger.LogWarning("Tenant not found for {Action}, ID: {TenantId}", action, tenantId);
                throw new InvalidOperationException("Tenant not found.");
            }

            if (tenant.ClientId != clientIdFromHeader)
            {
                await LogAuditFailureAsync(tenant.Email, tenantId, $"Unauthorized access: Invalid ClientId {clientIdFromHeader}.", ActorType.System);
                throw new UnauthorizedAccessException("Invalid ClientId.");
            }

            return tenant;
        }

        private async Task<(string originalFullName, string originalEmail)> GetOriginalPiiDataAsync(string tenantId, string defaultFullName, string defaultEmail)
        {
            var mappingFilter = Builders<PseudonymMapping>.Filter.Eq(m => m.TenantId, tenantId);
            var mappings = await _pseudonymMappingsCollection.Find(mappingFilter).ToListAsync();
            var fullNameMapping = mappings.FirstOrDefault(m => m.FieldType == "FullName");
            var emailMapping = mappings.FirstOrDefault(m => m.FieldType == "Email");

            try
            {
                string originalFullName = fullNameMapping != null
                    ? EncryptionProvider.DecryptString(fullNameMapping.EncryptedOriginalValue, EncryptionKey)
                    : defaultFullName;

                string originalEmail = emailMapping != null
                    ? EncryptionProvider.DecryptString(emailMapping.EncryptedOriginalValue, EncryptionKey)
                    : defaultEmail;

                return (originalFullName, originalEmail);
            }
            catch (CryptographicException ex)
            {
                await LogAuditFailureAsync(null, tenantId, $"Failed to decrypt tenant data: {ex.Message}", ActorType.System);
                _logger.LogError(ex, "Failed to decrypt tenant data for ID: {TenantId}", tenantId);
                throw new InvalidOperationException("Failed to decrypt tenant data.", ex);
            }
        }

        private async Task LogAuditSuccessAsync(string tenantId, string performedBy, AuditActionType actionType, TargetEntityType targetEntityType, string details)
        {
            var auditLog = new AuditLog
            {
                Id = ObjectId.GenerateNewId().ToString(),
                TenantId = tenantId,
                PerformedBy = performedBy,
                ActorType = ActorType.User,
                ActionType = actionType,
                TargetEntity = targetEntityType,
                TargetEntityId = tenantId,
                TimestampUtc = DateTime.UtcNow,
                ClientIpAddress = null, // Placeholder: IP capture requires IHttpContextAccessor
                DeviceType = null, // Placeholder: Device type requires IHttpContextAccessor
                Details = new Dictionary<string, object> { { "Action", details } },
                IsGdprRelevant = true,
                RetentionExpiryUtc = DateTime.UtcNow.AddYears(5),
                CorrelationId = Guid.NewGuid().ToString(),
                IsSuccess = true
            };
            auditLog.ComputeIntegrityHash();

            try
            {
                await _auditLogs.CreateAsync(auditLog);
                _logger.LogInformation("{Details}, TenantId: {TenantId}", details, tenantId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log audit for action: {ActionType}, TenantId: {TenantId}", actionType, tenantId);
            }
        }

        private async Task LogAuditFailureAsync(
            string? performedBy,
            string? tenantId,
            string errorMessage,
            ActorType actorType)
        {
            var auditLog = new AuditLog
            {
                Id = ObjectId.GenerateNewId().ToString(),
                TenantId = tenantId ?? "Unknown",
                PerformedBy = performedBy != null ? EncryptionProvider.HashString(performedBy) : "System",
                ActorType = actorType,
                ActionType = AuditActionType.Create,
                TargetEntity = TargetEntityType.Tenant,
                TargetEntityId = tenantId,
                TimestampUtc = DateTime.UtcNow,
                ClientIpAddress = null,
                DeviceType = null,
                Details = new Dictionary<string, object>
                {
                    { "Error", errorMessage },
                },
                IsGdprRelevant = true,
                RetentionExpiryUtc = DateTime.UtcNow.AddYears(5),
                CorrelationId = Guid.NewGuid().ToString(),
                IsSuccess = false
            };
            auditLog.ComputeIntegrityHash();

            try
            {
                await _auditLogs.CreateAsync(auditLog);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log audit failure for tenant creation, PerformedBy: {PerformedBy}", performedBy);
            }
        }

        // El resto de los métodos privados se mantienen igual ya que su lógica es específica y no se repite

        private static object ConvertStringToBsonCompatible(string? value)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            // Handle string values that represent booleans
            if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase))
                return true;
            if (string.Equals(value, "false", StringComparison.OrdinalIgnoreCase))
                return false;

            return value; // Return string as-is if not a boolean
        }

        private Dictionary<string, object> ConvertBsonToJsonCompatible(IDictionary<string, object> dict)
        {
            var result = new Dictionary<string, object>();
            foreach (var kvp in dict)
            {
                // Convert key to camelCase
                string camelCaseKey = ToCamelCase(kvp.Key);
                try
                {
                    // Expect value to be a byte[] (BSON Binary)
                    if (kvp.Value is byte[] encryptedValue)
                    {
                        // Decrypt the byte[] to a JSON string
                        string jsonValue = EncryptionProvider.DecryptString(encryptedValue, EncryptionKey);
                        // Deserialize the JSON string back to its original type
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
                        object decryptedValue = JsonSerializer.Deserialize<object>(jsonValue, new System.Text.Json.JsonSerializerOptions
                        {
                            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                        });
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
                        result[camelCaseKey] = ConvertBsonValueToJsonCompatible(decryptedValue!);
                    }
                    else
                    {
                        result[camelCaseKey] = null; // Handle unexpected types
                        _logger.LogWarning("Unexpected value type for key '{Key}': {Type}", kvp.Key, kvp.Value?.GetType().Name);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to decrypt or deserialize value for key '{Key}'", kvp.Key);
                    throw new InvalidOperationException($"Failed to decrypt or deserialize value for key '{kvp.Key}'.", ex);
                }
            }
            return result;
        }

        private object ConvertBsonValueToJsonCompatible(object value)
        {
            if (value is JsonElement jsonElement)
            {
#pragma warning disable CS8603 // Possible null reference return.
                return jsonElement.ValueKind switch
                {
                    JsonValueKind.Object => jsonElement.EnumerateObject().ToDictionary(
                        prop => ToCamelCase(prop.Name),
                        prop => ConvertBsonValueToJsonCompatible(prop.Value)),
                    JsonValueKind.Array => jsonElement.EnumerateArray().Select(e => ConvertBsonValueToJsonCompatible(e)).ToList(),
                    JsonValueKind.String => jsonElement.GetString(),
                    JsonValueKind.Number => jsonElement.TryGetInt32(out int i) ? i : jsonElement.GetDouble(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.Null => null,
                    _ => throw new InvalidOperationException($"Unsupported JsonElement kind: {jsonElement.ValueKind}")
                };
#pragma warning restore CS8603 // Possible null reference return.
            }
            else if (value is IEnumerable<object> list)
            {
                return list.Select(ConvertBsonValueToJsonCompatible).ToList();
            }
            else if (value is IDictionary<string, object> dict)
            {
                return ConvertBsonToJsonCompatible(dict);
            }
            return value; // Return primitive types as-is
        }

        private object ConvertToBsonCompatible(object value)
        {
            if (value is JsonElement jsonElement)
            {
                return jsonElement.ValueKind switch
                {
                    JsonValueKind.Object => BsonDocument.Parse(jsonElement.GetRawText()),
                    JsonValueKind.Array => jsonElement.EnumerateArray().Select(e => ConvertToBsonCompatible(e)).ToList(),
                    JsonValueKind.String => ConvertStringToBsonCompatible(jsonElement.GetString()),
                    JsonValueKind.Number => jsonElement.TryGetInt32(out int i) ? i : jsonElement.GetDouble(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.Null => null,
                    _ => throw new InvalidOperationException($"Unsupported JsonElement kind: {jsonElement.ValueKind}")
                };
            }
            else if (value is string str)
            {
                return ConvertStringToBsonCompatible(str);
            }
            else if (value is IEnumerable<object> list)
            {
                return list.Select(ConvertToBsonCompatible).ToList();
            }
            else if (value is IDictionary<string, object> dict)
            {
                var bsonDoc = new BsonDocument();
                foreach (var kvp in dict)
                {
                    bsonDoc.Add(kvp.Key, BsonDocumentWrapper.Create(ConvertToBsonCompatible(kvp.Value)));
                }
                return bsonDoc;
            }
            return value; // Return primitive types (int, bool, etc.) as-is
        }

        private static string ToCamelCase(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            if (char.IsLower(input[0]))
                return input;

            return char.ToLowerInvariant(input[0]) + input.Substring(1);
        }

        private byte[] InitializeEncryptionKey()
        {
            try
            {
                // Opcional: Recuperar la clave desde Azure Key Vault
                // Descomentar y configurar en producción
                /*
                var keyVaultUri = "https://my-keyvault.vault.azure.net/";
                var client = new SecretClient(new Uri(keyVaultUri), new DefaultAzureCredential());
                var secret = client.GetSecretAsync("TenantEncryptionKey").GetAwaiter().GetResult();
                return Convert.FromBase64String(secret.Value);
                */

                // Para pruebas: Usar el Base64Key definido
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
    }
}
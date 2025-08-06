using GdprConfigurations;
using GdprServices.AuditLogs;
using GdprServices.DataExporter;
using Microsoft.Extensions.Logging;
using Models.AuditLog;
using Models.Enums;
using Models.Tenants;
using MongoDB.Bson;
using MongoDB.Driver;
using Repositories.Interfaces;
using System.Security.Cryptography;
using System.Text.Json;

namespace GdprServices.Audience
{
    public class TenantAudienceService : ITenantAudience
    {
        private readonly ITenantAudienceRepository _repository;
        private readonly ILogger<TenantAudienceService> _logger;
        private readonly IAuditLogs _auditLogs;
        private static readonly string Base64Key = "ASNFZ4mrze/+3LqYdlQyEBEiM0RVV2aHiZqrzN3u/wA=";
        private readonly byte[] EncryptionKey;

        public TenantAudienceService(
            ITenantAudienceRepository repository,
            ILogger<TenantAudienceService> logger,
            IAuditLogs auditLogs,
            IDataFormatter dataFormatter)
        {
            _repository = repository;
            _logger = logger;
            _auditLogs = auditLogs;
            EncryptionKey = InitializeEncryptionKey();

            // Create index on PseudonymMapping for efficient retrieval
            _repository.CreatePseudonymMappingIndex();
        }

        public async Task<string> SaveTenantAudienceAsync(TenantAudience tenantAudience, string clientIdFromHeader)
        {
            if (tenantAudience == null || string.IsNullOrEmpty(tenantAudience.TenantId))
            {
                await LogAuditAsync(
                    tenantId: tenantAudience?.TenantId,
                    performedBy: null,
                    actorType: ActorType.System,
                    actionType: AuditActionType.Create,
                    targetEntity: TargetEntityType.Tenant,
                    targetEntityId: tenantAudience?.TenantId,
                    details: new Dictionary<string, object> { { "Error", "Attempted to save tenant audience with null or empty TenantId." } },
                    isSuccess: false,
                    logErrorContext: "tenant creation"
                );
                _logger.LogWarning("SaveTenantAudienceAsync called with null or empty tenantAudience/TenantId.");
                throw new ArgumentNullException(nameof(tenantAudience), "TenantAudience and TenantId cannot be null or empty.");
            }

            if (string.IsNullOrEmpty(clientIdFromHeader))
            {
                await LogAuditAsync(
                    tenantId: tenantAudience.TenantId,
                    performedBy: null,
                    actorType: ActorType.System,
                    actionType: AuditActionType.Create,
                    targetEntity: TargetEntityType.Tenant,
                    targetEntityId: tenantAudience.TenantId,
                    details: new Dictionary<string, object> { { "Error", "Attempted to save tenant audience with null or empty ClientId." } },
                    isSuccess: false,
                    logErrorContext: "tenant creation"
                );
                _logger.LogWarning("SaveTenantAudienceAsync called with null or empty clientIdFromHeader for TenantId: {TenantId}", tenantAudience.TenantId);
                throw new ArgumentNullException(nameof(clientIdFromHeader), "ClientId cannot be null or empty.");
            }

            // Verify tenant exists and client is authorized
            var tenant = await _repository.GetTenantByIdAsync(tenantAudience.TenantId);
            if (tenant == null)
            {
                await LogAuditAsync(
                    tenantId: tenantAudience.TenantId,
                    performedBy: null,
                    actorType: ActorType.System,
                    actionType: AuditActionType.Create,
                    targetEntity: TargetEntityType.Tenant,
                    targetEntityId: tenantAudience.TenantId,
                    details: new Dictionary<string, object> { { "Error", "Tenant not found for audience data." } },
                    isSuccess: false,
                    logErrorContext: "tenant creation"
                );
                _logger.LogWarning("Tenant not found for TenantId: {TenantId}", tenantAudience.TenantId);
                throw new InvalidOperationException("Tenant not found.");
            }

            if (tenant.ClientId != clientIdFromHeader)
            {
                await LogAuditAsync(
                    tenantId: tenantAudience.TenantId,
                    performedBy: null,
                    actorType: ActorType.System,
                    actionType: AuditActionType.Create,
                    targetEntity: TargetEntityType.Tenant,
                    targetEntityId: tenantAudience.TenantId,
                    details: new Dictionary<string, object> { { "Error", $"Unauthorized access: Invalid ClientId {clientIdFromHeader}." } },
                    isSuccess: false,
                    logErrorContext: "tenant creation"
                );
                _logger.LogWarning("Unauthorized access for TenantId: {TenantId} with ClientId: {ClientId}", tenantAudience.TenantId, clientIdFromHeader);
                throw new UnauthorizedAccessException("Invalid ClientId.");
            }

            // Verify tenant consent for data processing
            if (!tenant.ConsentAccepted)
            {
                await LogAuditAsync(
                    tenantId: tenantAudience.TenantId,
                    performedBy: null,
                    actorType: ActorType.System,
                    actionType: AuditActionType.Create,
                    targetEntity: TargetEntityType.Tenant,
                    targetEntityId: tenantAudience.TenantId,
                    details: new Dictionary<string, object> { { "Error", "Tenant has not provided consent for data processing." } },
                    isSuccess: false,
                    logErrorContext: "tenant creation"
                );
                _logger.LogWarning("Tenant has not provided consent for TenantId: {TenantId}", tenantAudience.TenantId);
                throw new InvalidOperationException("Tenant consent is required for data processing.");
            }

            // Validate and prepare Details dictionary
            Dictionary<string, object>? detailsDict = null;
            if (tenantAudience.Details != null && tenantAudience.Details.Any())
            {
                try
                {
                    detailsDict = new Dictionary<string, object>();
                    foreach (var kvp in tenantAudience.Details)
                    {
                        try
                        {
                            // Convert value to JSON string
                            string jsonValue = JsonSerializer.Serialize(kvp.Value, new JsonSerializerOptions
                            {
                                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                            });
                            // Encrypt the JSON string to byte[]
                            byte[] encryptedValue = EncryptionProvider.EncryptString(jsonValue, EncryptionKey);
                            detailsDict.Add(kvp.Key, encryptedValue); // Store byte[] directly
                        }
                        catch (Exception ex)
                        {
                            await LogAuditAsync(
                                tenantId: tenantAudience.TenantId,
                                performedBy: null,
                                actorType: ActorType.System,
                                actionType: AuditActionType.Create,
                                targetEntity: TargetEntityType.Tenant,
                                targetEntityId: tenantAudience.TenantId,
                                details: new Dictionary<string, object> { { "Error", $"Failed to serialize or encrypt Details key '{kvp.Key}': {ex.Message}" } },
                                isSuccess: false,
                                logErrorContext: "tenant creation"
                            );
                            _logger.LogError(ex, "Serialization/encryption failed for Details key '{Key}' for TenantId: {TenantId}", kvp.Key, tenantAudience.TenantId);
                            throw new InvalidOperationException($"Failed to serialize or encrypt Details key '{kvp.Key}'.", ex);
                        }
                    }
                }
                catch (Exception ex)
                {
                    await LogAuditAsync(
                        tenantId: tenantAudience.TenantId,
                        performedBy: null,
                        actorType: ActorType.System,
                        actionType: AuditActionType.Create,
                        targetEntity: TargetEntityType.Tenant,
                        targetEntityId: tenantAudience.TenantId,
                        details: new Dictionary<string, object> { { "Error", $"Failed to process Details dictionary: {ex.Message}" } },
                        isSuccess: false,
                        logErrorContext: "tenant creation"
                    );
                    _logger.LogError(ex, "Failed to process Details dictionary for TenantId: {TenantId}. StackTrace: {StackTrace}", tenantAudience.TenantId, ex.StackTrace);
                    throw new InvalidOperationException("Failed to process audience details.", ex);
                }
            }

            // Prepare TenantAudience for storage
            var audienceToSave = new TenantAudience
            {
                Id = string.IsNullOrEmpty(tenantAudience.Id) ? ObjectId.GenerateNewId().ToString() : tenantAudience.Id,
                TenantId = tenantAudience.TenantId,
                Details = detailsDict
            };

            // Save to MongoDB
            try
            {
                await _repository.InsertTenantAudienceAsync(audienceToSave);
            }
            catch (MongoException ex)
            {
                await LogAuditAsync(
                    tenantId: tenantAudience.TenantId,
                    performedBy: null,
                    actorType: ActorType.System,
                    actionType: AuditActionType.Create,
                    targetEntity: TargetEntityType.Tenant,
                    targetEntityId: tenantAudience.TenantId,
                    details: new Dictionary<string, object> { { "Error", $"MongoDB error while saving tenant audience data: {ex.Message}" } },
                    isSuccess: false,
                    logErrorContext: "tenant creation"
                );
                _logger.LogError(ex, "MongoException occurred while saving tenant audience for TenantId: {TenantId}. StackTrace: {StackTrace}", tenantAudience.TenantId, ex.StackTrace);
                throw new InvalidOperationException("Failed to save tenant audience data due to database error.", ex);
            }
            catch (Exception ex)
            {
                await LogAuditAsync(
                    tenantId: tenantAudience.TenantId,
                    performedBy: null,
                    actorType: ActorType.System,
                    actionType: AuditActionType.Create,
                    targetEntity: TargetEntityType.Tenant,
                    targetEntityId: tenantAudience.TenantId,
                    details: new Dictionary<string, object> { { "Error", $"Unexpected error while saving tenant audience data: {ex.Message}" } },
                    isSuccess: false,
                    logErrorContext: "tenant creation"
                );
                _logger.LogError(ex, "Unexpected error while saving tenant audience for TenantId: {TenantId}. StackTrace: {StackTrace}", tenantAudience.TenantId, ex.StackTrace);
                throw new InvalidOperationException("Unexpected error while saving tenant audience data.", ex);
            }

            await LogAuditAsync(
                tenantId: tenantAudience.TenantId,
                performedBy: tenant.Email,
                actorType: ActorType.User,
                actionType: AuditActionType.Create,
                targetEntity: TargetEntityType.TenantAudience,
                targetEntityId: audienceToSave.Id,
                details: new Dictionary<string, object> { { "Action", "Tenant audience data saved" } },
                isSuccess: true,
                logErrorContext: "tenant audience save"
            );

            _logger.LogInformation("Tenant audience data saved successfully for TenantId: {TenantId}, AudienceId: {AudienceId}", tenantAudience.TenantId, audienceToSave.Id);
            return "Tenant audience data saved successfully.";
        }

        public async Task<List<TenantAudience>> GetTenantAudiencesByTenantIdAsync(string tenantId, string clientIdFromHeader)
        {
            if (string.IsNullOrEmpty(tenantId))
            {
                await LogAuditAsync(
                    tenantId: tenantId,
                    performedBy: null,
                    actorType: ActorType.System,
                    actionType: AuditActionType.Access,
                    targetEntity: TargetEntityType.Tenant,
                    targetEntityId: tenantId,
                    details: new Dictionary<string, object> { { "Error", "Attempted to retrieve tenant audiences with null or empty TenantId." } },
                    isSuccess: false,
                    logErrorContext: "tenant audience list access"
                );
                _logger.LogWarning("GetTenantAudiencesByTenantIdAsync called with null or empty tenantId.");
                throw new ArgumentNullException(nameof(tenantId), "Tenant ID cannot be null or empty.");
            }

            if (string.IsNullOrEmpty(clientIdFromHeader))
            {
                await LogAuditAsync(
                    tenantId: tenantId,
                    performedBy: null,
                    actorType: ActorType.System,
                    actionType: AuditActionType.Access,
                    targetEntity: TargetEntityType.Tenant,
                    targetEntityId: tenantId,
                    details: new Dictionary<string, object> { { "Error", "Attempted to retrieve tenant audiences with null or empty ClientId." } },
                    isSuccess: false,
                    logErrorContext: "tenant audience list access"
                );
                _logger.LogWarning("GetTenantAudiencesByTenantIdAsync called with null or empty clientIdFromHeader for TenantId: {TenantId}", tenantId);
                throw new ArgumentNullException(nameof(clientIdFromHeader), "ClientId cannot be null or empty.");
            }

            // Verify tenant exists and client is authorized
            var tenant = await _repository.GetTenantByIdAsync(tenantId);
            if (tenant == null)
            {
                await LogAuditAsync(
                    tenantId: tenantId,
                    performedBy: null,
                    actorType: ActorType.System,
                    actionType: AuditActionType.Access,
                    targetEntity: TargetEntityType.Tenant,
                    targetEntityId: tenantId,
                    details: new Dictionary<string, object> { { "Error", "Tenant not found for audience data." } },
                    isSuccess: false,
                    logErrorContext: "tenant audience list access"
                );
                _logger.LogWarning("Tenant not found for TenantId: {TenantId}", tenantId);
                throw new InvalidOperationException("Tenant not found.");
            }

            if (tenant.ClientId != clientIdFromHeader)
            {
                await LogAuditAsync(
                    tenantId: tenantId,
                    performedBy: null,
                    actorType: ActorType.System,
                    actionType: AuditActionType.Access,
                    targetEntity: TargetEntityType.Tenant,
                    targetEntityId: tenantId,
                    details: new Dictionary<string, object> { { "Error", $"Unauthorized access: Invalid ClientId {clientIdFromHeader}." } },
                    isSuccess: false,
                    logErrorContext: "tenant audience list access"
                );
                _logger.LogWarning("Unauthorized access for TenantId: {TenantId} with ClientId: {ClientId}", tenantId, clientIdFromHeader);
                throw new UnauthorizedAccessException("Invalid ClientId.");
            }

            // Verify tenant consent
            if (!tenant.ConsentAccepted)
            {
                await LogAuditAsync(
                    tenantId: tenantId,
                    performedBy: null,
                    actorType: ActorType.System,
                    actionType: AuditActionType.Access,
                    targetEntity: TargetEntityType.Tenant,
                    targetEntityId: tenantId,
                    details: new Dictionary<string, object> { { "Error", "Tenant has not provided consent for data access." } },
                    isSuccess: false,
                    logErrorContext: "tenant audience list access"
                );
                _logger.LogWarning("Tenant has not provided consent for TenantId: {TenantId}", tenantId);
                throw new InvalidOperationException("Tenant consent is required for data access.");
            }

            // Retrieve all TenantAudience records for the TenantId
            var tenantAudiences = await _repository.GetTenantAudiencesByTenantIdAsync(tenantId);

            // Convert BsonDocument values to JSON-compatible types with decryption
            var convertedAudiences = tenantAudiences.Select(audience =>
            {
                var convertedAudience = new TenantAudience
                {
                    Id = audience.Id,
                    TenantId = audience.TenantId,
                    Details = audience.Details != null ? ConvertBsonToJsonCompatible(audience.Details) : null
                };
                return convertedAudience;
            }).ToList();

            await LogAuditAsync(
                tenantId: tenantId,
                performedBy: tenant.Email,
                actorType: ActorType.User,
                actionType: AuditActionType.Access,
                targetEntity: TargetEntityType.TenantAudience,
                targetEntityId: tenantId,
                details: new Dictionary<string, object> { { "Action", $"Retrieved {tenantAudiences.Count} tenant audience records with decryption" } },
                isSuccess: true,
                logErrorContext: "tenant audience list access"
            );

            _logger.LogInformation("Retrieved {Count} tenant audience records for TenantId: {TenantId}", tenantAudiences.Count, tenantId);
            return convertedAudiences;
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
            return value;
        }

        private object ConvertStringToBsonCompatible(string? value)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase))
                return true;
            if (string.Equals(value, "false", StringComparison.OrdinalIgnoreCase))
                return false;

            return value;
        }

        private Dictionary<string, object> ConvertBsonToJsonCompatible(IDictionary<string, object> dict)
        {
            var result = new Dictionary<string, object>();
            foreach (var kvp in dict)
            {
                string camelCaseKey = ToCamelCase(kvp.Key);
                try
                {
                    if (kvp.Value is byte[] encryptedValue)
                    {
                        string jsonValue = EncryptionProvider.DecryptString(encryptedValue, EncryptionKey);
                        object decryptedValue = JsonSerializer.Deserialize<object>(jsonValue, new JsonSerializerOptions
                        {
                            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                        });
                        result[camelCaseKey] = ConvertBsonValueToJsonCompatible(decryptedValue);
                    }
                    else
                    {
                        result[camelCaseKey] = null;
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
            }
            else if (value is IEnumerable<object> list)
            {
                return list.Select(ConvertBsonValueToJsonCompatible).ToList();
            }
            else if (value is IDictionary<string, object> dict)
            {
                return ConvertBsonToJsonCompatible(dict);
            }
            return value;
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
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log audit for {Context}, TenantId: {TenantId}", logErrorContext, tenantId);
            }
        }
    }
}
namespace Models.Enums
{
    /// <summary>
    /// Defines the types of entities that can be affected by actions in the audit log.
    /// Each value represents a distinct entity type that may involve personal data or GDPR-related operations,
    /// ensuring compliance with GDPR's accountability and traceability requirements (Articles 5(2), 30).
    /// </summary>
    public enum TargetEntityType
    {
        /// <summary>
        /// A tenant in the multi-tenant system, representing an organization or account.
        /// Relevant for GDPR as tenants may contain personal data of users.
        /// </summary>
        Tenant,

        /// <summary>
        /// An individual user, typically associated with personal data (e.g., email, name).
        /// Critical for tracking user-related GDPR actions like data access or erasure.
        /// </summary>
        User,

        /// <summary>
        /// A document containing personal data (e.g., user-uploaded files, records).
        /// Relevant for GDPR data processing and storage limitations.
        /// </summary>
        Document,

        /// <summary>
        /// A consent record, tracking user consent for data processing (e.g., marketing, analytics).
        /// Essential for GDPR compliance under Article 7.
        /// </summary>
        Consent,

        /// <summary>
        /// A user profile, containing personal details like preferences or contact information.
        /// Relevant for GDPR data subject rights (e.g., access, rectification).
        /// </summary>
        Profile,

        /// <summary>
        /// A role assigned to a user, affecting data access permissions.
        /// Relevant for GDPR to track access control changes (Article 32).
        /// </summary>
        Role,

        /// <summary>
        /// A specific permission or access right granted to a user or role.
        /// Tracks changes to data access policies for GDPR compliance.
        /// </summary>
        Permission,

        /// <summary>
        /// An API key or token used for system access, potentially linked to personal data processing.
        /// Relevant for GDPR security and access control (Article 32).
        /// </summary>
        ApiKey,

        /// <summary>
        /// System or user settings, which may include privacy preferences.
        /// Relevant for GDPR when settings affect data processing (e.g., notification preferences).
        /// </summary>
        Settings,

        /// <summary>
        /// A data export request, typically for GDPR data portability (Article 20).
        /// Tracks user requests to export their personal data.
        /// </summary>
        DataExport,

        /// <summary>
        /// A data erasure request, for GDPR right to be forgotten (Article 17).
        /// Tracks user requests to delete their personal data.
        /// </summary>
        DataErasure,

        /// <summary>
        /// A session record, tracking user login/logout activities.
        /// Relevant for GDPR to monitor access to personal data (Article 30).
        /// </summary>
        Session,

        /// <summary>
        /// A security event, such as failed login attempts or account lockouts.
        /// Relevant for GDPR security monitoring (Article 32).
        /// </summary>
        SecurityEvent,

        /// <summary>
        /// A data processing activity record, as required by GDPR Article 30.
        /// Tracks specific data processing operations for compliance reporting.
        /// </summary>
        ProcessingActivity,

        /// <summary>
        /// A data breach incident, required for GDPR notification obligations (Article 33).
        /// Tracks incidents involving personal data breaches.
        /// </summary>
        DataBreach,

        /// <summary>
        /// A third-party data sharing agreement or record.
        /// Relevant for GDPR to track data transfers to processors or controllers (Article 28).
        /// </summary>
        DataSharing,
        TenantAudience
    }
}
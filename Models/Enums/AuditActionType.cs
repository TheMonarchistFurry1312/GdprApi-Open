namespace Models.Enums
{
    public enum AuditActionType
    {
        Create,             // New data or resource created
        Update,             // Data or resource updated
        Delete,             // Data or resource deleted
        Access,             // Data accessed or viewed
        Login,              // User logged in
        Logout,             // User logged out
        ConsentGiven,       // User gave consent
        ConsentRevoked,     // User revoked consent
        DataExported,       // User data exported (e.g., data portability request)
        DataErased,         // User data erased/deleted upon request
        PermissionChanged,  // Changes to permissions or roles
        PasswordChanged,    // Password updated
        RoleAssigned,       // Role granted to a user
        RoleRevoked,        // Role removed from a user
        AccountLocked,      // Account locked (e.g., after failed login attempts)
        AccountUnlocked,    // Account unlocked
        TwoFactorEnabled,   // 2FA enabled
        TwoFactorDisabled,  // 2FA disabled
        ProfileViewed,      // User profile viewed by admin or system
        SettingsChanged,    // User or admin changed settings/preferences
        LoginFailed,        // Failed login attempt
        SessionExpired,     // User session expired
        ApiKeyCreated,      // API key or token created
        ApiKeyRevoked,      // API key or token revoked
        Authentication,     // API Authentication
        Download            // Download file

    }
}

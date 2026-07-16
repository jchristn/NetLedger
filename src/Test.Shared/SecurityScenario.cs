namespace Test.Shared
{
    using System;
    using System.Threading.Tasks;
    using NetLedger;
    using NetLedger.Server.API.Agnostic;
    using NetLedger.Server.Authentication;

    internal sealed class SecurityScenario : IAsyncDisposable
    {
        /// <summary>
        /// Instantiate the security scenario.
        /// </summary>
        /// <param name="ledger">Ledger instance.</param>
        /// <param name="authorization">Authorization service.</param>
        /// <param name="authService">Authentication service.</param>
        /// <param name="credentialHandler">Credential handler.</param>
        /// <param name="accountHandler">Account handler.</param>
        /// <param name="entryHandler">Entry handler.</param>
        /// <param name="balanceHandler">Balance handler.</param>
        /// <param name="identityHandler">Identity handler.</param>
        /// <param name="tenantA">First tenant.</param>
        /// <param name="tenantB">Second tenant.</param>
        /// <param name="systemAdmin">System administrator.</param>
        /// <param name="tenantAAdmin">Tenant A administrator.</param>
        /// <param name="tenantBAdmin">Tenant B administrator.</param>
        /// <param name="tenantAUser">Tenant A user.</param>
        /// <param name="tenantAOtherUser">Second Tenant A user.</param>
        /// <param name="tenantBUser">Tenant B user.</param>
        /// <param name="tenantAUserAccountId">Tenant A mapped account identifier.</param>
        /// <param name="tenantAUnmappedAccountId">Tenant A unmapped account identifier.</param>
        /// <param name="tenantBAccountId">Tenant B account identifier.</param>
        /// <param name="tenantAUserCredentialId">Tenant A user credential identifier.</param>
        /// <param name="tenantAOtherUserCredentialId">Tenant A other user credential identifier.</param>
        /// <param name="tenantBUserCredentialId">Tenant B user credential identifier.</param>
        public SecurityScenario(
            Ledger ledger,
            AuthorizationService authorization,
            AuthService authService,
            ApiKeyHandler credentialHandler,
            AccountHandler accountHandler,
            EntryHandler entryHandler,
            BalanceHandler balanceHandler,
            IdentityHandler identityHandler,
            Tenant tenantA,
            Tenant tenantB,
            User systemAdmin,
            User tenantAAdmin,
            User tenantBAdmin,
            User tenantAUser,
            User tenantAOtherUser,
            User tenantBUser,
            string tenantAUserAccountId,
            string tenantAUnmappedAccountId,
            string tenantBAccountId,
            string tenantAUserCredentialId,
            string tenantAOtherUserCredentialId,
            string tenantBUserCredentialId)
        {
            Ledger = ledger;
            Authorization = authorization;
            AuthService = authService;
            CredentialHandler = credentialHandler;
            AccountHandler = accountHandler;
            EntryHandler = entryHandler;
            BalanceHandler = balanceHandler;
            IdentityHandler = identityHandler;
            TenantA = tenantA;
            TenantB = tenantB;
            SystemAdmin = systemAdmin;
            TenantAAdmin = tenantAAdmin;
            TenantBAdmin = tenantBAdmin;
            TenantAUser = tenantAUser;
            TenantAOtherUser = tenantAOtherUser;
            TenantBUser = tenantBUser;
            TenantAUserAccountId = tenantAUserAccountId;
            TenantAUnmappedAccountId = tenantAUnmappedAccountId;
            TenantBAccountId = tenantBAccountId;
            TenantAUserCredentialId = tenantAUserCredentialId;
            TenantAOtherUserCredentialId = tenantAOtherUserCredentialId;
            TenantBUserCredentialId = tenantBUserCredentialId;
        }

        /// <summary>
        /// Ledger instance.
        /// </summary>
        public Ledger Ledger { get; }

        /// <summary>
        /// Authorization service.
        /// </summary>
        public AuthorizationService Authorization { get; }

        /// <summary>
        /// Authentication service.
        /// </summary>
        public AuthService AuthService { get; }

        /// <summary>
        /// Credential handler.
        /// </summary>
        public ApiKeyHandler CredentialHandler { get; }

        /// <summary>
        /// Account handler.
        /// </summary>
        public AccountHandler AccountHandler { get; }

        /// <summary>
        /// Entry handler.
        /// </summary>
        public EntryHandler EntryHandler { get; }

        /// <summary>
        /// Balance handler.
        /// </summary>
        public BalanceHandler BalanceHandler { get; }

        /// <summary>
        /// Identity handler.
        /// </summary>
        public IdentityHandler IdentityHandler { get; }

        /// <summary>
        /// First tenant.
        /// </summary>
        public Tenant TenantA { get; }

        /// <summary>
        /// Second tenant.
        /// </summary>
        public Tenant TenantB { get; }

        /// <summary>
        /// System administrator.
        /// </summary>
        public User SystemAdmin { get; }

        /// <summary>
        /// Tenant A administrator.
        /// </summary>
        public User TenantAAdmin { get; }

        /// <summary>
        /// Tenant B administrator.
        /// </summary>
        public User TenantBAdmin { get; }

        /// <summary>
        /// Tenant A user.
        /// </summary>
        public User TenantAUser { get; }

        /// <summary>
        /// Tenant A other user.
        /// </summary>
        public User TenantAOtherUser { get; }

        /// <summary>
        /// Tenant B user.
        /// </summary>
        public User TenantBUser { get; }

        /// <summary>
        /// Tenant A mapped account identifier.
        /// </summary>
        public string TenantAUserAccountId { get; }

        /// <summary>
        /// Tenant A unmapped account identifier.
        /// </summary>
        public string TenantAUnmappedAccountId { get; }

        /// <summary>
        /// Tenant B account identifier.
        /// </summary>
        public string TenantBAccountId { get; }

        /// <summary>
        /// Tenant A user credential identifier.
        /// </summary>
        public string TenantAUserCredentialId { get; }

        /// <summary>
        /// Tenant A other user credential identifier.
        /// </summary>
        public string TenantAOtherUserCredentialId { get; }

        /// <summary>
        /// Tenant B user credential identifier.
        /// </summary>
        public string TenantBUserCredentialId { get; }

        /// <summary>
        /// Dispose resources.
        /// </summary>
        /// <returns>Value task.</returns>
        public async ValueTask DisposeAsync()
        {
            AuthService.Dispose();
            await Ledger.DisposeAsync().ConfigureAwait(false);
        }
    }
}

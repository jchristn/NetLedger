namespace NetLedger.Archive.Server.API.Routes
{
    using System;
    using System.Net;
    using System.Threading.Tasks;
    using WatsonWebserver;
    using WatsonWebserver.Core;

    internal static class ArchiveMutationRejectionRoutes
    {
        internal static void Register(
            Webserver webserver,
            string prefix,
            Func<HttpContextBase, Task> mutationNotAllowedAsync,
            Func<HttpContextBase, Exception, Task> exceptionHandler)
        {
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.PUT, prefix + "/accounts", mutationNotAllowedAsync, exceptionHandler);
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.DELETE, prefix + "/accounts/{accountId}", mutationNotAllowedAsync, exceptionHandler);
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.PUT, prefix + "/tenants/{tenantId}/accounts", mutationNotAllowedAsync, exceptionHandler);
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.DELETE, prefix + "/tenants/{tenantId}/accounts/{accountId}", mutationNotAllowedAsync, exceptionHandler);
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.PUT, prefix + "/accounts/{accountId}/credits", mutationNotAllowedAsync, exceptionHandler);
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.PUT, prefix + "/accounts/{accountId}/debits", mutationNotAllowedAsync, exceptionHandler);
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, prefix + "/accounts/{accountId}/commit", mutationNotAllowedAsync, exceptionHandler);
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.DELETE, prefix + "/accounts/{accountId}/entries/{entryId}", mutationNotAllowedAsync, exceptionHandler);
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.PUT, prefix + "/tenants/{tenantId}/accounts/{accountId}/credits", mutationNotAllowedAsync, exceptionHandler);
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.PUT, prefix + "/tenants/{tenantId}/accounts/{accountId}/debits", mutationNotAllowedAsync, exceptionHandler);
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, prefix + "/tenants/{tenantId}/accounts/{accountId}/commit", mutationNotAllowedAsync, exceptionHandler);
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.DELETE, prefix + "/tenants/{tenantId}/accounts/{accountId}/entries/{entryId}", mutationNotAllowedAsync, exceptionHandler);
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.PUT, prefix + "/tenants", mutationNotAllowedAsync, exceptionHandler);
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.DELETE, prefix + "/tenants/{tenantId}", mutationNotAllowedAsync, exceptionHandler);
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.PUT, prefix + "/tenants/{tenantId}/users", mutationNotAllowedAsync, exceptionHandler);
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.PUT, prefix + "/tenants/{tenantId}/roles", mutationNotAllowedAsync, exceptionHandler);
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.PUT, prefix + "/tenants/{tenantId}/permissions", mutationNotAllowedAsync, exceptionHandler);
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.PUT, prefix + "/tenants/{tenantId}/users/{userId}/roles", mutationNotAllowedAsync, exceptionHandler);
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.PUT, prefix + "/tenants/{tenantId}/roles/{roleId}/permissions/{permissionId}", mutationNotAllowedAsync, exceptionHandler);
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.PUT, prefix + "/credentials", mutationNotAllowedAsync, exceptionHandler);
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.DELETE, prefix + "/credentials/{credentialId}", mutationNotAllowedAsync, exceptionHandler);
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.PUT, prefix + "/tenants/{tenantId}/credentials", mutationNotAllowedAsync, exceptionHandler);
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.DELETE, prefix + "/tenants/{tenantId}/credentials/{credentialId}", mutationNotAllowedAsync, exceptionHandler);
        }
    }
}

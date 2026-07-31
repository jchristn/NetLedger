namespace NetLedger.Archive.Server.API.Routes
{
    using System;
    using System.Net;
    using System.Threading.Tasks;
    using WatsonWebserver;
    using WatsonWebserver.Core;

    internal static class ArchivedLedgerRoutes
    {
        internal static void Register(
            Webserver webserver,
            string prefix,
            Func<HttpContextBase, Task> getArchivedEntriesAsync,
            Func<HttpContextBase, Task> getArchivedBalanceAsOfAsync,
            Func<HttpContextBase, Task> verifyArchivedBalanceChainAsync,
            Func<HttpContextBase, Exception, Task> exceptionHandler)
        {
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, prefix + "/archive/accounts/{accountId}/entries", getArchivedEntriesAsync, exceptionHandler);
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, prefix + "/archive/accounts/{accountId}/balance/asof", getArchivedBalanceAsOfAsync, exceptionHandler);
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, prefix + "/archive/accounts/{accountId}/verify", verifyArchivedBalanceChainAsync, exceptionHandler);
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, prefix + "/tenants/{tenantId}/accounts/{accountId}/entries", getArchivedEntriesAsync, exceptionHandler);
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, prefix + "/tenants/{tenantId}/accounts/{accountId}/entries/enumerate", getArchivedEntriesAsync, exceptionHandler);
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, prefix + "/tenants/{tenantId}/accounts/{accountId}/balance/asof", getArchivedBalanceAsOfAsync, exceptionHandler);
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, prefix + "/tenants/{tenantId}/accounts/{accountId}/verify", verifyArchivedBalanceChainAsync, exceptionHandler);
        }
    }
}

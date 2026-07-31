namespace NetLedger.Archive.Server.API.Routes
{
    using System;
    using System.Net;
    using System.Threading.Tasks;
    using WatsonWebserver;
    using WatsonWebserver.Core;

    internal static class ArchiveServerRequestHistoryRoutes
    {
        internal static void Register(
            Webserver webserver,
            string prefix,
            Func<HttpContextBase, Task> getArchiveServerRequestHistoryAsync,
            Func<HttpContextBase, Task> getArchiveServerRequestHistorySummaryAsync,
            Func<HttpContextBase, Task> getArchiveServerRequestHistoryEntryAsync,
            Func<HttpContextBase, Exception, Task> exceptionHandler)
        {
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.GET, prefix + "/archive-server/request-history", getArchiveServerRequestHistoryAsync, exceptionHandler);
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.GET, prefix + "/archive-server/request-history/summary", getArchiveServerRequestHistorySummaryAsync, exceptionHandler);
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, prefix + "/archive-server/request-history/{id}", getArchiveServerRequestHistoryEntryAsync, exceptionHandler);
        }
    }
}

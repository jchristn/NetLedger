namespace NetLedger.Archive.Server.API.Routes
{
    using System;
    using System.Net;
    using System.Threading.Tasks;
    using WatsonWebserver;
    using WatsonWebserver.Core;

    internal static class ArchivedRequestHistoryRoutes
    {
        internal static void Register(
            Webserver webserver,
            string prefix,
            Func<HttpContextBase, Task> getArchivedRequestHistoryAsync,
            Func<HttpContextBase, Task> getArchivedRequestHistorySummaryAsync,
            Func<HttpContextBase, Task> getArchivedRequestHistoryEntryAsync,
            Func<HttpContextBase, Exception, Task> exceptionHandler)
        {
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.GET, prefix + "/request-history", getArchivedRequestHistoryAsync, exceptionHandler);
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.GET, prefix + "/request-history/summary", getArchivedRequestHistorySummaryAsync, exceptionHandler);
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, prefix + "/request-history/{id}", getArchivedRequestHistoryEntryAsync, exceptionHandler);
        }
    }
}

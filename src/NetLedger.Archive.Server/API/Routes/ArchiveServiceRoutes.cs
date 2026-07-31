namespace NetLedger.Archive.Server.API.Routes
{
    using System;
    using System.Net;
    using System.Threading.Tasks;
    using WatsonWebserver;
    using WatsonWebserver.Core;

    internal static class ArchiveServiceRoutes
    {
        internal static void Register(
            Webserver webserver,
            Func<HttpContextBase, Task> existsAsync,
            Func<HttpContextBase, Task> getServiceAsync,
            Func<HttpContextBase, Task> getHealthAsync,
            Func<HttpContextBase, Task> openApiAsync)
        {
            webserver.Routes.PreAuthentication.Static.Add(HttpMethod.HEAD, "/", existsAsync);
            webserver.Routes.PreAuthentication.Static.Add(HttpMethod.GET, "/", getServiceAsync);
            webserver.Routes.PreAuthentication.Static.Add(HttpMethod.GET, "/v1/service", getServiceAsync);
            webserver.Routes.PreAuthentication.Static.Add(HttpMethod.GET, "/v1/health", getHealthAsync);
            webserver.Routes.PreAuthentication.Static.Add(HttpMethod.GET, "/api/v1/service", getServiceAsync);
            webserver.Routes.PreAuthentication.Static.Add(HttpMethod.GET, "/api/v1/health", getHealthAsync);
            webserver.Routes.PreAuthentication.Static.Add(HttpMethod.GET, "/openapi.json", openApiAsync);
        }
    }
}

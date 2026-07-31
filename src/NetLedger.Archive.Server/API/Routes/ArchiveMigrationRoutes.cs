namespace NetLedger.Archive.Server.API.Routes
{
    using System;
    using System.Net;
    using System.Threading.Tasks;
    using WatsonWebserver;
    using WatsonWebserver.Core;

    internal static class ArchiveMigrationRoutes
    {
        internal static void Register(
            Webserver webserver,
            string prefix,
            Func<HttpContextBase, Task> getArchiveMigrationsAsync,
            Func<HttpContextBase, Task> createArchiveMigrationAsync,
            Func<HttpContextBase, Task> getArchiveMigrationAsync,
            Func<HttpContextBase, Task> getArchiveMigrationBatchesAsync,
            Func<HttpContextBase, Task> createArchiveMigrationBatchAsync,
            Func<HttpContextBase, Task> uploadArchiveMigrationBatchContentAsync,
            Func<HttpContextBase, Task> sealArchiveMigrationAsync,
            Func<HttpContextBase, Task> commitArchiveMigrationAsync,
            Func<HttpContextBase, Task> abortArchiveMigrationAsync,
            Func<HttpContextBase, Exception, Task> exceptionHandler)
        {
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.GET, prefix + "/archive/migrations", getArchiveMigrationsAsync, exceptionHandler);
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.POST, prefix + "/archive/migrations", createArchiveMigrationAsync, exceptionHandler);
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, prefix + "/archive/migrations/{migrationId}", getArchiveMigrationAsync, exceptionHandler);
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, prefix + "/archive/migrations/{migrationId}/batches", getArchiveMigrationBatchesAsync, exceptionHandler);
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, prefix + "/archive/migrations/{migrationId}/batches", createArchiveMigrationBatchAsync, exceptionHandler);
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.PUT, prefix + "/archive/migrations/{migrationId}/batches/{batchId}/content", uploadArchiveMigrationBatchContentAsync, exceptionHandler);
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, prefix + "/archive/migrations/{migrationId}/seal", sealArchiveMigrationAsync, exceptionHandler);
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, prefix + "/archive/migrations/{migrationId}/commit", commitArchiveMigrationAsync, exceptionHandler);
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, prefix + "/archive/migrations/{migrationId}/abort", abortArchiveMigrationAsync, exceptionHandler);
        }
    }
}

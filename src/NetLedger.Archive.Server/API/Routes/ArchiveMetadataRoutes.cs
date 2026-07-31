namespace NetLedger.Archive.Server.API.Routes
{
    using System;
    using System.Net;
    using System.Threading.Tasks;
    using WatsonWebserver;
    using WatsonWebserver.Core;

    internal static class ArchiveMetadataRoutes
    {
        internal static void Register(
            Webserver webserver,
            string prefix,
            Func<HttpContextBase, Task> getArchiveRangesAsync,
            Func<HttpContextBase, Task> getArchiveManifestsAsync,
            Func<HttpContextBase, Task> getArchiveManifestAsync,
            Func<HttpContextBase, Task> getArchiveManifestObjectsAsync,
            Func<HttpContextBase, Task> getArchiveObjectMetadataAsync,
            Func<HttpContextBase, Task> getArchiveManifestCheckpointsAsync,
            Func<HttpContextBase, Task> archiveMetadataActionAsync,
            Func<HttpContextBase, Task> getArchiveStoragePoolsAsync,
            Func<HttpContextBase, Task> getStoragePoolHealthAsync,
            Func<HttpContextBase, Exception, Task> exceptionHandler)
        {
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.GET, prefix + "/archive/ranges", getArchiveRangesAsync, exceptionHandler);
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, prefix + "/tenants/{tenantId}/archive/ranges", getArchiveRangesAsync, exceptionHandler);
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, prefix + "/tenants/{tenantId}/accounts/{accountId}/archive/ranges", getArchiveRangesAsync, exceptionHandler);
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.GET, prefix + "/archive/manifests", getArchiveManifestsAsync, exceptionHandler);
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, prefix + "/archive/manifests/{manifestId}", getArchiveManifestAsync, exceptionHandler);
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, prefix + "/archive/manifests/{manifestId}/objects", getArchiveManifestObjectsAsync, exceptionHandler);
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, prefix + "/archive/objects/{objectId}/metadata", getArchiveObjectMetadataAsync, exceptionHandler);
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, prefix + "/archive/manifests/{manifestId}/checkpoints", getArchiveManifestCheckpointsAsync, exceptionHandler);
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, prefix + "/archive/manifests/{manifestId}/verify", archiveMetadataActionAsync, exceptionHandler);
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, prefix + "/archive/manifests/{manifestId}/quarantine", archiveMetadataActionAsync, exceptionHandler);
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, prefix + "/archive/manifests/{manifestId}/supersede", archiveMetadataActionAsync, exceptionHandler);
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.GET, prefix + "/archive/storage-pools", getArchiveStoragePoolsAsync, exceptionHandler);
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, prefix + "/archive/storage-pools/{storagePoolId}/health", getStoragePoolHealthAsync, exceptionHandler);
        }
    }
}

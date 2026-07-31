"""Archive Server operations for the NetLedger SDK."""

from typing import Any, Dict, List, Optional
from urllib.parse import quote, urlencode

from ..http_client import HttpClient
from ..models import Entry, EnumerationResult


def _append_query(path: str, query: Optional[Dict[str, Any]]) -> str:
    if not query:
        return path

    parameters = []
    for key, value in query.items():
        if value is None or value == "":
            continue

        if isinstance(value, (list, tuple)):
            parameters.append((key, ",".join(str(item) for item in value)))
        elif isinstance(value, dict):
            parameters.append((key, ",".join(f"{item_key}={item_value}" for item_key, item_value in value.items())))
        else:
            parameters.append((key, str(value)))

    if not parameters:
        return path

    return f"{path}?{urlencode(parameters)}"


class ArchiveMethods:
    """Archive Server operations for cold data and archive metadata."""

    def __init__(self, client: HttpClient):
        """Initialize archive methods."""
        self._client = client

    def health(self) -> Dict[str, Any]:
        """Read Archive Server health."""
        response = self._client.get("/v1/health")
        return response.data or {"Healthy": False}

    def ranges(self, query: Optional[Dict[str, Any]] = None) -> List[Dict[str, Any]]:
        """List archive coverage ranges."""
        response = self._client.get(_append_query("/v1/archive/ranges", query))
        return response.data or []

    def manifests(self, query: Optional[Dict[str, Any]] = None) -> List[Dict[str, Any]]:
        """List archive manifests."""
        response = self._client.get(_append_query("/v1/archive/manifests", query))
        return response.data or []

    def manifest(self, manifest_id: str) -> Dict[str, Any]:
        """Read an archive manifest."""
        response = self._client.get(f"/v1/archive/manifests/{quote(manifest_id, safe='')}")
        return response.data or {}

    def manifest_objects(self, manifest_id: str, query: Optional[Dict[str, Any]] = None) -> List[Dict[str, Any]]:
        """List objects for an archive manifest."""
        path = f"/v1/archive/manifests/{quote(manifest_id, safe='')}/objects"
        response = self._client.get(_append_query(path, query))
        return response.data or []

    def manifest_checkpoints(self, manifest_id: str, query: Optional[Dict[str, Any]] = None) -> List[Dict[str, Any]]:
        """List balance checkpoints for an archive manifest."""
        path = f"/v1/archive/manifests/{quote(manifest_id, safe='')}/checkpoints"
        response = self._client.get(_append_query(path, query))
        return response.data or []

    def verify_manifest(self, manifest_id: str) -> None:
        """Verify an archive manifest."""
        self._client.post(f"/v1/archive/manifests/{quote(manifest_id, safe='')}/verify")

    def quarantine_manifest(self, manifest_id: str) -> None:
        """Quarantine an archive manifest."""
        self._client.post(f"/v1/archive/manifests/{quote(manifest_id, safe='')}/quarantine")

    def supersede_manifest(self, manifest_id: str) -> None:
        """Supersede an archive manifest."""
        self._client.post(f"/v1/archive/manifests/{quote(manifest_id, safe='')}/supersede")

    def storage_pools(self, query: Optional[Dict[str, Any]] = None) -> List[Dict[str, Any]]:
        """List archive storage pools."""
        response = self._client.get(_append_query("/v1/archive/storage-pools", query))
        return response.data or []

    def storage_pool_health(self, storage_pool_id: str) -> Dict[str, Any]:
        """Read archive storage pool health."""
        response = self._client.get(f"/v1/archive/storage-pools/{quote(storage_pool_id, safe='')}/health")
        return response.data or {}

    def migrations(self, query: Optional[Dict[str, Any]] = None) -> List[Dict[str, Any]]:
        """List archive migrations."""
        response = self._client.get(_append_query("/v1/archive/migrations", query))
        return response.data or []

    def migration(self, migration_id: str) -> Dict[str, Any]:
        """Read an archive migration."""
        response = self._client.get(f"/v1/archive/migrations/{quote(migration_id, safe='')}")
        return response.data or {}

    def migration_batches(self, migration_id: str, query: Optional[Dict[str, Any]] = None) -> List[Dict[str, Any]]:
        """List archive migration batches."""
        path = f"/v1/archive/migrations/{quote(migration_id, safe='')}/batches"
        response = self._client.get(_append_query(path, query))
        return response.data or []

    def create_migration(self, request: Dict[str, Any]) -> Dict[str, Any]:
        """Create an Archive Server migration."""
        response = self._client.post("/v1/archive/migrations", request)
        return response.data or {}

    def create_migration_batch(self, migration_id: str, request: Dict[str, Any]) -> Dict[str, Any]:
        """Create metadata for one Archive Server migration batch."""
        path = f"/v1/archive/migrations/{quote(migration_id, safe='')}/batches"
        response = self._client.post(path, request)
        return response.data or {}

    def upload_migration_batch_content(
        self,
        migration_id: str,
        batch_id: str,
        content: Any,
        content_hash_sha256: Optional[str] = None,
        content_type: str = "application/gzip"
    ) -> Dict[str, Any]:
        """Upload compressed JSONL content for one Archive Server migration batch."""
        headers = {"x-content-sha256": content_hash_sha256} if content_hash_sha256 else None
        path = f"/v1/archive/migrations/{quote(migration_id, safe='')}/batches/{quote(batch_id, safe='')}/content"
        response = self._client.put_raw(path, content, content_type, headers)
        return response.data or {}

    def seal_migration(self, migration_id: str) -> Dict[str, Any]:
        """Seal an Archive Server migration after all batches are uploaded."""
        response = self._client.post(f"/v1/archive/migrations/{quote(migration_id, safe='')}/seal")
        return response.data or {}

    def commit_migration(self, migration_id: str) -> Dict[str, Any]:
        """Commit an Archive Server migration and create its manifest."""
        response = self._client.post(f"/v1/archive/migrations/{quote(migration_id, safe='')}/commit")
        return response.data or {}

    def abort_migration(self, migration_id: str) -> Dict[str, Any]:
        """Abort an Archive Server migration and delete temporary content."""
        response = self._client.post(f"/v1/archive/migrations/{quote(migration_id, safe='')}/abort")
        return response.data or {}

    def export_entries(self, request: Dict[str, Any]) -> Dict[str, Any]:
        """Start an active NetLedger Server export of committed entries to NetLedger Archive Server."""
        response = self._client.post("/v1/archive/exports/entries", request)
        return response.data or {}

    def export_request_history(self, request: Dict[str, Any]) -> Dict[str, Any]:
        """Start an active NetLedger Server export of request history to NetLedger Archive Server."""
        response = self._client.post("/v1/archive/exports/request-history", request)
        return response.data or {}

    def export_tenant_account_entries(
        self,
        tenant_id: str,
        account_id: str,
        request: Optional[Dict[str, Any]] = None
    ) -> Dict[str, Any]:
        """Start an active NetLedger Server export of committed entries for one tenant account."""
        path = f"/v1/tenants/{quote(tenant_id, safe='')}/accounts/{quote(account_id, safe='')}/archive/export"
        response = self._client.post(path, request or {})
        return response.data or {}

    def entries(self, account_id: str, query: Optional[Dict[str, Any]] = None) -> EnumerationResult:
        """Enumerate archived entries for an account."""
        response = self._client.get(_append_query(f"/v1/archive/accounts/{quote(account_id, safe='')}/entries", query))
        if not response.data:
            return EnumerationResult()
        return EnumerationResult.from_dict(response.data, Entry.from_dict)

    def tenant_entries(
        self,
        tenant_id: str,
        account_id: str,
        query: Optional[Dict[str, Any]] = None
    ) -> EnumerationResult:
        """Enumerate archived entries for a tenant account."""
        path = f"/v1/tenants/{quote(tenant_id, safe='')}/accounts/{quote(account_id, safe='')}/entries"
        response = self._client.get(_append_query(path, query))
        if not response.data:
            return EnumerationResult()
        return EnumerationResult.from_dict(response.data, Entry.from_dict)

    def balance_as_of(self, account_id: str, as_of_utc: str) -> Dict[str, Any]:
        """Read archived balance as of a point in time for an account."""
        path = f"/v1/archive/accounts/{quote(account_id, safe='')}/balance/asof?asOf={quote(as_of_utc, safe='')}"
        response = self._client.get(path)
        return response.data or {}

    def tenant_balance_as_of(self, tenant_id: str, account_id: str, as_of_utc: str) -> Dict[str, Any]:
        """Read archived balance as of a point in time for a tenant account."""
        path = f"/v1/tenants/{quote(tenant_id, safe='')}/accounts/{quote(account_id, safe='')}/balance/asof?asOf={quote(as_of_utc, safe='')}"
        response = self._client.get(path)
        return response.data or {}

    def balance_asof(self, account_id: str, as_of_utc: str) -> Dict[str, Any]:
        """Read archived balance as of a point in time for an account."""
        return self.balance_as_of(account_id, as_of_utc)

    def tenant_balance_asof(self, tenant_id: str, account_id: str, as_of_utc: str) -> Dict[str, Any]:
        """Read archived balance as of a point in time for a tenant account."""
        return self.tenant_balance_as_of(tenant_id, account_id, as_of_utc)

    def verify_account(self, account_id: str, query: Optional[Dict[str, Any]] = None) -> Dict[str, Any]:
        """Verify archived balance chain and object integrity for an account."""
        response = self._client.get(_append_query(f"/v1/archive/accounts/{quote(account_id, safe='')}/verify", query))
        return response.data or {}

    def verify_tenant_account(
        self,
        tenant_id: str,
        account_id: str,
        query: Optional[Dict[str, Any]] = None
    ) -> Dict[str, Any]:
        """Verify archived balance chain and object integrity for a tenant account."""
        path = f"/v1/tenants/{quote(tenant_id, safe='')}/accounts/{quote(account_id, safe='')}/verify"
        response = self._client.get(_append_query(path, query))
        return response.data or {}

    def object_metadata(self, object_id: str) -> Dict[str, Any]:
        """Read archive object catalog and storage metadata."""
        response = self._client.get(f"/v1/archive/objects/{quote(object_id, safe='')}/metadata")
        return response.data or {}

    def request_history(self, query: Optional[Dict[str, Any]] = None) -> Dict[str, Any]:
        """Enumerate archived NetLedger request history from Archive Server."""
        response = self._client.get(_append_query("/v1/request-history", query))
        return response.data or {}

    def request_history_summary(self, query: Optional[Dict[str, Any]] = None) -> Dict[str, Any]:
        """Summarize archived NetLedger request history from Archive Server."""
        response = self._client.get(_append_query("/v1/request-history/summary", query))
        return response.data or {}

    def request_history_entry(self, entry_id: str, query: Optional[Dict[str, Any]] = None) -> Dict[str, Any]:
        """Read one archived NetLedger request history entry from Archive Server."""
        response = self._client.get(_append_query(f"/v1/request-history/{quote(entry_id, safe='')}", query))
        return response.data or {}

    def archive_server_request_history(self, query: Optional[Dict[str, Any]] = None) -> Dict[str, Any]:
        """Enumerate Archive Server operational request history."""
        response = self._client.get(_append_query("/v1/archive-server/request-history", query))
        return response.data or {}

    def archive_server_request_history_summary(self, query: Optional[Dict[str, Any]] = None) -> Dict[str, Any]:
        """Summarize Archive Server operational request history."""
        response = self._client.get(_append_query("/v1/archive-server/request-history/summary", query))
        return response.data or {}

    def archive_server_request_history_entry(self, entry_id: str, query: Optional[Dict[str, Any]] = None) -> Dict[str, Any]:
        """Read one Archive Server operational request history entry."""
        response = self._client.get(_append_query(f"/v1/archive-server/request-history/{quote(entry_id, safe='')}", query))
        return response.data or {}

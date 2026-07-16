"""Identity and security administration operations."""

from typing import Any, Dict, List

from ..http_client import HttpClient
from ..models import EnumerationResult


class IdentityMethods:
    """Identity and security administration operations."""

    def __init__(self, client: HttpClient):
        """Initialize identity methods."""
        self._client = client

    def permissions(self) -> Dict[str, Any]:
        """Get effective permissions for the current principal."""
        response = self._client.get('/v1/me/permissions')
        return response.data or {'Permissions': []}

    def tenants(self, max_results: int = 100) -> EnumerationResult:
        """Enumerate tenants."""
        response = self._client.get(f'/v1/tenants?maxResults={max_results}')
        return EnumerationResult.from_dict(response.data or {})

    def create_tenant(self, name: str) -> Dict[str, Any]:
        """Create a tenant."""
        response = self._client.put('/v1/tenants', {'Name': name})
        return response.data or {}

    def users(self, tenant_id: str, max_results: int = 100) -> EnumerationResult:
        """Enumerate users in a tenant."""
        response = self._client.get(f'/v1/tenants/{tenant_id}/users?maxResults={max_results}')
        return EnumerationResult.from_dict(response.data or {})

    def create_user(self, tenant_id: str, user: Dict[str, Any]) -> Dict[str, Any]:
        """Create a user in a tenant."""
        response = self._client.put(f'/v1/tenants/{tenant_id}/users', user)
        return response.data or {}

    def sessions(self, tenant_id: str, max_results: int = 100) -> EnumerationResult:
        """Enumerate sessions in a tenant."""
        response = self._client.get(f'/v1/tenants/{tenant_id}/sessions?maxResults={max_results}')
        return EnumerationResult.from_dict(response.data or {})

    def audit(self, tenant_id: str, max_results: int = 100) -> EnumerationResult:
        """Enumerate audit records in a tenant."""
        response = self._client.get(f'/v1/tenants/{tenant_id}/audit?maxResults={max_results}')
        return EnumerationResult.from_dict(response.data or {})

    def roles(self, tenant_id: str, max_results: int = 100) -> EnumerationResult:
        """Enumerate roles in a tenant."""
        response = self._client.get(f'/v1/tenants/{tenant_id}/roles?maxResults={max_results}')
        return EnumerationResult.from_dict(response.data or {})

    def create_role(self, tenant_id: str, name: str) -> Dict[str, Any]:
        """Create a custom role."""
        response = self._client.put(f'/v1/tenants/{tenant_id}/roles', {'Name': name})
        return response.data or {}

    def role_permissions(self, tenant_id: str, max_results: int = 100) -> EnumerationResult:
        """Enumerate permissions in a tenant."""
        response = self._client.get(f'/v1/tenants/{tenant_id}/permissions?maxResults={max_results}')
        return EnumerationResult.from_dict(response.data or {})

    def create_permission(self, tenant_id: str, permission: Dict[str, Any]) -> Dict[str, Any]:
        """Create a custom permission."""
        response = self._client.put(f'/v1/tenants/{tenant_id}/permissions', permission)
        return response.data or {}

    def assign_user_role(self, tenant_id: str, user_id: str, assignment: Dict[str, Any]) -> Dict[str, Any]:
        """Assign a role to a user."""
        response = self._client.put(f'/v1/tenants/{tenant_id}/users/{user_id}/roles', assignment)
        return response.data or {}


def discover_tenants(base_url: str, email: str, timeout_seconds: float = 30.0) -> List[Dict[str, Any]]:
    """Discover tenants for an email address."""
    client = HttpClient(base_url, '', timeout_seconds)
    response = client.post('/v1/auth/tenants', {'Email': email})
    client.close()
    return response.data or []


def login(base_url: str, tenant_id: str, email: str, password: str, timeout_seconds: float = 30.0) -> HttpClient:
    """Login and return an authenticated HTTP client."""
    client = HttpClient(base_url, '', timeout_seconds)
    response = client.post('/v1/auth/login', {'TenantId': tenant_id, 'Email': email, 'Password': password})
    token = None
    if response.data:
        session = response.data.get('Session') or response.data.get('session') or {}
        token = session.get('Token') or session.get('token')
    client.close()
    if not token:
        raise ValueError('Login response did not include a session token')
    return HttpClient(base_url, token, timeout_seconds, tenant_id)

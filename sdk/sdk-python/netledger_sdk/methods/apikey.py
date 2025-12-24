"""API key management operations for the NetLedger API."""

from typing import Optional

from ..http_client import HttpClient
from ..models import ApiKeyInfo, ApiKeyEnumerationQuery, EnumerationResult
from ..exceptions import NetLedgerValidationError


class ApiKeyMethods:
    """API key management operations."""

    def __init__(self, client: HttpClient):
        """
        Initialize API key methods.

        Args:
            client: The HTTP client to use.
        """
        self._client = client

    def create(self, name: str, is_admin: bool = False) -> ApiKeyInfo:
        """
        Create a new API key.

        Args:
            name: Display name for the key.
            is_admin: Whether the key has admin privileges.

        Returns:
            The created API key info (includes the key value).

        Raises:
            NetLedgerValidationError: If the name is empty.
            NetLedgerConnectionError: If unable to connect to the server.
            NetLedgerApiError: If the server returns an error (401 if not authorized).
        """
        if not name or not name.strip():
            raise NetLedgerValidationError('API key name cannot be empty', 'name')

        body = {'name': name, 'isAdmin': is_admin}
        response = self._client.put('/v1/apikeys', body)
        if not response.data:
            raise ValueError('No data returned from server')
        return ApiKeyInfo.from_dict(response.data)

    def enumerate(self, query: Optional[ApiKeyEnumerationQuery] = None) -> EnumerationResult:
        """
        Enumerate API keys.

        Args:
            query: Query parameters.

        Returns:
            Enumeration result (key values not included).

        Raises:
            NetLedgerConnectionError: If unable to connect to the server.
            NetLedgerApiError: If the server returns an error (401 if not authorized).
        """
        if query is None:
            query = ApiKeyEnumerationQuery()

        path = f'/v1/apikeys?maxResults={query.max_results}&skip={query.skip}'
        response = self._client.get(path)

        if not response.data:
            return EnumerationResult()

        return EnumerationResult.from_dict(response.data, ApiKeyInfo.from_dict)

    def revoke(self, api_key_guid: str) -> None:
        """
        Revoke (delete) an API key.

        Args:
            api_key_guid: The API key GUID.

        Raises:
            NetLedgerConnectionError: If unable to connect to the server.
            NetLedgerApiError: If the server returns an error (401 if not authorized, 404 if not found).
        """
        self._client.delete(f'/v1/apikeys/{api_key_guid}')

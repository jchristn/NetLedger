"""Service-level operations for the NetLedger API."""

from ..http_client import HttpClient
from ..models import ServiceInfo


class ServiceMethods:
    """Service-level operations."""

    def __init__(self, client: HttpClient):
        """
        Initialize service methods.

        Args:
            client: The HTTP client to use.
        """
        self._client = client

    def health_check(self) -> bool:
        """
        Check if the server is healthy.

        Returns:
            True if the server is healthy.
        """
        try:
            return self._client.head('/')
        except Exception:
            return False

    def get_info(self) -> ServiceInfo:
        """
        Get service information.

        Returns:
            Service information including version and uptime.

        Raises:
            NetLedgerConnectionError: If unable to connect to the server.
            NetLedgerApiError: If the server returns an error.
        """
        response = self._client.get('/')
        if not response.data:
            raise ValueError('No data returned from server')
        return ServiceInfo.from_dict(response.data)

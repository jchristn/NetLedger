"""NetLedger client for interacting with the REST API."""

from typing import Optional

from .http_client import HttpClient
from .methods import (
    ServiceMethods,
    AccountMethods,
    EntryMethods,
    BalanceMethods,
    ApiKeyMethods
)


class NetLedgerClient:
    """
    Client for interacting with the NetLedger Server REST API.

    Provides access to account, entry, balance, and API key management operations.

    Attributes:
        service: Service operations including health checks and service information.
        account: Account management operations.
        entry: Entry operations including credits and debits.
        balance: Balance operations including commits and verification.
        api_key: API key management operations.
        base_url: The base URL of the NetLedger server.

    Example:
        client = NetLedgerClient('http://localhost:8080', 'your-api-key')

        # Check server health
        is_healthy = client.service.health_check()

        # Create an account
        account = client.account.create('My Account')

        # Add a credit
        credit = client.entry.add_credit(account.guid, 100.00, 'Initial deposit')

        # Get balance
        balance = client.balance.get(account.guid)
    """

    def __init__(self, base_url: str, api_key: str, timeout_seconds: float = 30.0):
        """
        Create a new NetLedger client.

        Args:
            base_url: The base URL of the NetLedger server (e.g., "http://localhost:8080").
            api_key: The API key for authentication.
            timeout_seconds: Request timeout in seconds. Default is 30.

        Raises:
            ValueError: If base_url or api_key is empty.
        """
        if not base_url or not base_url.strip():
            raise ValueError('Base URL cannot be empty')
        if not api_key or not api_key.strip():
            raise ValueError('API key cannot be empty')

        self.base_url = base_url.rstrip('/')
        self._http_client = HttpClient(self.base_url, api_key, timeout_seconds)

        self.service = ServiceMethods(self._http_client)
        self.account = AccountMethods(self._http_client)
        self.entry = EntryMethods(self._http_client)
        self.balance = BalanceMethods(self._http_client)
        self.api_key = ApiKeyMethods(self._http_client)

    def close(self) -> None:
        """Close the client and release resources."""
        self._http_client.close()

    def __enter__(self) -> 'NetLedgerClient':
        """Enter context manager."""
        return self

    def __exit__(self, exc_type, exc_val, exc_tb) -> None:
        """Exit context manager."""
        self.close()

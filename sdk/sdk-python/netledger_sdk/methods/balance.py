"""Balance operations for the NetLedger API."""

from datetime import datetime
from typing import List, Optional

from ..http_client import HttpClient
from ..models import Balance
from ..exceptions import NetLedgerApiError


class BalanceMethods:
    """Balance operations."""

    def __init__(self, client: HttpClient):
        """
        Initialize balance methods.

        Args:
            client: The HTTP client to use.
        """
        self._client = client

    def get(self, account_id: str) -> Balance:
        """
        Get the current balance for an account.

        Args:
            account_id: The account identifier.

        Returns:
            The account balance.

        Raises:
            NetLedgerConnectionError: If unable to connect to the server.
            NetLedgerApiError: If the server returns an error (404 if not found).
        """
        response = self._client.get(f'/v1/accounts/{account_id}/balance')
        if not response.data:
            raise ValueError('No data returned from server')
        return Balance.from_dict(response.data)

    def get_as_of(self, account_id: str, as_of_utc: datetime) -> float:
        """
        Get the historical balance as of a specific time.

        Args:
            account_id: The account identifier.
            as_of_utc: The UTC timestamp.

        Returns:
            The balance value as of that time.

        Raises:
            NetLedgerConnectionError: If unable to connect to the server.
            NetLedgerApiError: If the server returns an error (404 if not found).
        """
        # Format as ISO 8601 compatible string that C# can parse
        timestamp = as_of_utc.strftime('%Y-%m-%dT%H:%M:%SZ')
        response = self._client.get(f'/v1/accounts/{account_id}/balance/asof?asOf={timestamp}')
        if not response.data:
            raise ValueError('No data returned from server')
        # Server returns {accountId, asOfUtc, balance}.
        return float(response.data.get('balance', 0))

    def get_all(self) -> List[Balance]:
        """
        Get balances for all accounts.

        Returns:
            All account balances.

        Raises:
            NetLedgerConnectionError: If unable to connect to the server.
            NetLedgerApiError: If the server returns an error.
        """
        response = self._client.get('/v1/balances')
        if not response.data:
            return []
        # Server returns Dictionary<string, Balance>.
        balances = []
        for id, balance_data in response.data.items():
            balances.append(Balance.from_dict(balance_data))
        return balances

    def commit(self, account_id: str, entry_ids: Optional[List[str]] = None) -> Balance:
        """
        Commit pending entries for an account.

        Args:
            account_id: The account identifier.
            entry_ids: Specific entry identifiers to commit (None = all pending).

        Returns:
            The balance after commit.

        Raises:
            NetLedgerConnectionError: If unable to connect to the server.
            NetLedgerApiError: If the server returns an error (404 if not found).
        """
        body = None
        if entry_ids:
            body = {'EntryIds': entry_ids}

        response = self._client.post(f'/v1/accounts/{account_id}/commit', body)
        if not response.data:
            raise ValueError('No data returned from server')
        return Balance.from_dict(response.data)

    def verify(self, account_id: str) -> bool:
        """
        Verify the balance chain integrity.

        Args:
            account_id: The account identifier.

        Returns:
            True if the balance chain is valid.

        Raises:
            NetLedgerConnectionError: If unable to connect to the server.
            NetLedgerApiError: If the server returns an error (404 if not found).
        """
        try:
            response = self._client.get(f'/v1/accounts/{account_id}/verify')
            return response.status_code == 200
        except NetLedgerApiError as e:
            if e.status_code == 409:
                return False
            raise

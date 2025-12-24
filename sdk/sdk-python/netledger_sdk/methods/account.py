"""Account management operations for the NetLedger API."""

from typing import Optional
from urllib.parse import quote

from ..http_client import HttpClient
from ..models import Account, AccountEnumerationQuery, EnumerationResult
from ..exceptions import NetLedgerValidationError


class AccountMethods:
    """Account management operations."""

    def __init__(self, client: HttpClient):
        """
        Initialize account methods.

        Args:
            client: The HTTP client to use.
        """
        self._client = client

    def create(self, name: str, notes: Optional[str] = None) -> Account:
        """
        Create a new account.

        Args:
            name: The account name.
            notes: Optional notes for the account.

        Returns:
            The created account.

        Raises:
            NetLedgerValidationError: If the name is empty.
            NetLedgerConnectionError: If unable to connect to the server.
            NetLedgerApiError: If the server returns an error.
        """
        if not name or not name.strip():
            raise NetLedgerValidationError('Account name cannot be empty', 'name')

        body = {'name': name}
        if notes:
            body['notes'] = notes

        response = self._client.put('/v1/accounts', body)
        if not response.data:
            raise ValueError('No data returned from server')
        return Account.from_dict(response.data)

    def get(self, account_guid: str) -> Account:
        """
        Get an account by GUID.

        Args:
            account_guid: The account GUID.

        Returns:
            The account.

        Raises:
            NetLedgerConnectionError: If unable to connect to the server.
            NetLedgerApiError: If the server returns an error (404 if not found).
        """
        response = self._client.get(f'/v1/accounts/{account_guid}')
        if not response.data:
            raise ValueError('No data returned from server')
        return Account.from_dict(response.data)

    def get_by_name(self, name: str) -> Account:
        """
        Get an account by name.

        Args:
            name: The account name.

        Returns:
            The account.

        Raises:
            NetLedgerValidationError: If the name is empty.
            NetLedgerConnectionError: If unable to connect to the server.
            NetLedgerApiError: If the server returns an error (404 if not found).
        """
        if not name or not name.strip():
            raise NetLedgerValidationError('Account name cannot be empty', 'name')

        encoded_name = quote(name, safe='')
        response = self._client.get(f'/v1/accounts/byname/{encoded_name}')
        if not response.data:
            raise ValueError('No data returned from server')
        return Account.from_dict(response.data)

    def exists(self, account_guid: str) -> bool:
        """
        Check if an account exists.

        Args:
            account_guid: The account GUID.

        Returns:
            True if the account exists.

        Raises:
            NetLedgerConnectionError: If unable to connect to the server.
        """
        return self._client.head(f'/v1/accounts/{account_guid}')

    def delete(self, account_guid: str) -> None:
        """
        Delete an account.

        Args:
            account_guid: The account GUID.

        Raises:
            NetLedgerConnectionError: If unable to connect to the server.
            NetLedgerApiError: If the server returns an error (404 if not found).
        """
        self._client.delete(f'/v1/accounts/{account_guid}')

    def enumerate(self, query: Optional[AccountEnumerationQuery] = None) -> EnumerationResult:
        """
        Enumerate accounts with optional filtering and pagination.

        Args:
            query: Query parameters for filtering and pagination.

        Returns:
            Enumeration result containing accounts.

        Raises:
            NetLedgerConnectionError: If unable to connect to the server.
            NetLedgerApiError: If the server returns an error.
        """
        if query is None:
            query = AccountEnumerationQuery()

        params = [f'maxResults={query.max_results}', f'skip={query.skip}']
        if query.search_term:
            params.append(f'searchTerm={quote(query.search_term, safe="")}')

        path = f'/v1/accounts?{"&".join(params)}'
        response = self._client.get(path)

        if not response.data:
            return EnumerationResult()

        return EnumerationResult.from_dict(response.data, Account.from_dict)

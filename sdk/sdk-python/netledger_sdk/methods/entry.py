"""Entry operations for the NetLedger API."""

from typing import List, Optional

from ..http_client import HttpClient
from ..models import Entry, EntryInput, EntryEnumerationQuery, EnumerationResult
from ..exceptions import NetLedgerValidationError


class EntryMethods:
    """Entry operations."""

    def __init__(self, client: HttpClient):
        """
        Initialize entry methods.

        Args:
            client: The HTTP client to use.
        """
        self._client = client

    def add_credit(self, account_guid: str, amount: float, description: Optional[str] = None) -> str:
        """
        Add a credit entry.

        Args:
            account_guid: The account GUID.
            amount: The credit amount (must be positive).
            description: Optional description.

        Returns:
            The GUID of the created entry.

        Raises:
            NetLedgerValidationError: If amount is not positive.
            NetLedgerConnectionError: If unable to connect to the server.
            NetLedgerApiError: If the server returns an error.
        """
        if amount <= 0:
            raise NetLedgerValidationError('Amount must be greater than zero', 'amount')

        body = {'Amount': amount}
        if description:
            body['Notes'] = description

        response = self._client.put(f'/v1/accounts/{account_guid}/credits', body)
        if not response.data:
            raise ValueError('No data returned from server')
        # Server returns {EntryGuids: [guid]}, return the first GUID
        entry_guids = response.data.get('EntryGuids', [])
        if not entry_guids:
            raise ValueError('No entry GUID returned from server')
        return entry_guids[0]

    def add_credits(self, account_guid: str, entries: List[EntryInput]) -> List[str]:
        """
        Add multiple credit entries.

        Args:
            account_guid: The account GUID.
            entries: The credit entries to add.

        Returns:
            The GUIDs of the created entries.

        Raises:
            NetLedgerValidationError: If entries is empty or any amount is not positive.
            NetLedgerConnectionError: If unable to connect to the server.
            NetLedgerApiError: If the server returns an error.
        """
        if not entries:
            raise NetLedgerValidationError('Entries list cannot be empty', 'entries')

        for entry in entries:
            if entry.amount <= 0:
                raise NetLedgerValidationError('All amounts must be greater than zero', 'entries')

        # Server expects {Entries: [{Amount: X, Notes: Y}, ...]}
        body = {'Entries': [{'Amount': e.amount, 'Notes': e.description} for e in entries]}
        response = self._client.put(f'/v1/accounts/{account_guid}/credits', body)
        if not response.data:
            return []
        return response.data.get('EntryGuids', [])

    def add_debit(self, account_guid: str, amount: float, description: Optional[str] = None) -> str:
        """
        Add a debit entry.

        Args:
            account_guid: The account GUID.
            amount: The debit amount (must be positive).
            description: Optional description.

        Returns:
            The GUID of the created entry.

        Raises:
            NetLedgerValidationError: If amount is not positive.
            NetLedgerConnectionError: If unable to connect to the server.
            NetLedgerApiError: If the server returns an error.
        """
        if amount <= 0:
            raise NetLedgerValidationError('Amount must be greater than zero', 'amount')

        body = {'Amount': amount}
        if description:
            body['Notes'] = description

        response = self._client.put(f'/v1/accounts/{account_guid}/debits', body)
        if not response.data:
            raise ValueError('No data returned from server')
        # Server returns {EntryGuids: [guid]}, return the first GUID
        entry_guids = response.data.get('EntryGuids', [])
        if not entry_guids:
            raise ValueError('No entry GUID returned from server')
        return entry_guids[0]

    def add_debits(self, account_guid: str, entries: List[EntryInput]) -> List[str]:
        """
        Add multiple debit entries.

        Args:
            account_guid: The account GUID.
            entries: The debit entries to add.

        Returns:
            The GUIDs of the created entries.

        Raises:
            NetLedgerValidationError: If entries is empty or any amount is not positive.
            NetLedgerConnectionError: If unable to connect to the server.
            NetLedgerApiError: If the server returns an error.
        """
        if not entries:
            raise NetLedgerValidationError('Entries list cannot be empty', 'entries')

        for entry in entries:
            if entry.amount <= 0:
                raise NetLedgerValidationError('All amounts must be greater than zero', 'entries')

        # Server expects {Entries: [{Amount: X, Notes: Y}, ...]}
        body = {'Entries': [{'Amount': e.amount, 'Notes': e.description} for e in entries]}
        response = self._client.put(f'/v1/accounts/{account_guid}/debits', body)
        if not response.data:
            return []
        return response.data.get('EntryGuids', [])

    def get_all(self, account_guid: str) -> List[Entry]:
        """
        Get all entries for an account.

        Args:
            account_guid: The account GUID.

        Returns:
            All entries.

        Raises:
            NetLedgerConnectionError: If unable to connect to the server.
            NetLedgerApiError: If the server returns an error.
        """
        response = self._client.get(f'/v1/accounts/{account_guid}/entries')
        if not response.data:
            return []
        # Server returns EnumerationResult<Entry>
        objects = response.data.get('Objects', [])
        return [Entry.from_dict(e) for e in objects]

    def enumerate(self, account_guid: str, query: Optional[EntryEnumerationQuery] = None) -> EnumerationResult:
        """
        Enumerate entries with filtering and pagination.

        Args:
            account_guid: The account GUID.
            query: Query parameters.

        Returns:
            Enumeration result.

        Raises:
            NetLedgerConnectionError: If unable to connect to the server.
            NetLedgerApiError: If the server returns an error.
        """
        if query is None:
            query = EntryEnumerationQuery()

        response = self._client.post(f'/v1/accounts/{account_guid}/entries/enumerate', query.to_dict())
        if not response.data:
            return EnumerationResult()
        return EnumerationResult.from_dict(response.data, Entry.from_dict)

    def get_pending(self, account_guid: str) -> List[Entry]:
        """
        Get all pending (uncommitted) entries.

        Args:
            account_guid: The account GUID.

        Returns:
            Pending entries.

        Raises:
            NetLedgerConnectionError: If unable to connect to the server.
            NetLedgerApiError: If the server returns an error.
        """
        response = self._client.get(f'/v1/accounts/{account_guid}/entries/pending')
        if not response.data:
            return []
        return [Entry.from_dict(e) for e in response.data]

    def get_pending_credits(self, account_guid: str) -> List[Entry]:
        """
        Get pending credit entries.

        Args:
            account_guid: The account GUID.

        Returns:
            Pending credits.

        Raises:
            NetLedgerConnectionError: If unable to connect to the server.
            NetLedgerApiError: If the server returns an error.
        """
        response = self._client.get(f'/v1/accounts/{account_guid}/entries/pending/credits')
        if not response.data:
            return []
        return [Entry.from_dict(e) for e in response.data]

    def get_pending_debits(self, account_guid: str) -> List[Entry]:
        """
        Get pending debit entries.

        Args:
            account_guid: The account GUID.

        Returns:
            Pending debits.

        Raises:
            NetLedgerConnectionError: If unable to connect to the server.
            NetLedgerApiError: If the server returns an error.
        """
        response = self._client.get(f'/v1/accounts/{account_guid}/entries/pending/debits')
        if not response.data:
            return []
        return [Entry.from_dict(e) for e in response.data]

    def cancel(self, account_guid: str, entry_guid: str) -> None:
        """
        Cancel (delete) a pending entry.

        Args:
            account_guid: The account GUID.
            entry_guid: The entry GUID.

        Raises:
            NetLedgerConnectionError: If unable to connect to the server.
            NetLedgerApiError: If the server returns an error (404 if not found, 409 if committed).
        """
        self._client.delete(f'/v1/accounts/{account_guid}/entries/{entry_guid}')

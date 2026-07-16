"""
NetLedger SDK for Python.

A Python SDK for interacting with the NetLedger Server REST API.

Example:
    from netledger_sdk import NetLedgerClient

    client = NetLedgerClient('http://localhost:8080', 'your-api-key')

    # Check server health
    is_healthy = client.service.health_check()

    # Create an account
    account = client.account.create('My Account', 'Optional notes')

    # Add a credit
    credit = client.entry.add_credit(account.id, 100.00, 'Initial deposit')

    # Get balance
    balance = client.balance.get(account.id)
"""

from .client import NetLedgerClient
from .models import (
    Account,
    Entry,
    EntryInput,
    EntryType,
    Balance,
    PendingTransactionSummary,
    CommitResult,
    ApiKeyInfo,
    CredentialCreateResponse,
    ServiceInfo,
    EnumerationResult,
    EnumerationOrder,
    AccountEnumerationQuery,
    EntryEnumerationQuery,
    ApiKeyEnumerationQuery,
    RequestHistoryEntry,
    RequestHistoryQuery,
    RequestHistorySummary,
    RequestHistorySummaryBucket,
    RequestHistoryDeleteResult
)
from .exceptions import (
    NetLedgerError,
    NetLedgerConnectionError,
    NetLedgerApiError,
    NetLedgerValidationError
)

__version__ = '3.0.0'
__all__ = [
    'NetLedgerClient',
    'Account',
    'Entry',
    'EntryInput',
    'EntryType',
    'Balance',
    'PendingTransactionSummary',
    'CommitResult',
    'ApiKeyInfo',
    'CredentialCreateResponse',
    'ServiceInfo',
    'EnumerationResult',
    'EnumerationOrder',
    'AccountEnumerationQuery',
    'EntryEnumerationQuery',
    'ApiKeyEnumerationQuery',
    'RequestHistoryEntry',
    'RequestHistoryQuery',
    'RequestHistorySummary',
    'RequestHistorySummaryBucket',
    'RequestHistoryDeleteResult',
    'NetLedgerError',
    'NetLedgerConnectionError',
    'NetLedgerApiError',
    'NetLedgerValidationError'
]

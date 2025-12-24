"""Data models for the NetLedger SDK."""

from dataclasses import dataclass, field
from enum import IntEnum
from typing import Optional, List, Any, Dict
from datetime import datetime


class EntryType(IntEnum):
    """Entry type enumeration."""
    CREDIT = 0
    DEBIT = 1
    BALANCE = 2


class EnumerationOrder(IntEnum):
    """Enumeration order options."""
    CREATED_ASCENDING = 0
    CREATED_DESCENDING = 1
    AMOUNT_ASCENDING = 2
    AMOUNT_DESCENDING = 3


@dataclass
class Account:
    """Represents a ledger account."""
    guid: str
    name: str
    notes: Optional[str] = None
    created_utc: Optional[str] = None

    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> "Account":
        """Create an Account from a dictionary."""
        return cls(
            guid=data.get("GUID", ""),
            name=data.get("Name", ""),
            notes=data.get("Notes"),
            created_utc=data.get("CreatedUtc")
        )


@dataclass
class Entry:
    """Represents a ledger entry."""
    guid: str
    account_guid: str
    type: EntryType
    amount: float
    description: Optional[str] = None
    replaces: Optional[str] = None
    is_committed: bool = False
    committed_by_guid: Optional[str] = None
    committed_utc: Optional[str] = None
    created_utc: Optional[str] = None

    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> "Entry":
        """Create an Entry from a dictionary."""
        # Handle EntryType - can be string or int from server
        entry_type_raw = data.get("Type", 0)
        if isinstance(entry_type_raw, str):
            type_map = {"Credit": 0, "Debit": 1, "Balance": 2}
            entry_type = EntryType(type_map.get(entry_type_raw, 0))
        else:
            entry_type = EntryType(entry_type_raw)
        return cls(
            guid=data.get("GUID", ""),
            account_guid=data.get("AccountGUID", ""),
            type=entry_type,
            amount=float(data.get("Amount", 0)),
            description=data.get("Description"),
            replaces=data.get("Replaces"),
            is_committed=data.get("IsCommitted", False),
            committed_by_guid=data.get("CommittedByGUID"),
            committed_utc=data.get("CommittedUtc"),
            created_utc=data.get("CreatedUtc")
        )


@dataclass
class EntryInput:
    """Input for creating an entry."""
    amount: float
    description: Optional[str] = None

    def to_dict(self) -> Dict[str, Any]:
        """Convert to dictionary for API request."""
        result: Dict[str, Any] = {"amount": self.amount}
        if self.description:
            result["description"] = self.description
        return result


@dataclass
class PendingTransactionSummary:
    """Summary of pending transactions."""
    count: int = 0
    total: float = 0.0
    entries: Optional[List[Entry]] = None

    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> "PendingTransactionSummary":
        """Create from a dictionary."""
        entries = None
        if data.get("Entries"):
            entries = [Entry.from_dict(e) for e in data["Entries"]]
        return cls(
            count=data.get("Count", 0),
            total=float(data.get("Total", 0)),
            entries=entries
        )


@dataclass
class Balance:
    """Represents an account balance."""
    account_guid: str
    committed_balance: float
    pending_balance: float
    pending_credits: Optional[PendingTransactionSummary] = None
    pending_debits: Optional[PendingTransactionSummary] = None

    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> "Balance":
        """Create a Balance from a dictionary."""
        pending_credits = None
        pending_debits = None
        if data.get("PendingCredits"):
            pending_credits = PendingTransactionSummary.from_dict(data["PendingCredits"])
        if data.get("PendingDebits"):
            pending_debits = PendingTransactionSummary.from_dict(data["PendingDebits"])
        return cls(
            account_guid=data.get("AccountGUID", ""),
            committed_balance=float(data.get("CommittedBalance", 0)),
            pending_balance=float(data.get("PendingBalance", 0)),
            pending_credits=pending_credits,
            pending_debits=pending_debits
        )


@dataclass
class CommitResult:
    """Result of a commit operation."""
    entries_committed: int = 0
    balance_entry: Optional[Entry] = None
    balance: Optional[Balance] = None

    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> "CommitResult":
        """Create from a dictionary."""
        balance_entry = None
        balance = None
        if data.get("BalanceEntry"):
            balance_entry = Entry.from_dict(data["BalanceEntry"])
        if data.get("Balance"):
            balance = Balance.from_dict(data["Balance"])
        return cls(
            entries_committed=data.get("EntriesCommitted", 0),
            balance_entry=balance_entry,
            balance=balance
        )


@dataclass
class ApiKeyInfo:
    """Information about an API key."""
    guid: str
    name: str
    api_key: Optional[str] = None
    active: bool = True
    is_admin: bool = False
    created_utc: Optional[str] = None

    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> "ApiKeyInfo":
        """Create from a dictionary."""
        return cls(
            guid=data.get("GUID", ""),
            name=data.get("Name", ""),
            api_key=data.get("Key"),
            active=data.get("Active", True),
            is_admin=data.get("IsAdmin", False),
            created_utc=data.get("CreatedUtc")
        )


@dataclass
class ServiceInfo:
    """Service information."""
    name: str = ""
    version: str = ""
    start_time_utc: Optional[str] = None
    uptime_seconds: int = 0
    uptime_formatted: str = ""

    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> "ServiceInfo":
        """Create from a dictionary."""
        return cls(
            name=data.get("Name") or data.get("name", ""),
            version=data.get("Version") or data.get("version", ""),
            start_time_utc=data.get("StartTimeUtc") or data.get("startTimeUtc"),
            uptime_seconds=data.get("UptimeSeconds") or data.get("uptimeSeconds", 0),
            uptime_formatted=data.get("UptimeFormatted") or data.get("uptimeFormatted", "")
        )


@dataclass
class EnumerationResult:
    """Enumeration result with pagination."""
    total_records: int = 0
    records_remaining: int = 0
    end_of_results: bool = True
    continuation_token: Optional[str] = None
    objects: Optional[List[Any]] = None

    @classmethod
    def from_dict(cls, data: Dict[str, Any], object_factory=None) -> "EnumerationResult":
        """Create from a dictionary."""
        objects = None
        if data.get("Objects") and object_factory:
            objects = [object_factory(o) for o in data["Objects"]]
        elif data.get("Objects"):
            objects = data["Objects"]
        return cls(
            total_records=data.get("TotalRecords", 0),
            records_remaining=data.get("RecordsRemaining", 0),
            end_of_results=data.get("EndOfResults", True),
            continuation_token=data.get("ContinuationToken"),
            objects=objects
        )


@dataclass
class AccountEnumerationQuery:
    """Query for enumerating accounts."""
    max_results: int = 100
    skip: int = 0
    search_term: Optional[str] = None


@dataclass
class EntryEnumerationQuery:
    """Query for enumerating entries."""
    max_results: int = 100
    continuation_token: Optional[str] = None
    created_after_utc: Optional[str] = None
    created_before_utc: Optional[str] = None
    amount_min: Optional[float] = None
    amount_max: Optional[float] = None
    ordering: EnumerationOrder = EnumerationOrder.CREATED_DESCENDING

    def to_dict(self) -> Dict[str, Any]:
        """Convert to dictionary for API request."""
        result: Dict[str, Any] = {
            "MaxResults": self.max_results,
            "Ordering": self.ordering.value
        }
        if self.continuation_token:
            result["ContinuationToken"] = self.continuation_token
        if self.created_after_utc:
            result["CreatedAfterUtc"] = self.created_after_utc
        if self.created_before_utc:
            result["CreatedBeforeUtc"] = self.created_before_utc
        if self.amount_min is not None:
            result["AmountMinimum"] = self.amount_min
        if self.amount_max is not None:
            result["AmountMaximum"] = self.amount_max
        return result


@dataclass
class ApiKeyEnumerationQuery:
    """Query for enumerating API keys."""
    max_results: int = 100
    skip: int = 0

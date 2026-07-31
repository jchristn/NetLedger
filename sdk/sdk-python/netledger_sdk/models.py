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
    id: str
    name: str
    tenant_id: str = ""
    notes: Optional[str] = None
    labels: List[str] = field(default_factory=list)
    tags: Dict[str, str] = field(default_factory=dict)
    created_utc: Optional[str] = None
    last_update_utc: Optional[str] = None
    active: bool = True

    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> "Account":
        """Create an Account from a dictionary."""
        return cls(
            id=data.get("Id", ""),
            name=data.get("Name", ""),
            tenant_id=data.get("TenantId", ""),
            notes=data.get("Notes"),
            labels=data.get("Labels") or [],
            tags=data.get("Tags") or {},
            created_utc=data.get("CreatedUtc"),
            last_update_utc=data.get("LastUpdateUtc"),
            active=data.get("Active", True)
        )


@dataclass
class Entry:
    """Represents a ledger entry."""
    id: str
    account_id: str
    type: EntryType
    amount: float
    tenant_id: str = ""
    description: Optional[str] = None
    replaces: Optional[str] = None
    is_committed: bool = False
    committed_by_id: Optional[str] = None
    labels: List[str] = field(default_factory=list)
    tags: Dict[str, str] = field(default_factory=dict)
    committed_utc: Optional[str] = None
    created_utc: Optional[str] = None
    last_update_utc: Optional[str] = None

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
            id=data.get("Id", ""),
            account_id=data.get("AccountId", ""),
            type=entry_type,
            amount=float(data.get("Amount", 0)),
            tenant_id=data.get("TenantId", ""),
            description=data.get("Description"),
            replaces=data.get("Replaces"),
            is_committed=data.get("IsCommitted", False),
            committed_by_id=data.get("CommittedById") or data.get("CommittedById"),
            labels=data.get("Labels") or [],
            tags=data.get("Tags") or {},
            committed_utc=data.get("CommittedUtc"),
            created_utc=data.get("CreatedUtc"),
            last_update_utc=data.get("LastUpdateUtc")
        )


@dataclass
class EntryInput:
    """Input for creating an entry."""
    amount: float
    description: Optional[str] = None
    labels: List[str] = field(default_factory=list)
    tags: Dict[str, str] = field(default_factory=dict)

    def to_dict(self) -> Dict[str, Any]:
        """Convert to dictionary for API request."""
        result: Dict[str, Any] = {"amount": self.amount}
        if self.description:
            result["description"] = self.description
        if self.labels:
            result["Labels"] = self.labels
        if self.tags:
            result["Tags"] = self.tags
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
    account_id: str
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
            account_id=data.get("AccountId", ""),
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
    id: str
    name: str
    tenant_id: str = ""
    user_id: str = ""
    api_key: Optional[str] = None
    secret_key_last4: str = ""
    active: bool = True
    is_admin: bool = False
    created_utc: Optional[str] = None

    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> "ApiKeyInfo":
        """Create from a dictionary."""
        return cls(
            id=data.get("Id", ""),
            name=data.get("Name", ""),
            tenant_id=data.get("TenantId", ""),
            user_id=data.get("UserId", ""),
            api_key=data.get("Key"),
            secret_key_last4=data.get("SecretKeyLast4", ""),
            active=data.get("Active", True),
            is_admin=data.get("IsAdmin", False),
            created_utc=data.get("CreatedUtc")
        )


@dataclass
class CredentialCreateResponse:
    """Credential creation response with one-time secret key."""
    credential: Optional[ApiKeyInfo] = None
    secret_key: Optional[str] = None

    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> "CredentialCreateResponse":
        """Create from a dictionary."""
        credential = None
        if data.get("Credential"):
            credential = ApiKeyInfo.from_dict(data["Credential"])
        return cls(
            credential=credential,
            secret_key=data.get("SecretKey")
        )


@dataclass
class ServiceInfo:
    """Service information."""
    name: str = ""
    version: str = ""
    start_time_utc: Optional[str] = None
    uptime_seconds: int = 0
    uptime_formatted: str = ""
    archive: Optional[Dict[str, Any]] = None

    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> "ServiceInfo":
        """Create from a dictionary."""
        return cls(
            name=data.get("Name") or data.get("name", ""),
            version=data.get("Version") or data.get("version", ""),
            start_time_utc=data.get("StartTimeUtc") or data.get("startTimeUtc"),
            uptime_seconds=data.get("UptimeSeconds") or data.get("uptimeSeconds", 0),
            uptime_formatted=data.get("UptimeFormatted") or data.get("uptimeFormatted", ""),
            archive=data.get("Archive") or data.get("archive")
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
    labels: List[str] = field(default_factory=list)
    tags: Dict[str, str] = field(default_factory=dict)


@dataclass
class EntryEnumerationQuery:
    """Query for enumerating entries."""
    max_results: int = 100
    skip: int = 0
    continuation_token: Optional[str] = None
    search_term: Optional[str] = None
    created_after_utc: Optional[str] = None
    created_before_utc: Optional[str] = None
    amount_min: Optional[float] = None
    amount_max: Optional[float] = None
    credit_minimum: Optional[float] = None
    credit_maximum: Optional[float] = None
    debit_minimum: Optional[float] = None
    debit_maximum: Optional[float] = None
    labels: List[str] = field(default_factory=list)
    tags: Dict[str, str] = field(default_factory=dict)
    ordering: EnumerationOrder = EnumerationOrder.CREATED_DESCENDING

    def to_dict(self) -> Dict[str, Any]:
        """Convert to dictionary for API request."""
        result: Dict[str, Any] = {
            "MaxResults": self.max_results,
            "Skip": self.skip,
            "Ordering": self.ordering.value
        }
        if self.continuation_token:
            result["ContinuationToken"] = self.continuation_token
        if self.search_term:
            result["SearchTerm"] = self.search_term
        if self.created_after_utc:
            result["CreatedAfterUtc"] = self.created_after_utc
        if self.created_before_utc:
            result["CreatedBeforeUtc"] = self.created_before_utc
        if self.amount_min is not None:
            result["AmountMinimum"] = self.amount_min
        if self.amount_max is not None:
            result["AmountMaximum"] = self.amount_max
        if self.credit_minimum is not None:
            result["CreditMinimum"] = self.credit_minimum
        if self.credit_maximum is not None:
            result["CreditMaximum"] = self.credit_maximum
        if self.debit_minimum is not None:
            result["DebitMinimum"] = self.debit_minimum
        if self.debit_maximum is not None:
            result["DebitMaximum"] = self.debit_maximum
        if self.labels:
            result["Labels"] = self.labels
        if self.tags:
            result["Tags"] = self.tags
        return result


@dataclass
class ApiKeyEnumerationQuery:
    """Query for enumerating API keys."""
    max_results: int = 100
    skip: int = 0
    tenant_id: Optional[str] = None


@dataclass
class RequestHistoryEntry:
    """Captured REST request and response metadata."""
    id: str
    method: str
    path: str
    url: str
    status_code: int
    duration_ms: float
    tenant_id: Optional[str] = None
    principal_id: Optional[str] = None
    principal_type: Optional[str] = None
    source_ip: Optional[str] = None
    request_headers: Dict[str, str] = field(default_factory=dict)
    request_body: Optional[str] = None
    request_body_bytes: int = 0
    request_body_truncated: bool = False
    response_headers: Dict[str, str] = field(default_factory=dict)
    response_body: Optional[str] = None
    response_body_bytes: int = 0
    response_body_truncated: bool = False
    created_utc: Optional[str] = None
    completed_utc: Optional[str] = None

    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> "RequestHistoryEntry":
        """Create from a dictionary."""
        return cls(
            id=data.get("Id", ""),
            tenant_id=data.get("TenantId"),
            principal_id=data.get("PrincipalId"),
            principal_type=data.get("PrincipalType"),
            method=data.get("Method", ""),
            path=data.get("Path", ""),
            url=data.get("Url", ""),
            status_code=int(data.get("StatusCode", 0)),
            duration_ms=float(data.get("DurationMs", 0)),
            source_ip=data.get("SourceIp"),
            request_headers=data.get("RequestHeaders") or {},
            request_body=data.get("RequestBody"),
            request_body_bytes=int(data.get("RequestBodyBytes", 0)),
            request_body_truncated=bool(data.get("RequestBodyTruncated", False)),
            response_headers=data.get("ResponseHeaders") or {},
            response_body=data.get("ResponseBody"),
            response_body_bytes=int(data.get("ResponseBodyBytes", 0)),
            response_body_truncated=bool(data.get("ResponseBodyTruncated", False)),
            created_utc=data.get("CreatedUtc"),
            completed_utc=data.get("CompletedUtc")
        )


@dataclass
class RequestHistoryQuery:
    """Query for request history enumeration and summaries."""
    max_results: int = 25
    skip: int = 0
    continuation_token: Optional[str] = None
    tenant_id: Optional[str] = None
    principal_id: Optional[str] = None
    method: Optional[str] = None
    status_code: Optional[int] = None
    path_contains: Optional[str] = None
    from_utc: Optional[str] = None
    to_utc: Optional[str] = None
    bucket_minutes: int = 15
    allow_partial: Optional[bool] = None


@dataclass
class RequestHistorySummaryBucket:
    """Request history summary bucket."""
    bucket_start_utc: Optional[str] = None
    bucket_end_utc: Optional[str] = None
    success_count: int = 0
    failure_count: int = 0
    average_duration_ms: float = 0.0

    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> "RequestHistorySummaryBucket":
        """Create from a dictionary."""
        return cls(
            bucket_start_utc=data.get("BucketStartUtc"),
            bucket_end_utc=data.get("BucketEndUtc"),
            success_count=int(data.get("SuccessCount", 0)),
            failure_count=int(data.get("FailureCount", 0)),
            average_duration_ms=float(data.get("AverageDurationMs", 0))
        )


@dataclass
class RequestHistorySummary:
    """Request history summary."""
    total_count: int = 0
    total_success: int = 0
    total_failure: int = 0
    average_duration_ms: float = 0.0
    buckets: List[RequestHistorySummaryBucket] = field(default_factory=list)

    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> "RequestHistorySummary":
        """Create from a dictionary."""
        return cls(
            total_count=int(data.get("TotalCount", 0)),
            total_success=int(data.get("TotalSuccess", 0)),
            total_failure=int(data.get("TotalFailure", 0)),
            average_duration_ms=float(data.get("AverageDurationMs", 0)),
            buckets=[RequestHistorySummaryBucket.from_dict(item) for item in data.get("Buckets", [])]
        )


@dataclass
class RequestHistoryDeleteResult:
    """Request history delete result."""
    deleted_count: int = 0

    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> "RequestHistoryDeleteResult":
        """Create from a dictionary."""
        return cls(deleted_count=int(data.get("DeletedCount", 0)))

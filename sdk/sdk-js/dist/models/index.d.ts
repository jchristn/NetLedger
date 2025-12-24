/**
 * Entry type enumeration.
 */
export declare enum EntryType {
    /** Credit entry (increases balance). */
    Credit = 0,
    /** Debit entry (decreases balance). */
    Debit = 1,
    /** Balance snapshot entry. */
    Balance = 2
}
/**
 * Enumeration order options.
 */
export declare enum EnumerationOrder {
    /** Order by creation date, ascending (oldest first). */
    CreatedAscending = 0,
    /** Order by creation date, descending (newest first). */
    CreatedDescending = 1,
    /** Order by amount, ascending (smallest first). */
    AmountAscending = 2,
    /** Order by amount, descending (largest first). */
    AmountDescending = 3
}
/**
 * Represents a ledger account.
 */
export interface Account {
    /** Unique identifier for the account. */
    GUID: string;
    /** Name of the account. */
    Name: string;
    /** Optional notes. */
    Notes?: string;
    /** UTC timestamp when created. */
    CreatedUtc: string;
}
/**
 * Represents a ledger entry.
 */
export interface Entry {
    /** Unique identifier for the entry. */
    GUID: string;
    /** Account this entry belongs to. */
    AccountGUID: string;
    /** Type of entry (Credit, Debit, Balance). */
    Type: EntryType;
    /** Monetary amount. */
    Amount: number;
    /** Optional description. */
    Description?: string;
    /** For balance entries, the GUID of the replaced balance. */
    Replaces?: string;
    /** Whether the entry is committed. */
    IsCommitted: boolean;
    /** GUID of the balance entry that committed this. */
    CommittedByGUID?: string;
    /** UTC timestamp when committed. */
    CommittedUtc?: string;
    /** UTC timestamp when created. */
    CreatedUtc: string;
}
/**
 * Input for creating an entry.
 */
export interface EntryInput {
    /** Monetary amount (must be positive). */
    Amount: number;
    /** Optional notes. */
    Notes?: string;
}
/**
 * Response from adding entries.
 */
export interface AddEntriesResponse {
    /** GUIDs of created entries. */
    EntryGuids: string[];
}
/**
 * Historical balance at a point in time.
 * Note: Properties are lowercase as returned by the server.
 */
export interface HistoricalBalance {
    /** Account GUID. */
    accountGuid: string;
    /** The timestamp this balance is as of. */
    asOfUtc: string;
    /** The balance value. */
    balance: number;
}
/**
 * Summary of pending transactions.
 */
export interface PendingTransactionSummary {
    /** Number of pending transactions. */
    Count: number;
    /** Total amount. */
    Total: number;
    /** List of pending entries. */
    Entries?: Entry[];
}
/**
 * Represents an account balance.
 */
export interface Balance {
    /** Account GUID. */
    AccountGUID: string;
    /** Committed (finalized) balance. */
    CommittedBalance: number;
    /** Pending balance (includes uncommitted entries). */
    PendingBalance: number;
    /** Summary of pending credits. */
    PendingCredits?: PendingTransactionSummary;
    /** Summary of pending debits. */
    PendingDebits?: PendingTransactionSummary;
}
/**
 * Result of a commit operation.
 */
export interface CommitResult {
    /** Number of entries committed. */
    EntriesCommitted: number;
    /** The balance entry created. */
    BalanceEntry?: Entry;
    /** The new balance. */
    Balance?: Balance;
}
/**
 * Request to commit entries.
 */
export interface CommitRequest {
    /** Specific entry GUIDs to commit (null = all). */
    EntryGuids?: string[];
}
/**
 * Information about an API key.
 */
export interface ApiKeyInfo {
    /** Unique identifier. */
    GUID: string;
    /** Display name. */
    Name: string;
    /** The API key value (only on creation). */
    Key?: string;
    /** Whether the key is active. */
    Active: boolean;
    /** Whether the key has admin privileges. */
    IsAdmin: boolean;
    /** UTC timestamp when created. */
    CreatedUtc: string;
}
/**
 * Service information.
 */
export interface ServiceInfo {
    /** Service name. */
    Name: string;
    /** Service version. */
    Version: string;
    /** Uptime in seconds. */
    UptimeSeconds: number;
    /** Formatted uptime string. */
    UptimeFormatted: string;
    /** UTC timestamp when server started. */
    StartTimeUtc: string;
}
/**
 * Enumeration result with pagination.
 */
export interface EnumerationResult<T> {
    /** Total number of records. */
    TotalRecords: number;
    /** Records remaining after this page. */
    RecordsRemaining: number;
    /** Whether this is the last page. */
    EndOfResults: boolean;
    /** Continuation token for next page. */
    ContinuationToken?: string;
    /** Objects in this page. */
    Objects?: T[];
}
/**
 * Query for enumerating accounts.
 */
export interface AccountEnumerationQuery {
    /** Maximum results (1-1000). */
    MaxResults?: number;
    /** Number to skip. */
    Skip?: number;
    /** Search term for name. */
    SearchTerm?: string;
}
/**
 * Query for enumerating entries.
 */
export interface EntryEnumerationQuery {
    /** Maximum results (1-1000). */
    MaxResults?: number;
    /** Continuation token. */
    ContinuationToken?: string;
    /** Filter: created after. */
    CreatedAfterUtc?: string;
    /** Filter: created before. */
    CreatedBeforeUtc?: string;
    /** Filter: minimum amount. */
    AmountMinimum?: number;
    /** Filter: maximum amount. */
    AmountMaximum?: number;
    /** Result ordering. */
    Ordering?: EnumerationOrder;
}
/**
 * Query for enumerating API keys.
 */
export interface ApiKeyEnumerationQuery {
    /** Maximum results (1-1000). */
    MaxResults?: number;
    /** Number to skip. */
    Skip?: number;
}
/**
 * API response wrapper.
 */
export interface ApiResponse<T> {
    /** Response data. */
    Data?: T;
    /** HTTP status code. */
    StatusCode: number;
    /** Request GUID. */
    RequestGuid?: string;
}
/**
 * Error response from the API.
 */
export interface ErrorResponse {
    /** Error code. */
    Error: number;
    /** Error message. */
    Message?: string;
    /** Additional context. */
    Context?: string;
    /** Detailed description. */
    Description?: string;
}
//# sourceMappingURL=index.d.ts.map
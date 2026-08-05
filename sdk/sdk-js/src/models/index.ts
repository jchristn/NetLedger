/**
 * Entry type enumeration.
 */
export enum EntryType {
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
export enum EnumerationOrder {
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
    Id: string;
    /** Tenant identifier. */
    TenantId: string;
    /** Name of the account. */
    Name: string;
    /** Optional notes. */
    Notes?: string;
    /** Optional unit or currency label (e.g. "USD" or "tokens"). */
    Units?: string | null;
    /** Account labels. */
    Labels: string[];
    /** Account tags. */
    Tags: Record<string, string>;
    /** UTC timestamp when created. */
    CreatedUtc: string;
    /** UTC timestamp when last updated. */
    LastUpdateUtc: string;
    /** Whether the account is active. */
    Active: boolean;
}

/**
 * Represents a ledger entry.
 */
export interface Entry {
    /** Unique identifier for the entry. */
    Id: string;
    /** Tenant identifier. */
    TenantId: string;
    /** Account this entry belongs to. */
    AccountId: string;
    /** Type of entry (Credit, Debit, Balance). */
    Type: EntryType;
    /** Monetary amount. */
    Amount: number;
    /** Optional description. */
    Description?: string;
    /** For balance entries, the Identifier of the replaced balance. */
    Replaces?: string;
    /** Whether the entry is committed. */
    IsCommitted: boolean;
    /** Identifier of the balance entry that committed this. */
    CommittedById?: string;
    /** Entry labels. */
    Labels: string[];
    /** Entry tags. */
    Tags: Record<string, string>;
    /** UTC timestamp when committed. */
    CommittedUtc?: string;
    /** UTC timestamp when created. */
    CreatedUtc: string;
    /** UTC timestamp when last updated. */
    LastUpdateUtc: string;
}

/**
 * Input for creating an entry.
 */
export interface EntryInput {
    /** Monetary amount (must be positive). */
    Amount: number;
    /** Optional notes. */
    Notes?: string;
    /** Labels to attach to the entry. */
    Labels?: string[];
    /** Tags to attach to the entry. */
    Tags?: Record<string, string>;
}

/**
 * Response from adding entries.
 */
export interface AddEntriesResponse {
    /** Identifiers of created entries. */
    EntryIds: string[];
}

/**
 * Historical balance at a point in time.
 * Note: Properties are lowercase as returned by the server.
 */
export interface HistoricalBalance {
    /** Account identifier. */
    accountId: string;
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
    /** Account identifier. */
    AccountId: string;
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
    /** Specific entry identifiers to commit (null = all). */
    EntryIds?: string[];
}

/**
 * Information about an API key.
 */
export interface ApiKeyInfo {
    /** Unique identifier. */
    Id: string;
    /** Tenant identifier. */
    TenantId?: string;
    /** Owning user identifier. */
    UserId?: string;
    /** Display name. */
    Name: string;
    /** The API key value (only on creation). */
    Key?: string;
    /** Last four characters of the credential secret key. */
    SecretKeyLast4?: string;
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
export interface ActiveArchiveInfo {
    /** Whether archive integration is enabled. */
    Enabled: boolean;
    /** Archive Server endpoint when enabled. */
    ArchiveServerEndpoint?: string;
    /** Active retention period for the resolved tenant. */
    ActiveDataRetentionDays: number;
    /** Oldest timestamp retained by active APIs. */
    ActiveBoundaryUtc: string;
}

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
    /** Active archive integration information. */
    Archive?: ActiveArchiveInfo;
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
    /** Labels that must all match. */
    Labels?: string[];
    /** Tags that must all match. */
    Tags?: Record<string, string>;
}

/**
 * Query for enumerating entries.
 */
export interface EntryEnumerationQuery {
    /** Maximum results (1-1000). */
    MaxResults?: number;
    /** Number to skip. */
    Skip?: number;
    /** Continuation token. */
    ContinuationToken?: string;
    /** Search term for entry description. */
    SearchTerm?: string;
    /** Filter: created after. */
    CreatedAfterUtc?: string;
    /** Filter: created before. */
    CreatedBeforeUtc?: string;
    /** Filter: minimum amount. */
    AmountMinimum?: number;
    /** Filter: maximum amount. */
    AmountMaximum?: number;
    /** Credit minimum filter. */
    CreditMinimum?: number;
    /** Credit maximum filter. */
    CreditMaximum?: number;
    /** Debit minimum filter. */
    DebitMinimum?: number;
    /** Debit maximum filter. */
    DebitMaximum?: number;
    /** Labels that must all match. */
    Labels?: string[];
    /** Tags that must all match. */
    Tags?: Record<string, string>;
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
    /** Optional tenant identifier. */
    TenantId?: string;
}

export interface RequestHistoryEntry {
    Id: string;
    TenantId?: string;
    PrincipalId?: string;
    PrincipalType?: string;
    Method: string;
    Path: string;
    Url: string;
    StatusCode: number;
    DurationMs: number;
    SourceIp?: string;
    RequestHeaders: Record<string, string>;
    RequestBody?: string;
    RequestBodyBytes: number;
    RequestBodyTruncated: boolean;
    ResponseHeaders: Record<string, string>;
    ResponseBody?: string;
    ResponseBodyBytes: number;
    ResponseBodyTruncated: boolean;
    CreatedUtc: string;
    CompletedUtc?: string;
}

export interface RequestHistoryQuery {
    TenantId?: string;
    PrincipalId?: string;
    Method?: string;
    StatusCode?: number;
    PathContains?: string;
    FromUtc?: string;
    ToUtc?: string;
    MaxResults?: number;
    Skip?: number;
    ContinuationToken?: string;
    BucketMinutes?: number;
    AllowPartial?: boolean;
}

export interface RequestHistorySummaryBucket {
    BucketStartUtc: string;
    BucketEndUtc: string;
    SuccessCount: number;
    FailureCount: number;
    AverageDurationMs: number;
}

export interface RequestHistorySummary {
    TotalCount: number;
    TotalSuccess: number;
    TotalFailure: number;
    AverageDurationMs: number;
    Buckets: RequestHistorySummaryBucket[];
}

export interface RequestHistoryDeleteResult {
    DeletedCount: number;
}

export interface ArchiveHealth {
    Healthy: boolean;
    Version?: string;
    Details?: string[];
}

export interface ArchiveQuery {
    maxResults?: number;
    skip?: number;
    continuationToken?: string;
    search?: string;
    tenantId?: string;
    accountId?: string;
    entityType?: string;
    storagePoolId?: string;
    migrationId?: string;
    manifestStatus?: string;
    migrationStatus?: string;
    fromUtc?: string;
    toUtc?: string;
    startTime?: string;
    endTime?: string;
    ordering?: string;
    amountMinimum?: number;
    amountMaximum?: number;
    creditMinimum?: number;
    creditMaximum?: number;
    debitMinimum?: number;
    debitMaximum?: number;
    labels?: string[];
    tags?: Record<string, string>;
    allowPartial?: boolean;
}

export interface ArchiveRangeInfo {
    TenantId: string;
    AccountId?: string;
    EntityType: string;
    FromUtc: string;
    ToUtc: string;
    RowCount: number;
}

export interface ArchiveManifest {
    Id: string;
    TenantId: string;
    AccountId?: string;
    EntityType: string;
    StoragePoolId: string;
    FromUtc: string;
    ToUtc: string;
    RowCount: number;
    ContentHashSha256: string;
    ManifestHashSha256: string;
    Status: string;
    CreatedUtc: string;
    LastUpdateUtc: string;
}

export interface ArchiveObject {
    Id: string;
    ManifestId: string;
    StoragePoolId: string;
    RelativePath: string;
    RowCount: number;
    ByteCount: number;
    ContentHashSha256: string;
    CreatedUtc: string;
}

export interface ArchiveBalanceCheckpoint {
    Id: string;
    TenantId: string;
    AccountId: string;
    ManifestId: string;
    AsOfUtc: string;
    Balance: number;
    CreatedUtc: string;
}

export interface ArchiveStorageObjectMetadata {
    Exists: boolean;
    ByteCount?: number;
    LastModifiedUtc?: string;
    IsReadOnly?: boolean;
    Properties: Record<string, string>;
}

export interface ArchiveObjectMetadata {
    ObjectId?: string;
    ManifestId?: string;
    StoragePoolId?: string;
    CatalogByteCount: number;
    CatalogContentHashSha256?: string;
    Storage: ArchiveStorageObjectMetadata;
}

export interface ArchiveStoragePool {
    Id: string;
    Name: string;
    Type: string;
    Prefix: string;
    Format: string;
    Compression: string;
}

export interface ArchiveStoragePoolHealth {
    Healthy: boolean;
    StoragePoolId?: string;
    Type?: string;
    Detail?: string;
    CheckedUtc?: string;
}

export interface ArchiveMigration {
    Id: string;
    TenantId: string;
    AccountId?: string;
    EntityType: string;
    StoragePoolId: string;
    Format: string;
    Compression: string;
    FromUtc: string;
    ToUtc: string;
    Status: string;
    IdempotencyKey: string;
    CreatedUtc: string;
    LastUpdateUtc: string;
}

export interface ArchiveMigrationRequest {
    TenantId?: string;
    AccountId?: string;
    EntityType?: string;
    StoragePoolId?: string;
    Format?: string;
    Compression?: string;
    FromUtc?: string;
    ToUtc?: string;
    IdempotencyKey?: string;
}

export interface ArchiveMigrationBatchRequest {
    Id?: string;
    SequenceNumber: number;
    RowCount: number;
    ByteCount?: number;
    ContentHashSha256?: string;
}

export interface ArchiveMigrationBatch {
    Id: string;
    MigrationId: string;
    StoragePoolId: string;
    TenantId: string;
    AccountId?: string;
    SequenceNumber: number;
    RowCount: number;
    ByteCount: number;
    ContentHashSha256: string;
    TemporaryRelativePath: string;
    CommittedRelativePath: string;
    Status: string;
    CreatedUtc: string;
    LastUpdateUtc: string;
}

export interface ArchiveBalanceInfo {
    AccountId?: string;
    TenantId?: string;
    AsOfUtc: string;
    Balance: number;
    ManifestId?: string;
}

export interface ArchiveExportRequest {
    TenantId?: string;
    AccountId?: string;
    FromUtc?: string;
    ToUtc?: string;
    StoragePoolId?: string;
    IdempotencyKey?: string;
    MaxBatchRows?: number;
    DeleteAfterCommit?: boolean;
}

export interface ArchiveExportBatchResult {
    BatchId?: string;
    SequenceNumber: number;
    RowCount: number;
    ByteCount: number;
    ContentHashSha256?: string;
}

export interface ArchiveExportResponse {
    MigrationId?: string;
    ManifestId?: string;
    TenantId?: string;
    AccountId?: string;
    RowsExported: number;
    BytesUploaded: number;
    ActiveCleanupExecuted: boolean;
    ActiveCleanupRowsDeleted: number;
    Batches: ArchiveExportBatchResult[];
}

export interface ArchiveVerificationResult {
    TenantId: string;
    AccountId: string;
    IsValid: boolean;
    CheckedManifests: number;
    CheckedObjects: number;
    CheckedBalanceCheckpoints: number;
    Details: string[];
    Errors: string[];
}

export interface TenantInfo {
    Id: string;
    ParentId?: string;
    Name: string;
    Region?: string;
    Active: boolean;
    IsProtected: boolean;
    CreatedUtc: string;
    LastUpdateUtc: string;
}

export interface UserInfo {
    Id: string;
    TenantId: string;
    FirstName?: string;
    LastName?: string;
    Email: string;
    IsAdmin: boolean;
    IsTenantAdmin: boolean;
    Active: boolean;
    IsProtected: boolean;
    CreatedUtc: string;
    LastUpdateUtc: string;
}

export interface AuthSessionInfo {
    Id: string;
    TenantId: string;
    UserId?: string;
    CredentialId?: string;
    Token?: string;
    Active: boolean;
    ExpiresUtc: string;
    CreatedUtc: string;
}

/**
 * Credential creation response with the one-time secret key.
 */
export interface CredentialCreateResponse {
    /** Created credential. */
    Credential?: ApiKeyInfo;
    /** Raw secret key shown only once. */
    SecretKey?: string;
}

export interface AuditRecordInfo {
    Id: string;
    TenantId: string;
    PrincipalId?: string;
    PrincipalType?: string;
    EventType: string;
    ResourceType?: string;
    OperationType?: string;
    ResourceId?: string;
    Result: string;
    Reason?: string;
    RequestId?: string;
    CreatedUtc: string;
}

export interface RoleInfo {
    Id: string;
    TenantId?: string;
    Name: string;
    IsBuiltIn: boolean;
    Active: boolean;
    IsProtected: boolean;
    CreatedUtc: string;
    LastUpdateUtc: string;
}

export interface PermissionInfo {
    Id: string;
    TenantId?: string;
    Name: string;
    ResourceTypes: string[];
    OperationTypes: string[];
    PermissionType: string;
    Active: boolean;
    IsProtected: boolean;
    CreatedUtc: string;
    LastUpdateUtc: string;
}

export interface UserRoleAssignmentInfo {
    Id?: string;
    TenantId?: string;
    UserId?: string;
    RoleId?: string;
    RoleName?: string;
    ResourceScope?: string;
    ResourceId?: string;
    InheritsToChildren?: boolean;
    Active?: boolean;
}

export interface EffectivePermissionsInfo {
    TenantId?: string;
    PrincipalId?: string;
    PrincipalType?: string;
    Permissions: Array<{
        ResourceType: string;
        OperationType: string;
        ResourceScope: string;
        ResourceId?: string;
        PermissionType: string;
    }>;
}

/**
 * API response wrapper.
 */
export interface ApiResponse<T> {
    /** Response data. */
    Data?: T;
    /** HTTP status code. */
    StatusCode: number;
    /** Request identifier. */
    RequestId?: string;
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

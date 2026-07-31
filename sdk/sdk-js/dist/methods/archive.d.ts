import { ArchiveBalanceCheckpoint, ArchiveBalanceInfo, ArchiveHealth, ArchiveExportRequest, ArchiveExportResponse, ArchiveManifest, ArchiveMigration, ArchiveMigrationBatch, ArchiveMigrationBatchRequest, ArchiveMigrationRequest, ArchiveObject, ArchiveObjectMetadata, ArchiveQuery, ArchiveRangeInfo, ArchiveStoragePoolHealth, ArchiveStoragePool, ArchiveVerificationResult, Entry, EnumerationResult, RequestHistoryEntry, RequestHistoryQuery, RequestHistorySummary } from '../models';
import { HttpClient } from '../http-client';
/**
 * Archive Server operations for cold data and archive metadata.
 */
export declare class ArchiveMethods {
    private readonly client;
    constructor(client: HttpClient);
    health(): Promise<ArchiveHealth>;
    ranges(query?: ArchiveQuery): Promise<ArchiveRangeInfo[]>;
    manifests(query?: ArchiveQuery): Promise<ArchiveManifest[]>;
    manifest(manifestId: string): Promise<ArchiveManifest>;
    manifestObjects(manifestId: string, query?: ArchiveQuery): Promise<ArchiveObject[]>;
    manifestCheckpoints(manifestId: string, query?: ArchiveQuery): Promise<ArchiveBalanceCheckpoint[]>;
    verifyManifest(manifestId: string): Promise<void>;
    quarantineManifest(manifestId: string): Promise<void>;
    supersedeManifest(manifestId: string): Promise<void>;
    storagePools(query?: ArchiveQuery): Promise<ArchiveStoragePool[]>;
    storagePoolHealth(storagePoolId: string): Promise<ArchiveStoragePoolHealth>;
    migrations(query?: ArchiveQuery): Promise<ArchiveMigration[]>;
    migration(migrationId: string): Promise<ArchiveMigration>;
    migrationBatches(migrationId: string, query?: ArchiveQuery): Promise<ArchiveMigrationBatch[]>;
    createMigration(request: ArchiveMigrationRequest): Promise<ArchiveMigration>;
    createMigrationBatch(migrationId: string, request: ArchiveMigrationBatchRequest): Promise<ArchiveMigrationBatch>;
    uploadMigrationBatchContent(migrationId: string, batchId: string, content: Buffer | Uint8Array | string, contentHashSha256?: string, contentType?: string): Promise<ArchiveMigrationBatch>;
    sealMigration(migrationId: string): Promise<ArchiveMigration>;
    commitMigration(migrationId: string): Promise<ArchiveManifest>;
    abortMigration(migrationId: string): Promise<ArchiveMigration>;
    exportEntries(request: ArchiveExportRequest): Promise<ArchiveExportResponse>;
    exportRequestHistory(request: ArchiveExportRequest): Promise<ArchiveExportResponse>;
    exportTenantAccountEntries(tenantId: string, accountId: string, request?: ArchiveExportRequest): Promise<ArchiveExportResponse>;
    entries(accountId: string, query?: ArchiveQuery): Promise<EnumerationResult<Entry>>;
    tenantEntries(tenantId: string, accountId: string, query?: ArchiveQuery): Promise<EnumerationResult<Entry>>;
    balanceAsOf(accountId: string, asOfUtc: string | Date): Promise<ArchiveBalanceInfo>;
    tenantBalanceAsOf(tenantId: string, accountId: string, asOfUtc: string | Date): Promise<ArchiveBalanceInfo>;
    verifyAccount(accountId: string, query?: ArchiveQuery): Promise<ArchiveVerificationResult>;
    verifyTenantAccount(tenantId: string, accountId: string, query?: ArchiveQuery): Promise<ArchiveVerificationResult>;
    objectMetadata(objectId: string): Promise<ArchiveObjectMetadata>;
    requestHistory(query?: RequestHistoryQuery): Promise<EnumerationResult<RequestHistoryEntry>>;
    requestHistorySummary(query?: RequestHistoryQuery): Promise<RequestHistorySummary>;
    requestHistoryEntry(id: string, query?: RequestHistoryQuery): Promise<RequestHistoryEntry>;
    archiveServerRequestHistory(query?: RequestHistoryQuery): Promise<EnumerationResult<RequestHistoryEntry>>;
    archiveServerRequestHistorySummary(query?: RequestHistoryQuery): Promise<RequestHistorySummary>;
    archiveServerRequestHistoryEntry(id: string, query?: RequestHistoryQuery): Promise<RequestHistoryEntry>;
}
//# sourceMappingURL=archive.d.ts.map
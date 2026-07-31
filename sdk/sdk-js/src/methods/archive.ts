import {
    ArchiveBalanceCheckpoint,
    ArchiveBalanceInfo,
    ArchiveHealth,
    ArchiveExportRequest,
    ArchiveExportResponse,
    ArchiveManifest,
    ArchiveMigration,
    ArchiveMigrationBatch,
    ArchiveMigrationBatchRequest,
    ArchiveMigrationRequest,
    ArchiveObject,
    ArchiveObjectMetadata,
    ArchiveQuery,
    ArchiveRangeInfo,
    ArchiveStoragePoolHealth,
    ArchiveStoragePool,
    ArchiveVerificationResult,
    Entry,
    EnumerationResult,
    RequestHistoryEntry,
    RequestHistoryQuery,
    RequestHistorySummary
} from '../models';
import { HttpClient } from '../http-client';
import { URLSearchParams } from 'url';

function appendQuery(path: string, query?: ArchiveQuery): string {
    if (!query) return path;

    const params = new URLSearchParams();
    Object.entries(query).forEach(([key, value]) => {
        if (value !== null && value !== undefined && value !== '') {
            if (Array.isArray(value)) {
                params.append(key, value.join(','));
            } else if (typeof value === 'object') {
                params.append(key, Object.entries(value).map(([itemKey, itemValue]) => `${itemKey}=${itemValue}`).join(','));
            } else {
                params.append(key, String(value));
            }
        }
    });

    const queryString = params.toString();
    return queryString ? `${path}?${queryString}` : path;
}

function appendRequestHistoryQuery(path: string, query?: RequestHistoryQuery): string {
    if (!query) return path;

    const params = new URLSearchParams();
    Object.entries(query).forEach(([key, value]) => {
        if (value !== null && value !== undefined && value !== '') {
            params.append(key.charAt(0).toLowerCase() + key.slice(1), String(value));
        }
    });

    const queryString = params.toString();
    return queryString ? `${path}?${queryString}` : path;
}

/**
 * Archive Server operations for cold data and archive metadata.
 */
export class ArchiveMethods {
    private readonly client: HttpClient;

    constructor(client: HttpClient) {
        this.client = client;
    }

    async health(): Promise<ArchiveHealth> {
        const response = await this.client.get<ArchiveHealth>('/v1/health');
        return response.Data || { Healthy: false };
    }

    async ranges(query?: ArchiveQuery): Promise<ArchiveRangeInfo[]> {
        const response = await this.client.get<ArchiveRangeInfo[]>(appendQuery('/v1/archive/ranges', query));
        return response.Data || [];
    }

    async manifests(query?: ArchiveQuery): Promise<ArchiveManifest[]> {
        const response = await this.client.get<ArchiveManifest[]>(appendQuery('/v1/archive/manifests', query));
        return response.Data || [];
    }

    async manifest(manifestId: string): Promise<ArchiveManifest> {
        const response = await this.client.get<ArchiveManifest>(`/v1/archive/manifests/${encodeURIComponent(manifestId)}`);
        if (!response.Data) throw new Error('No manifest returned from Archive Server');
        return response.Data;
    }

    async manifestObjects(manifestId: string, query?: ArchiveQuery): Promise<ArchiveObject[]> {
        const response = await this.client.get<ArchiveObject[]>(
            appendQuery(`/v1/archive/manifests/${encodeURIComponent(manifestId)}/objects`, query)
        );
        return response.Data || [];
    }

    async manifestCheckpoints(manifestId: string, query?: ArchiveQuery): Promise<ArchiveBalanceCheckpoint[]> {
        const response = await this.client.get<ArchiveBalanceCheckpoint[]>(
            appendQuery(`/v1/archive/manifests/${encodeURIComponent(manifestId)}/checkpoints`, query)
        );
        return response.Data || [];
    }

    async verifyManifest(manifestId: string): Promise<void> {
        await this.client.post(`/v1/archive/manifests/${encodeURIComponent(manifestId)}/verify`);
    }

    async quarantineManifest(manifestId: string): Promise<void> {
        await this.client.post(`/v1/archive/manifests/${encodeURIComponent(manifestId)}/quarantine`);
    }

    async supersedeManifest(manifestId: string): Promise<void> {
        await this.client.post(`/v1/archive/manifests/${encodeURIComponent(manifestId)}/supersede`);
    }

    async storagePools(query?: ArchiveQuery): Promise<ArchiveStoragePool[]> {
        const response = await this.client.get<ArchiveStoragePool[]>(appendQuery('/v1/archive/storage-pools', query));
        return response.Data || [];
    }

    async storagePoolHealth(storagePoolId: string): Promise<ArchiveStoragePoolHealth> {
        const response = await this.client.get<ArchiveStoragePoolHealth>(`/v1/archive/storage-pools/${encodeURIComponent(storagePoolId)}/health`);
        return response.Data || { Healthy: false, StoragePoolId: storagePoolId };
    }

    async migrations(query?: ArchiveQuery): Promise<ArchiveMigration[]> {
        const response = await this.client.get<ArchiveMigration[]>(appendQuery('/v1/archive/migrations', query));
        return response.Data || [];
    }

    async migration(migrationId: string): Promise<ArchiveMigration> {
        const response = await this.client.get<ArchiveMigration>(`/v1/archive/migrations/${encodeURIComponent(migrationId)}`);
        if (!response.Data) throw new Error('No migration returned from Archive Server');
        return response.Data;
    }

    async migrationBatches(migrationId: string, query?: ArchiveQuery): Promise<ArchiveMigrationBatch[]> {
        const response = await this.client.get<ArchiveMigrationBatch[]>(
            appendQuery(`/v1/archive/migrations/${encodeURIComponent(migrationId)}/batches`, query)
        );
        return response.Data || [];
    }

    async createMigration(request: ArchiveMigrationRequest): Promise<ArchiveMigration> {
        const response = await this.client.post<ArchiveMigration>('/v1/archive/migrations', request);
        if (!response.Data) throw new Error('No migration returned from Archive Server');
        return response.Data;
    }

    async createMigrationBatch(migrationId: string, request: ArchiveMigrationBatchRequest): Promise<ArchiveMigrationBatch> {
        const response = await this.client.post<ArchiveMigrationBatch>(
            `/v1/archive/migrations/${encodeURIComponent(migrationId)}/batches`,
            request
        );
        if (!response.Data) throw new Error('No migration batch returned from Archive Server');
        return response.Data;
    }

    async uploadMigrationBatchContent(
        migrationId: string,
        batchId: string,
        content: Buffer | Uint8Array | string,
        contentHashSha256?: string,
        contentType: string = 'application/gzip'
    ): Promise<ArchiveMigrationBatch> {
        const response = await this.client.putRaw<ArchiveMigrationBatch>(
            `/v1/archive/migrations/${encodeURIComponent(migrationId)}/batches/${encodeURIComponent(batchId)}/content`,
            content,
            contentType,
            contentHashSha256 ? { 'x-content-sha256': contentHashSha256 } : undefined
        );
        if (!response.Data) throw new Error('No migration batch returned from Archive Server');
        return response.Data;
    }

    async sealMigration(migrationId: string): Promise<ArchiveMigration> {
        const response = await this.client.post<ArchiveMigration>(`/v1/archive/migrations/${encodeURIComponent(migrationId)}/seal`);
        if (!response.Data) throw new Error('No migration returned from Archive Server');
        return response.Data;
    }

    async commitMigration(migrationId: string): Promise<ArchiveManifest> {
        const response = await this.client.post<ArchiveManifest>(`/v1/archive/migrations/${encodeURIComponent(migrationId)}/commit`);
        if (!response.Data) throw new Error('No manifest returned from Archive Server');
        return response.Data;
    }

    async abortMigration(migrationId: string): Promise<ArchiveMigration> {
        const response = await this.client.post<ArchiveMigration>(`/v1/archive/migrations/${encodeURIComponent(migrationId)}/abort`);
        if (!response.Data) throw new Error('No migration returned from Archive Server');
        return response.Data;
    }

    async exportEntries(request: ArchiveExportRequest): Promise<ArchiveExportResponse> {
        const response = await this.client.post<ArchiveExportResponse>('/v1/archive/exports/entries', request);
        return response.Data || { RowsExported: 0, BytesUploaded: 0, ActiveCleanupExecuted: false, ActiveCleanupRowsDeleted: 0, Batches: [] };
    }

    async exportRequestHistory(request: ArchiveExportRequest): Promise<ArchiveExportResponse> {
        const response = await this.client.post<ArchiveExportResponse>('/v1/archive/exports/request-history', request);
        return response.Data || { RowsExported: 0, BytesUploaded: 0, ActiveCleanupExecuted: false, ActiveCleanupRowsDeleted: 0, Batches: [] };
    }

    async exportTenantAccountEntries(tenantId: string, accountId: string, request?: ArchiveExportRequest): Promise<ArchiveExportResponse> {
        const response = await this.client.post<ArchiveExportResponse>(
            `/v1/tenants/${encodeURIComponent(tenantId)}/accounts/${encodeURIComponent(accountId)}/archive/export`,
            request || {}
        );
        return response.Data || { RowsExported: 0, BytesUploaded: 0, ActiveCleanupExecuted: false, ActiveCleanupRowsDeleted: 0, Batches: [] };
    }

    async entries(accountId: string, query?: ArchiveQuery): Promise<EnumerationResult<Entry>> {
        const response = await this.client.get<EnumerationResult<Entry>>(
            appendQuery(`/v1/archive/accounts/${encodeURIComponent(accountId)}/entries`, query)
        );
        return response.Data || { TotalRecords: 0, RecordsRemaining: 0, EndOfResults: true, Objects: [] };
    }

    async tenantEntries(tenantId: string, accountId: string, query?: ArchiveQuery): Promise<EnumerationResult<Entry>> {
        const response = await this.client.get<EnumerationResult<Entry>>(
            appendQuery(`/v1/tenants/${encodeURIComponent(tenantId)}/accounts/${encodeURIComponent(accountId)}/entries`, query)
        );
        return response.Data || { TotalRecords: 0, RecordsRemaining: 0, EndOfResults: true, Objects: [] };
    }

    async balanceAsOf(accountId: string, asOfUtc: string | Date): Promise<ArchiveBalanceInfo> {
        const asOf = asOfUtc instanceof Date ? asOfUtc.toISOString() : asOfUtc;
        const response = await this.client.get<ArchiveBalanceInfo>(
            `/v1/archive/accounts/${encodeURIComponent(accountId)}/balance/asof?asOf=${encodeURIComponent(asOf)}`
        );
        if (!response.Data) throw new Error('No archived balance returned from Archive Server');
        return response.Data;
    }

    async tenantBalanceAsOf(tenantId: string, accountId: string, asOfUtc: string | Date): Promise<ArchiveBalanceInfo> {
        const asOf = asOfUtc instanceof Date ? asOfUtc.toISOString() : asOfUtc;
        const response = await this.client.get<ArchiveBalanceInfo>(
            `/v1/tenants/${encodeURIComponent(tenantId)}/accounts/${encodeURIComponent(accountId)}/balance/asof?asOf=${encodeURIComponent(asOf)}`
        );
        if (!response.Data) throw new Error('No archived balance returned from Archive Server');
        return response.Data;
    }

    async verifyAccount(accountId: string, query?: ArchiveQuery): Promise<ArchiveVerificationResult> {
        const response = await this.client.get<ArchiveVerificationResult>(
            appendQuery(`/v1/archive/accounts/${encodeURIComponent(accountId)}/verify`, query)
        );
        return response.Data || { TenantId: '', AccountId: accountId, IsValid: true, CheckedManifests: 0, CheckedObjects: 0, CheckedBalanceCheckpoints: 0, Details: [], Errors: [] };
    }

    async verifyTenantAccount(tenantId: string, accountId: string, query?: ArchiveQuery): Promise<ArchiveVerificationResult> {
        const response = await this.client.get<ArchiveVerificationResult>(
            appendQuery(`/v1/tenants/${encodeURIComponent(tenantId)}/accounts/${encodeURIComponent(accountId)}/verify`, query)
        );
        return response.Data || { TenantId: tenantId, AccountId: accountId, IsValid: true, CheckedManifests: 0, CheckedObjects: 0, CheckedBalanceCheckpoints: 0, Details: [], Errors: [] };
    }

    async objectMetadata(objectId: string): Promise<ArchiveObjectMetadata> {
        const response = await this.client.get<ArchiveObjectMetadata>(`/v1/archive/objects/${encodeURIComponent(objectId)}/metadata`);
        if (!response.Data) throw new Error('No archive object metadata returned from Archive Server');
        return response.Data;
    }

    async requestHistory(query?: RequestHistoryQuery): Promise<EnumerationResult<RequestHistoryEntry>> {
        const response = await this.client.get<EnumerationResult<RequestHistoryEntry>>(appendRequestHistoryQuery('/v1/request-history', query));
        return response.Data || { TotalRecords: 0, RecordsRemaining: 0, EndOfResults: true, Objects: [] };
    }

    async requestHistorySummary(query?: RequestHistoryQuery): Promise<RequestHistorySummary> {
        const response = await this.client.get<RequestHistorySummary>(appendRequestHistoryQuery('/v1/request-history/summary', query));
        return response.Data || { TotalCount: 0, TotalSuccess: 0, TotalFailure: 0, AverageDurationMs: 0, Buckets: [] };
    }

    async requestHistoryEntry(id: string, query?: RequestHistoryQuery): Promise<RequestHistoryEntry> {
        const response = await this.client.get<RequestHistoryEntry>(appendRequestHistoryQuery(`/v1/request-history/${encodeURIComponent(id)}`, query));
        if (!response.Data) throw new Error('No request history entry returned from Archive Server');
        return response.Data;
    }

    async archiveServerRequestHistory(query?: RequestHistoryQuery): Promise<EnumerationResult<RequestHistoryEntry>> {
        const response = await this.client.get<EnumerationResult<RequestHistoryEntry>>(appendRequestHistoryQuery('/v1/archive-server/request-history', query));
        return response.Data || { TotalRecords: 0, RecordsRemaining: 0, EndOfResults: true, Objects: [] };
    }

    async archiveServerRequestHistorySummary(query?: RequestHistoryQuery): Promise<RequestHistorySummary> {
        const response = await this.client.get<RequestHistorySummary>(appendRequestHistoryQuery('/v1/archive-server/request-history/summary', query));
        return response.Data || { TotalCount: 0, TotalSuccess: 0, TotalFailure: 0, AverageDurationMs: 0, Buckets: [] };
    }

    async archiveServerRequestHistoryEntry(id: string, query?: RequestHistoryQuery): Promise<RequestHistoryEntry> {
        const response = await this.client.get<RequestHistoryEntry>(appendRequestHistoryQuery(`/v1/archive-server/request-history/${encodeURIComponent(id)}`, query));
        if (!response.Data) throw new Error('No Archive Server request history entry returned');
        return response.Data;
    }
}

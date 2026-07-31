"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.ArchiveMethods = void 0;
const url_1 = require("url");
function appendQuery(path, query) {
    if (!query)
        return path;
    const params = new url_1.URLSearchParams();
    Object.entries(query).forEach(([key, value]) => {
        if (value !== null && value !== undefined && value !== '') {
            if (Array.isArray(value)) {
                params.append(key, value.join(','));
            }
            else if (typeof value === 'object') {
                params.append(key, Object.entries(value).map(([itemKey, itemValue]) => `${itemKey}=${itemValue}`).join(','));
            }
            else {
                params.append(key, String(value));
            }
        }
    });
    const queryString = params.toString();
    return queryString ? `${path}?${queryString}` : path;
}
function appendRequestHistoryQuery(path, query) {
    if (!query)
        return path;
    const params = new url_1.URLSearchParams();
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
class ArchiveMethods {
    constructor(client) {
        this.client = client;
    }
    async health() {
        const response = await this.client.get('/v1/health');
        return response.Data || { Healthy: false };
    }
    async ranges(query) {
        const response = await this.client.get(appendQuery('/v1/archive/ranges', query));
        return response.Data || [];
    }
    async manifests(query) {
        const response = await this.client.get(appendQuery('/v1/archive/manifests', query));
        return response.Data || [];
    }
    async manifest(manifestId) {
        const response = await this.client.get(`/v1/archive/manifests/${encodeURIComponent(manifestId)}`);
        if (!response.Data)
            throw new Error('No manifest returned from Archive Server');
        return response.Data;
    }
    async manifestObjects(manifestId, query) {
        const response = await this.client.get(appendQuery(`/v1/archive/manifests/${encodeURIComponent(manifestId)}/objects`, query));
        return response.Data || [];
    }
    async manifestCheckpoints(manifestId, query) {
        const response = await this.client.get(appendQuery(`/v1/archive/manifests/${encodeURIComponent(manifestId)}/checkpoints`, query));
        return response.Data || [];
    }
    async verifyManifest(manifestId) {
        await this.client.post(`/v1/archive/manifests/${encodeURIComponent(manifestId)}/verify`);
    }
    async quarantineManifest(manifestId) {
        await this.client.post(`/v1/archive/manifests/${encodeURIComponent(manifestId)}/quarantine`);
    }
    async supersedeManifest(manifestId) {
        await this.client.post(`/v1/archive/manifests/${encodeURIComponent(manifestId)}/supersede`);
    }
    async storagePools(query) {
        const response = await this.client.get(appendQuery('/v1/archive/storage-pools', query));
        return response.Data || [];
    }
    async storagePoolHealth(storagePoolId) {
        const response = await this.client.get(`/v1/archive/storage-pools/${encodeURIComponent(storagePoolId)}/health`);
        return response.Data || { Healthy: false, StoragePoolId: storagePoolId };
    }
    async migrations(query) {
        const response = await this.client.get(appendQuery('/v1/archive/migrations', query));
        return response.Data || [];
    }
    async migration(migrationId) {
        const response = await this.client.get(`/v1/archive/migrations/${encodeURIComponent(migrationId)}`);
        if (!response.Data)
            throw new Error('No migration returned from Archive Server');
        return response.Data;
    }
    async migrationBatches(migrationId, query) {
        const response = await this.client.get(appendQuery(`/v1/archive/migrations/${encodeURIComponent(migrationId)}/batches`, query));
        return response.Data || [];
    }
    async createMigration(request) {
        const response = await this.client.post('/v1/archive/migrations', request);
        if (!response.Data)
            throw new Error('No migration returned from Archive Server');
        return response.Data;
    }
    async createMigrationBatch(migrationId, request) {
        const response = await this.client.post(`/v1/archive/migrations/${encodeURIComponent(migrationId)}/batches`, request);
        if (!response.Data)
            throw new Error('No migration batch returned from Archive Server');
        return response.Data;
    }
    async uploadMigrationBatchContent(migrationId, batchId, content, contentHashSha256, contentType = 'application/gzip') {
        const response = await this.client.putRaw(`/v1/archive/migrations/${encodeURIComponent(migrationId)}/batches/${encodeURIComponent(batchId)}/content`, content, contentType, contentHashSha256 ? { 'x-content-sha256': contentHashSha256 } : undefined);
        if (!response.Data)
            throw new Error('No migration batch returned from Archive Server');
        return response.Data;
    }
    async sealMigration(migrationId) {
        const response = await this.client.post(`/v1/archive/migrations/${encodeURIComponent(migrationId)}/seal`);
        if (!response.Data)
            throw new Error('No migration returned from Archive Server');
        return response.Data;
    }
    async commitMigration(migrationId) {
        const response = await this.client.post(`/v1/archive/migrations/${encodeURIComponent(migrationId)}/commit`);
        if (!response.Data)
            throw new Error('No manifest returned from Archive Server');
        return response.Data;
    }
    async abortMigration(migrationId) {
        const response = await this.client.post(`/v1/archive/migrations/${encodeURIComponent(migrationId)}/abort`);
        if (!response.Data)
            throw new Error('No migration returned from Archive Server');
        return response.Data;
    }
    async exportEntries(request) {
        const response = await this.client.post('/v1/archive/exports/entries', request);
        return response.Data || { RowsExported: 0, BytesUploaded: 0, ActiveCleanupExecuted: false, ActiveCleanupRowsDeleted: 0, Batches: [] };
    }
    async exportRequestHistory(request) {
        const response = await this.client.post('/v1/archive/exports/request-history', request);
        return response.Data || { RowsExported: 0, BytesUploaded: 0, ActiveCleanupExecuted: false, ActiveCleanupRowsDeleted: 0, Batches: [] };
    }
    async exportTenantAccountEntries(tenantId, accountId, request) {
        const response = await this.client.post(`/v1/tenants/${encodeURIComponent(tenantId)}/accounts/${encodeURIComponent(accountId)}/archive/export`, request || {});
        return response.Data || { RowsExported: 0, BytesUploaded: 0, ActiveCleanupExecuted: false, ActiveCleanupRowsDeleted: 0, Batches: [] };
    }
    async entries(accountId, query) {
        const response = await this.client.get(appendQuery(`/v1/archive/accounts/${encodeURIComponent(accountId)}/entries`, query));
        return response.Data || { TotalRecords: 0, RecordsRemaining: 0, EndOfResults: true, Objects: [] };
    }
    async tenantEntries(tenantId, accountId, query) {
        const response = await this.client.get(appendQuery(`/v1/tenants/${encodeURIComponent(tenantId)}/accounts/${encodeURIComponent(accountId)}/entries`, query));
        return response.Data || { TotalRecords: 0, RecordsRemaining: 0, EndOfResults: true, Objects: [] };
    }
    async balanceAsOf(accountId, asOfUtc) {
        const asOf = asOfUtc instanceof Date ? asOfUtc.toISOString() : asOfUtc;
        const response = await this.client.get(`/v1/archive/accounts/${encodeURIComponent(accountId)}/balance/asof?asOf=${encodeURIComponent(asOf)}`);
        if (!response.Data)
            throw new Error('No archived balance returned from Archive Server');
        return response.Data;
    }
    async tenantBalanceAsOf(tenantId, accountId, asOfUtc) {
        const asOf = asOfUtc instanceof Date ? asOfUtc.toISOString() : asOfUtc;
        const response = await this.client.get(`/v1/tenants/${encodeURIComponent(tenantId)}/accounts/${encodeURIComponent(accountId)}/balance/asof?asOf=${encodeURIComponent(asOf)}`);
        if (!response.Data)
            throw new Error('No archived balance returned from Archive Server');
        return response.Data;
    }
    async verifyAccount(accountId, query) {
        const response = await this.client.get(appendQuery(`/v1/archive/accounts/${encodeURIComponent(accountId)}/verify`, query));
        return response.Data || { TenantId: '', AccountId: accountId, IsValid: true, CheckedManifests: 0, CheckedObjects: 0, CheckedBalanceCheckpoints: 0, Details: [], Errors: [] };
    }
    async verifyTenantAccount(tenantId, accountId, query) {
        const response = await this.client.get(appendQuery(`/v1/tenants/${encodeURIComponent(tenantId)}/accounts/${encodeURIComponent(accountId)}/verify`, query));
        return response.Data || { TenantId: tenantId, AccountId: accountId, IsValid: true, CheckedManifests: 0, CheckedObjects: 0, CheckedBalanceCheckpoints: 0, Details: [], Errors: [] };
    }
    async objectMetadata(objectId) {
        const response = await this.client.get(`/v1/archive/objects/${encodeURIComponent(objectId)}/metadata`);
        if (!response.Data)
            throw new Error('No archive object metadata returned from Archive Server');
        return response.Data;
    }
    async requestHistory(query) {
        const response = await this.client.get(appendRequestHistoryQuery('/v1/request-history', query));
        return response.Data || { TotalRecords: 0, RecordsRemaining: 0, EndOfResults: true, Objects: [] };
    }
    async requestHistorySummary(query) {
        const response = await this.client.get(appendRequestHistoryQuery('/v1/request-history/summary', query));
        return response.Data || { TotalCount: 0, TotalSuccess: 0, TotalFailure: 0, AverageDurationMs: 0, Buckets: [] };
    }
    async requestHistoryEntry(id, query) {
        const response = await this.client.get(appendRequestHistoryQuery(`/v1/request-history/${encodeURIComponent(id)}`, query));
        if (!response.Data)
            throw new Error('No request history entry returned from Archive Server');
        return response.Data;
    }
    async archiveServerRequestHistory(query) {
        const response = await this.client.get(appendRequestHistoryQuery('/v1/archive-server/request-history', query));
        return response.Data || { TotalRecords: 0, RecordsRemaining: 0, EndOfResults: true, Objects: [] };
    }
    async archiveServerRequestHistorySummary(query) {
        const response = await this.client.get(appendRequestHistoryQuery('/v1/archive-server/request-history/summary', query));
        return response.Data || { TotalCount: 0, TotalSuccess: 0, TotalFailure: 0, AverageDurationMs: 0, Buckets: [] };
    }
    async archiveServerRequestHistoryEntry(id, query) {
        const response = await this.client.get(appendRequestHistoryQuery(`/v1/archive-server/request-history/${encodeURIComponent(id)}`, query));
        if (!response.Data)
            throw new Error('No Archive Server request history entry returned');
        return response.Data;
    }
}
exports.ArchiveMethods = ArchiveMethods;
//# sourceMappingURL=archive.js.map
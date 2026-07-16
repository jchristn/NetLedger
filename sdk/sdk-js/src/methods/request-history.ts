import {
    EnumerationResult,
    RequestHistoryDeleteResult,
    RequestHistoryEntry,
    RequestHistoryQuery,
    RequestHistorySummary
} from '../models';
import { HttpClient } from '../http-client';

/**
 * Request history operations.
 */
export class RequestHistoryMethods {
    private readonly client: HttpClient;

    constructor(client: HttpClient) {
        this.client = client;
    }

    /**
     * Enumerate request history entries.
     */
    async enumerate(query?: RequestHistoryQuery): Promise<EnumerationResult<RequestHistoryEntry>> {
        const response = await this.client.get<EnumerationResult<RequestHistoryEntry>>(
            `/v1.0/api/request-history${this.buildQueryString(query, false)}`
        );
        return response.Data || { TotalRecords: 0, RecordsRemaining: 0, EndOfResults: true };
    }

    /**
     * Summarize request history entries.
     */
    async summarize(query?: RequestHistoryQuery): Promise<RequestHistorySummary> {
        const response = await this.client.get<RequestHistorySummary>(
            `/v1.0/api/request-history/summary${this.buildQueryString(query, true)}`
        );
        if (!response.Data) {
            throw new Error('No data returned from server');
        }
        return response.Data;
    }

    /**
     * Read one request history entry.
     */
    async read(id: string): Promise<RequestHistoryEntry> {
        const response = await this.client.get<RequestHistoryEntry>(`/v1.0/api/request-history/${encodeURIComponent(id)}`);
        if (!response.Data) {
            throw new Error('No data returned from server');
        }
        return response.Data;
    }

    /**
     * Delete one request history entry.
     */
    async delete(id: string): Promise<RequestHistoryDeleteResult> {
        const response = await this.client.deleteWithResponse<RequestHistoryDeleteResult>(`/v1.0/api/request-history/${encodeURIComponent(id)}`);
        return response.Data || { DeletedCount: 0 };
    }

    /**
     * Delete matching request history entries.
     */
    async deleteMany(query?: RequestHistoryQuery): Promise<RequestHistoryDeleteResult> {
        const response = await this.client.deleteWithResponse<RequestHistoryDeleteResult>(
            `/v1.0/api/request-history${this.buildQueryString(query, false)}`
        );
        return response.Data || { DeletedCount: 0 };
    }

    private buildQueryString(query?: RequestHistoryQuery, includeBucketMinutes: boolean = false): string {
        const params = new URLSearchParams();
        if (query) {
            if (query.TenantId !== undefined) params.append('tenantId', query.TenantId);
            if (query.PrincipalId !== undefined) params.append('principalId', query.PrincipalId);
            if (query.Method !== undefined) params.append('method', query.Method);
            if (query.StatusCode !== undefined) params.append('statusCode', query.StatusCode.toString());
            if (query.PathContains !== undefined) params.append('pathContains', query.PathContains);
            if (query.FromUtc !== undefined) params.append('fromUtc', query.FromUtc);
            if (query.ToUtc !== undefined) params.append('toUtc', query.ToUtc);
            if (query.MaxResults !== undefined) params.append('maxResults', query.MaxResults.toString());
            if (query.Skip !== undefined) params.append('skip', query.Skip.toString());
            if (includeBucketMinutes && query.BucketMinutes !== undefined) params.append('bucketMinutes', query.BucketMinutes.toString());
        }

        const queryString = params.toString();
        return queryString ? `?${queryString}` : '';
    }
}

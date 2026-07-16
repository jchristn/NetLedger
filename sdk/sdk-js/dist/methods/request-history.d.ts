import { EnumerationResult, RequestHistoryDeleteResult, RequestHistoryEntry, RequestHistoryQuery, RequestHistorySummary } from '../models';
import { HttpClient } from '../http-client';
/**
 * Request history operations.
 */
export declare class RequestHistoryMethods {
    private readonly client;
    constructor(client: HttpClient);
    /**
     * Enumerate request history entries.
     */
    enumerate(query?: RequestHistoryQuery): Promise<EnumerationResult<RequestHistoryEntry>>;
    /**
     * Summarize request history entries.
     */
    summarize(query?: RequestHistoryQuery): Promise<RequestHistorySummary>;
    /**
     * Read one request history entry.
     */
    read(id: string): Promise<RequestHistoryEntry>;
    /**
     * Delete one request history entry.
     */
    delete(id: string): Promise<RequestHistoryDeleteResult>;
    /**
     * Delete matching request history entries.
     */
    deleteMany(query?: RequestHistoryQuery): Promise<RequestHistoryDeleteResult>;
    private buildQueryString;
}
//# sourceMappingURL=request-history.d.ts.map
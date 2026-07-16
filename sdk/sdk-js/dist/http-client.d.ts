import { ApiResponse } from './models';
/**
 * HTTP client for making API requests.
 */
export declare class HttpClient {
    private readonly baseUrl;
    private readonly apiKey;
    private readonly tenantId?;
    private readonly timeoutMs;
    private readonly httpAgent;
    private readonly httpsAgent;
    constructor(baseUrl: string, apiKey: string, timeoutMs?: number, tenantId?: string);
    /**
     * Make a GET request.
     */
    get<T>(path: string): Promise<ApiResponse<T>>;
    /**
     * Make a PUT request.
     */
    put<T>(path: string, body?: unknown): Promise<ApiResponse<T>>;
    /**
     * Make a POST request.
     */
    post<T>(path: string, body?: unknown): Promise<ApiResponse<T>>;
    /**
     * Make a DELETE request.
     */
    delete(path: string): Promise<void>;
    /**
     * Make a DELETE request and return a response body.
     */
    deleteWithResponse<T>(path: string): Promise<ApiResponse<T>>;
    /**
     * Make a HEAD request.
     */
    head(path: string): Promise<boolean>;
    /**
     * Make an HTTP request.
     */
    private request;
}
//# sourceMappingURL=http-client.d.ts.map
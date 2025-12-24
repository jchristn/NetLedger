import * as http from 'http';
import * as https from 'https';
import { URL } from 'url';
import { ApiResponse, ErrorResponse } from './models';
import { NetLedgerConnectionError, NetLedgerApiError } from './errors';

/**
 * HTTP client for making API requests.
 */
export class HttpClient {
    private readonly baseUrl: string;
    private readonly apiKey: string;
    private readonly timeoutMs: number;

    constructor(baseUrl: string, apiKey: string, timeoutMs: number = 30000) {
        this.baseUrl = baseUrl.replace(/\/$/, '');
        this.apiKey = apiKey;
        this.timeoutMs = timeoutMs;
    }

    /**
     * Make a GET request.
     */
    async get<T>(path: string): Promise<ApiResponse<T>> {
        return this.request<T>('GET', path);
    }

    /**
     * Make a PUT request.
     */
    async put<T>(path: string, body?: unknown): Promise<ApiResponse<T>> {
        return this.request<T>('PUT', path, body);
    }

    /**
     * Make a POST request.
     */
    async post<T>(path: string, body?: unknown): Promise<ApiResponse<T>> {
        return this.request<T>('POST', path, body);
    }

    /**
     * Make a DELETE request.
     */
    async delete(path: string): Promise<void> {
        await this.request<unknown>('DELETE', path);
    }

    /**
     * Make a HEAD request.
     */
    async head(path: string): Promise<boolean> {
        return new Promise<boolean>((resolve, reject) => {
            const url = new URL(path, this.baseUrl);
            const isHttps = url.protocol === 'https:';
            const lib = isHttps ? https : http;

            const options: http.RequestOptions = {
                method: 'HEAD',
                hostname: url.hostname,
                port: url.port || (isHttps ? 443 : 80),
                path: url.pathname + url.search,
                timeout: this.timeoutMs,
                headers: {
                    'Authorization': `Bearer ${this.apiKey}`,
                    'Accept': 'application/json'
                }
            };

            const req = lib.request(options, (res) => {
                resolve(res.statusCode !== undefined && res.statusCode >= 200 && res.statusCode < 300);
            });

            req.on('error', (err) => {
                reject(new NetLedgerConnectionError('Failed to connect to the server', err));
            });

            req.on('timeout', () => {
                req.destroy();
                reject(new NetLedgerConnectionError('Request timed out'));
            });

            req.end();
        });
    }

    /**
     * Make an HTTP request.
     */
    private request<T>(method: string, path: string, body?: unknown): Promise<ApiResponse<T>> {
        return new Promise<ApiResponse<T>>((resolve, reject) => {
            const url = new URL(path, this.baseUrl);
            const isHttps = url.protocol === 'https:';
            const lib = isHttps ? https : http;

            const bodyData = body !== null && body !== undefined ? JSON.stringify(body) : undefined;

            const options: http.RequestOptions = {
                method,
                hostname: url.hostname,
                port: url.port || (isHttps ? 443 : 80),
                path: url.pathname + url.search,
                timeout: this.timeoutMs,
                headers: {
                    'Authorization': `Bearer ${this.apiKey}`,
                    'Accept': 'application/json',
                    ...(bodyData ? { 'Content-Type': 'application/json', 'Content-Length': Buffer.byteLength(bodyData) } : {})
                }
            };

            const req = lib.request(options, (res) => {
                let data = '';

                res.on('data', (chunk) => {
                    data += chunk;
                });

                res.on('end', () => {
                    const statusCode = res.statusCode || 0;

                    if (statusCode < 200 || statusCode >= 300) {
                        let errorMessage = res.statusMessage || 'Unknown error';
                        let errorDetails: string | undefined;

                        if (data) {
                            try {
                                const errorResponse: ErrorResponse = JSON.parse(data);
                                errorMessage = errorResponse.Message || errorMessage;
                                errorDetails = errorResponse.Description;
                            } catch {
                                // Failed to parse error response
                            }
                        }

                        reject(new NetLedgerApiError(statusCode, errorMessage, errorDetails));
                        return;
                    }

                    if (!data || data.trim() === '') {
                        resolve({ StatusCode: statusCode } as ApiResponse<T>);
                        return;
                    }

                    try {
                        const parsedData = JSON.parse(data);
                        // Server sends data directly, wrap it in ApiResponse format
                        const response: ApiResponse<T> = {
                            StatusCode: statusCode,
                            Data: parsedData as T
                        };
                        resolve(response);
                    } catch (err) {
                        reject(new NetLedgerApiError(statusCode, 'Failed to parse response'));
                    }
                });
            });

            req.on('error', (err) => {
                reject(new NetLedgerConnectionError('Failed to connect to the server', err));
            });

            req.on('timeout', () => {
                req.destroy();
                reject(new NetLedgerConnectionError('Request timed out'));
            });

            if (bodyData) {
                req.write(bodyData);
            }
            req.end();
        });
    }
}

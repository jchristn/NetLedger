import { HttpClient } from './http-client';
import { ServiceMethods } from './methods/service';
import { AccountMethods } from './methods/account';
import { EntryMethods } from './methods/entry';
import { BalanceMethods } from './methods/balance';
import { ApiKeyMethods } from './methods/apikey';
import { IdentityMethods } from './methods/identity';
import { RequestHistoryMethods } from './methods/request-history';

// Re-export models and errors
export * from './models';
export * from './errors';

/**
 * Options for configuring the NetLedger client.
 */
export interface NetLedgerClientOptions {
    /** Request timeout in milliseconds. Default: 30000. */
    timeoutMs?: number;
    /** Tenant identifier sent as x-tenant-id. */
    tenantId?: string;
}

/**
 * Client for interacting with the NetLedger Server REST API.
 *
 * @example
 * ```typescript
 * const client = new NetLedgerClient('http://localhost:8080', 'your-api-key');
 *
 * // Check server health
 * const isHealthy = await client.service.healthCheck();
 *
 * // Create an account
 * const account = await client.account.create('My Account');
 *
 * // Add a credit
 * const credit = await client.entry.addCredit(account.guid, 100.00, 'Initial deposit');
 *
 * // Get balance
 * const balance = await client.balance.get(account.guid);
 * ```
 */
export class NetLedgerClient {
    private readonly httpClient: HttpClient;

    /** Service operations including health checks and service information. */
    public readonly service: ServiceMethods;

    /** Account management operations. */
    public readonly account: AccountMethods;

    /** Entry operations including credits and debits. */
    public readonly entry: EntryMethods;

    /** Balance operations including commits and verification. */
    public readonly balance: BalanceMethods;

    /** API key management operations. */
    public readonly apiKey: ApiKeyMethods;

    /** Identity and security administration operations. */
    public readonly identity: IdentityMethods;

    /** Request history operations. */
    public readonly requestHistory: RequestHistoryMethods;

    /** The base URL of the NetLedger server. */
    public readonly baseUrl: string;

    /**
     * Create a new NetLedger client.
     * @param baseUrl The base URL of the NetLedger server (e.g., "http://localhost:8080").
     * @param apiKey The API key for authentication.
     * @param options Optional configuration options.
     * @throws Error if baseUrl or apiKey is empty.
     */
    constructor(baseUrl: string, apiKey: string, options?: NetLedgerClientOptions) {
        if (!baseUrl || baseUrl.trim() === '') {
            throw new Error('Base URL cannot be empty');
        }
        if (!apiKey || apiKey.trim() === '') {
            throw new Error('API key cannot be empty');
        }

        this.baseUrl = baseUrl.replace(/\/$/, '');
        const timeoutMs = options?.timeoutMs || 30000;

        this.httpClient = new HttpClient(this.baseUrl, apiKey, timeoutMs, options?.tenantId);
        this.service = new ServiceMethods(this.httpClient);
        this.account = new AccountMethods(this.httpClient);
        this.entry = new EntryMethods(this.httpClient);
        this.balance = new BalanceMethods(this.httpClient);
        this.apiKey = new ApiKeyMethods(this.httpClient);
        this.identity = new IdentityMethods(this.httpClient);
        this.requestHistory = new RequestHistoryMethods(this.httpClient);
    }

    static async discoverTenants(baseUrl: string, email: string): Promise<import('./models').TenantInfo[]> {
        const client = new HttpClient(baseUrl.replace(/\/$/, ''), '');
        const response = await client.post<import('./models').TenantInfo[]>('/v1/auth/tenants', { Email: email });
        return response.Data || [];
    }

    static async login(baseUrl: string, tenantId: string, email: string, password: string): Promise<NetLedgerClient> {
        const normalizedBaseUrl = baseUrl.replace(/\/$/, '');
        const client = new HttpClient(normalizedBaseUrl, '');
        const response = await client.post<{ Session?: { Token?: string } }>('/v1/auth/login', { TenantId: tenantId, Email: email, Password: password });
        const token = response.Data?.Session?.Token;
        if (!token) throw new Error('Login response did not include a session token');
        return new NetLedgerClient(normalizedBaseUrl, token, { tenantId });
    }
}

// Default export
export default NetLedgerClient;

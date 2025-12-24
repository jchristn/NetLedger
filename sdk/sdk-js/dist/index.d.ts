import { ServiceMethods } from './methods/service';
import { AccountMethods } from './methods/account';
import { EntryMethods } from './methods/entry';
import { BalanceMethods } from './methods/balance';
import { ApiKeyMethods } from './methods/apikey';
export * from './models';
export * from './errors';
/**
 * Options for configuring the NetLedger client.
 */
export interface NetLedgerClientOptions {
    /** Request timeout in milliseconds. Default: 30000. */
    timeoutMs?: number;
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
export declare class NetLedgerClient {
    private readonly httpClient;
    /** Service operations including health checks and service information. */
    readonly service: ServiceMethods;
    /** Account management operations. */
    readonly account: AccountMethods;
    /** Entry operations including credits and debits. */
    readonly entry: EntryMethods;
    /** Balance operations including commits and verification. */
    readonly balance: BalanceMethods;
    /** API key management operations. */
    readonly apiKey: ApiKeyMethods;
    /** The base URL of the NetLedger server. */
    readonly baseUrl: string;
    /**
     * Create a new NetLedger client.
     * @param baseUrl The base URL of the NetLedger server (e.g., "http://localhost:8080").
     * @param apiKey The API key for authentication.
     * @param options Optional configuration options.
     * @throws Error if baseUrl or apiKey is empty.
     */
    constructor(baseUrl: string, apiKey: string, options?: NetLedgerClientOptions);
}
export default NetLedgerClient;
//# sourceMappingURL=index.d.ts.map
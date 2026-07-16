"use strict";
var __createBinding = (this && this.__createBinding) || (Object.create ? (function(o, m, k, k2) {
    if (k2 === undefined) k2 = k;
    var desc = Object.getOwnPropertyDescriptor(m, k);
    if (!desc || ("get" in desc ? !m.__esModule : desc.writable || desc.configurable)) {
      desc = { enumerable: true, get: function() { return m[k]; } };
    }
    Object.defineProperty(o, k2, desc);
}) : (function(o, m, k, k2) {
    if (k2 === undefined) k2 = k;
    o[k2] = m[k];
}));
var __exportStar = (this && this.__exportStar) || function(m, exports) {
    for (var p in m) if (p !== "default" && !Object.prototype.hasOwnProperty.call(exports, p)) __createBinding(exports, m, p);
};
Object.defineProperty(exports, "__esModule", { value: true });
exports.NetLedgerClient = void 0;
const http_client_1 = require("./http-client");
const service_1 = require("./methods/service");
const account_1 = require("./methods/account");
const entry_1 = require("./methods/entry");
const balance_1 = require("./methods/balance");
const apikey_1 = require("./methods/apikey");
const identity_1 = require("./methods/identity");
const request_history_1 = require("./methods/request-history");
// Re-export models and errors
__exportStar(require("./models"), exports);
__exportStar(require("./errors"), exports);
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
 * const credit = await client.entry.addCredit(account.id, 100.00, 'Initial deposit');
 *
 * // Get balance
 * const balance = await client.balance.get(account.id);
 * ```
 */
class NetLedgerClient {
    /**
     * Create a new NetLedger client.
     * @param baseUrl The base URL of the NetLedger server (e.g., "http://localhost:8080").
     * @param apiKey The API key for authentication.
     * @param options Optional configuration options.
     * @throws Error if baseUrl or apiKey is empty.
     */
    constructor(baseUrl, apiKey, options) {
        if (!baseUrl || baseUrl.trim() === '') {
            throw new Error('Base URL cannot be empty');
        }
        if (!apiKey || apiKey.trim() === '') {
            throw new Error('API key cannot be empty');
        }
        this.baseUrl = baseUrl.replace(/\/$/, '');
        const timeoutMs = options?.timeoutMs || 30000;
        this.httpClient = new http_client_1.HttpClient(this.baseUrl, apiKey, timeoutMs, options?.tenantId);
        this.service = new service_1.ServiceMethods(this.httpClient);
        this.account = new account_1.AccountMethods(this.httpClient);
        this.entry = new entry_1.EntryMethods(this.httpClient);
        this.balance = new balance_1.BalanceMethods(this.httpClient);
        this.apiKey = new apikey_1.ApiKeyMethods(this.httpClient);
        this.identity = new identity_1.IdentityMethods(this.httpClient);
        this.requestHistory = new request_history_1.RequestHistoryMethods(this.httpClient);
    }
    static async discoverTenants(baseUrl, email) {
        const client = new http_client_1.HttpClient(baseUrl.replace(/\/$/, ''), '');
        const response = await client.post('/v1/auth/tenants', { Email: email });
        return response.Data || [];
    }
    static async login(baseUrl, tenantId, email, password) {
        const normalizedBaseUrl = baseUrl.replace(/\/$/, '');
        const client = new http_client_1.HttpClient(normalizedBaseUrl, '');
        const response = await client.post('/v1/auth/login', { TenantId: tenantId, Email: email, Password: password });
        const token = response.Data?.Session?.Token;
        if (!token)
            throw new Error('Login response did not include a session token');
        return new NetLedgerClient(normalizedBaseUrl, token, { tenantId });
    }
}
exports.NetLedgerClient = NetLedgerClient;
// Default export
exports.default = NetLedgerClient;
//# sourceMappingURL=index.js.map
import { ApiKeyInfo, ApiKeyEnumerationQuery, EnumerationResult } from '../models';
import { HttpClient } from '../http-client';
/**
 * API key management operations.
 */
export declare class ApiKeyMethods {
    private readonly client;
    constructor(client: HttpClient);
    /**
     * Create a new API key.
     * @param name Display name for the key.
     * @param isAdmin Whether the key has admin privileges.
     * @returns The created API key info (includes the key value).
     */
    create(name: string, isAdmin?: boolean): Promise<ApiKeyInfo>;
    /**
     * Enumerate API keys.
     * @param query Query parameters.
     * @returns Enumeration result (key values not included).
     */
    enumerate(query?: ApiKeyEnumerationQuery): Promise<EnumerationResult<ApiKeyInfo>>;
    /**
     * Revoke (delete) an API key.
     * @param apiKeyGuid The API key GUID.
     */
    revoke(apiKeyGuid: string): Promise<void>;
}
//# sourceMappingURL=apikey.d.ts.map
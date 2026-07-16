import { ApiKeyInfo, ApiKeyEnumerationQuery, CredentialCreateResponse, EnumerationResult } from '../models';
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
     * @returns The created credential and one-time secret key.
     */
    create(name: string, isAdmin?: boolean): Promise<CredentialCreateResponse>;
    /**
     * Enumerate API keys.
     * @param query Query parameters.
     * @returns Enumeration result (key values not included).
     */
    enumerate(query?: ApiKeyEnumerationQuery): Promise<EnumerationResult<ApiKeyInfo>>;
    /**
     * Revoke (delete) an API key.
     * @param apiKeyId The API key identifier.
     */
    revoke(apiKeyId: string): Promise<void>;
}
//# sourceMappingURL=apikey.d.ts.map
import { ApiKeyInfo, ApiKeyEnumerationQuery, CredentialCreateResponse, EnumerationResult } from '../models';
import { HttpClient } from '../http-client';
import { NetLedgerValidationError } from '../errors';

/**
 * API key management operations.
 */
export class ApiKeyMethods {
    private readonly client: HttpClient;

    constructor(client: HttpClient) {
        this.client = client;
    }

    /**
     * Create a new API key.
     * @param name Display name for the key.
     * @param isAdmin Whether the key has admin privileges.
     * @returns The created credential and one-time secret key.
     */
    async create(name: string, isAdmin: boolean = false): Promise<CredentialCreateResponse> {
        if (!name || name.trim() === '') {
            throw new NetLedgerValidationError('API key name cannot be empty', 'name');
        }
        const response = await this.client.put<CredentialCreateResponse>(
            '/v1/credentials',
            { Name: name, IsAdmin: isAdmin }
        );
        if (!response.Data) {
            throw new Error('No data returned from server');
        }
        return response.Data;
    }

    /**
     * Enumerate API keys.
     * @param query Query parameters.
     * @returns Enumeration result (key values not included).
     */
    async enumerate(query?: ApiKeyEnumerationQuery): Promise<EnumerationResult<ApiKeyInfo>> {
        const params = new URLSearchParams();
        if (query) {
            if (query.MaxResults !== undefined) params.append('maxResults', query.MaxResults.toString());
            if (query.Skip !== undefined) params.append('skip', query.Skip.toString());
            if (query.TenantId !== undefined) params.append('tenantId', query.TenantId);
        }
        const queryString = params.toString();
        const path = queryString ? `/v1/credentials?${queryString}` : '/v1/credentials';
        const response = await this.client.get<EnumerationResult<ApiKeyInfo>>(path);
        return response.Data || { TotalRecords: 0, RecordsRemaining: 0, EndOfResults: true };
    }

    /**
     * Revoke (delete) an API key.
     * @param apiKeyGuid The API key GUID.
     */
    async revoke(apiKeyGuid: string): Promise<void> {
        await this.client.delete(`/v1/credentials/${apiKeyGuid}`);
    }
}

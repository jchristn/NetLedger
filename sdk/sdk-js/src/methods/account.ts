import { Account, AccountEnumerationQuery, EnumerationResult } from '../models';
import { HttpClient } from '../http-client';
import { NetLedgerValidationError } from '../errors';

/**
 * Account management operations.
 */
export class AccountMethods {
    private readonly client: HttpClient;

    constructor(client: HttpClient) {
        this.client = client;
    }

    /**
     * Create a new account.
     * @param name Account name.
     * @param notes Optional notes.
     * @returns The created account.
     */
    async create(name: string, notes?: string, labels?: string[], tags?: Record<string, string>): Promise<Account> {
        if (!name || name.trim() === '') {
            throw new NetLedgerValidationError('Account name cannot be empty', 'name');
        }
        const response = await this.client.put<Account>('/v1/accounts', { Name: name, Notes: notes, Labels: labels, Tags: tags });
        if (!response.Data) {
            throw new Error('No data returned from server');
        }
        return response.Data;
    }

    /**
     * Get an account by identifier.
     * @param accountId The account identifier.
     * @returns The account.
     */
    async get(accountId: string): Promise<Account> {
        const response = await this.client.get<Account>(`/v1/accounts/${accountId}`);
        if (!response.Data) {
            throw new Error('No data returned from server');
        }
        return response.Data;
    }

    /**
     * Get an account by name.
     * @param name The account name.
     * @returns The account.
     */
    async getByName(name: string): Promise<Account> {
        if (!name || name.trim() === '') {
            throw new NetLedgerValidationError('Account name cannot be empty', 'name');
        }
        const encodedName = encodeURIComponent(name);
        const response = await this.client.get<Account>(`/v1/accounts/byname/${encodedName}`);
        if (!response.Data) {
            throw new Error('No data returned from server');
        }
        return response.Data;
    }

    /**
     * Check if an account exists.
     * @param accountId The account identifier.
     * @returns True if the account exists.
     */
    async exists(accountId: string): Promise<boolean> {
        return await this.client.head(`/v1/accounts/${accountId}`);
    }

    /**
     * Delete an account.
     * @param accountId The account identifier.
     */
    async delete(accountId: string): Promise<void> {
        await this.client.delete(`/v1/accounts/${accountId}`);
    }

    /**
     * Enumerate accounts with optional filtering and pagination.
     * @param query Query parameters.
     * @returns Enumeration result containing accounts.
     */
    async enumerate(query?: AccountEnumerationQuery): Promise<EnumerationResult<Account>> {
        const params = new URLSearchParams();
        if (query) {
            if (query.MaxResults !== undefined) params.append('maxResults', query.MaxResults.toString());
            if (query.Skip !== undefined) params.append('skip', query.Skip.toString());
            if (query.SearchTerm) params.append('searchTerm', query.SearchTerm);
            if (query.Labels && query.Labels.length > 0) params.append('labels', query.Labels.join(','));
            if (query.Tags && Object.keys(query.Tags).length > 0) {
                params.append('tags', Object.entries(query.Tags).map(([key, value]) => `${key}=${value}`).join(','));
            }
        }
        const queryString = params.toString();
        const path = queryString ? `/v1/accounts?${queryString}` : '/v1/accounts';
        const response = await this.client.get<EnumerationResult<Account>>(path);
        return response.Data || { TotalRecords: 0, RecordsRemaining: 0, EndOfResults: true };
    }
}

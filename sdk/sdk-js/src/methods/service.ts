import { ServiceInfo } from '../models';
import { HttpClient } from '../http-client';

/**
 * Service-level operations.
 */
export class ServiceMethods {
    private readonly client: HttpClient;

    constructor(client: HttpClient) {
        this.client = client;
    }

    /**
     * Check if the server is healthy.
     * @returns True if the server is healthy.
     */
    async healthCheck(): Promise<boolean> {
        try {
            return await this.client.head('/');
        } catch {
            return false;
        }
    }

    /**
     * Get service information.
     * @returns Service information including version and uptime.
     */
    async getInfo(): Promise<ServiceInfo> {
        const response = await this.client.get<ServiceInfo>('/');
        if (!response.Data) {
            throw new Error('No data returned from server');
        }
        return response.Data;
    }
}

import { ServiceInfo } from '../models';
import { HttpClient } from '../http-client';
/**
 * Service-level operations.
 */
export declare class ServiceMethods {
    private readonly client;
    constructor(client: HttpClient);
    /**
     * Check if the server is healthy.
     * @returns True if the server is healthy.
     */
    healthCheck(): Promise<boolean>;
    /**
     * Get service information.
     * @returns Service information including version and uptime.
     */
    getInfo(): Promise<ServiceInfo>;
}
//# sourceMappingURL=service.d.ts.map
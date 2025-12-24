"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.ServiceMethods = void 0;
/**
 * Service-level operations.
 */
class ServiceMethods {
    constructor(client) {
        this.client = client;
    }
    /**
     * Check if the server is healthy.
     * @returns True if the server is healthy.
     */
    async healthCheck() {
        try {
            return await this.client.head('/');
        }
        catch {
            return false;
        }
    }
    /**
     * Get service information.
     * @returns Service information including version and uptime.
     */
    async getInfo() {
        const response = await this.client.get('/');
        if (!response.Data) {
            throw new Error('No data returned from server');
        }
        return response.Data;
    }
}
exports.ServiceMethods = ServiceMethods;
//# sourceMappingURL=service.js.map
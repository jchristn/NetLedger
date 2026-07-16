"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.IdentityMethods = void 0;
/**
 * Identity and security administration operations.
 */
class IdentityMethods {
    constructor(client) {
        this.client = client;
    }
    async permissions() {
        const response = await this.client.get('/v1/me/permissions');
        return response.Data || { Permissions: [] };
    }
    async tenants(maxResults = 100) {
        const response = await this.client.get(`/v1/tenants?maxResults=${maxResults}`);
        return response.Data || { TotalRecords: 0, RecordsRemaining: 0, EndOfResults: true };
    }
    async createTenant(name) {
        const response = await this.client.put('/v1/tenants', { Name: name });
        if (!response.Data)
            throw new Error('No data returned from server');
        return response.Data;
    }
    async users(tenantId, maxResults = 100) {
        const response = await this.client.get(`/v1/tenants/${tenantId}/users?maxResults=${maxResults}`);
        return response.Data || { TotalRecords: 0, RecordsRemaining: 0, EndOfResults: true };
    }
    async createUser(tenantId, user) {
        const response = await this.client.put(`/v1/tenants/${tenantId}/users`, user);
        if (!response.Data)
            throw new Error('No data returned from server');
        return response.Data;
    }
    async sessions(tenantId, maxResults = 100) {
        const response = await this.client.get(`/v1/tenants/${tenantId}/sessions?maxResults=${maxResults}`);
        return response.Data || { TotalRecords: 0, RecordsRemaining: 0, EndOfResults: true };
    }
    async audit(tenantId, maxResults = 100) {
        const response = await this.client.get(`/v1/tenants/${tenantId}/audit?maxResults=${maxResults}`);
        return response.Data || { TotalRecords: 0, RecordsRemaining: 0, EndOfResults: true };
    }
    async roles(tenantId, maxResults = 100) {
        const response = await this.client.get(`/v1/tenants/${tenantId}/roles?maxResults=${maxResults}`);
        return response.Data || { TotalRecords: 0, RecordsRemaining: 0, EndOfResults: true };
    }
    async createRole(tenantId, name) {
        const response = await this.client.put(`/v1/tenants/${tenantId}/roles`, { Name: name });
        if (!response.Data)
            throw new Error('No data returned from server');
        return response.Data;
    }
    async permissionsList(tenantId, maxResults = 100) {
        const response = await this.client.get(`/v1/tenants/${tenantId}/permissions?maxResults=${maxResults}`);
        return response.Data || { TotalRecords: 0, RecordsRemaining: 0, EndOfResults: true };
    }
    async createPermission(tenantId, permission) {
        const response = await this.client.put(`/v1/tenants/${tenantId}/permissions`, permission);
        if (!response.Data)
            throw new Error('No data returned from server');
        return response.Data;
    }
    async assignUserRole(tenantId, userId, assignment) {
        const response = await this.client.put(`/v1/tenants/${tenantId}/users/${userId}/roles`, assignment);
        if (!response.Data)
            throw new Error('No data returned from server');
        return response.Data;
    }
}
exports.IdentityMethods = IdentityMethods;
//# sourceMappingURL=identity.js.map
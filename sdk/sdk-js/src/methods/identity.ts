import { HttpClient } from '../http-client';
import { EnumerationResult, TenantInfo, UserInfo, AuthSessionInfo, AuditRecordInfo, EffectivePermissionsInfo, RoleInfo, PermissionInfo, UserRoleAssignmentInfo } from '../models';

/**
 * Identity and security administration operations.
 */
export class IdentityMethods {
    private readonly client: HttpClient;

    constructor(client: HttpClient) {
        this.client = client;
    }

    async permissions(): Promise<EffectivePermissionsInfo> {
        const response = await this.client.get<EffectivePermissionsInfo>('/v1/me/permissions');
        return response.Data || { Permissions: [] };
    }

    async tenants(maxResults: number = 100): Promise<EnumerationResult<TenantInfo>> {
        const response = await this.client.get<EnumerationResult<TenantInfo>>(`/v1/tenants?maxResults=${maxResults}`);
        return response.Data || { TotalRecords: 0, RecordsRemaining: 0, EndOfResults: true };
    }

    async createTenant(name: string): Promise<TenantInfo> {
        const response = await this.client.put<TenantInfo>('/v1/tenants', { Name: name });
        if (!response.Data) throw new Error('No data returned from server');
        return response.Data;
    }

    async users(tenantId: string, maxResults: number = 100): Promise<EnumerationResult<UserInfo>> {
        const response = await this.client.get<EnumerationResult<UserInfo>>(`/v1/tenants/${tenantId}/users?maxResults=${maxResults}`);
        return response.Data || { TotalRecords: 0, RecordsRemaining: 0, EndOfResults: true };
    }

    async createUser(tenantId: string, user: Partial<UserInfo> & { Password: string }): Promise<UserInfo> {
        const response = await this.client.put<UserInfo>(`/v1/tenants/${tenantId}/users`, user);
        if (!response.Data) throw new Error('No data returned from server');
        return response.Data;
    }

    async sessions(tenantId: string, maxResults: number = 100): Promise<EnumerationResult<AuthSessionInfo>> {
        const response = await this.client.get<EnumerationResult<AuthSessionInfo>>(`/v1/tenants/${tenantId}/sessions?maxResults=${maxResults}`);
        return response.Data || { TotalRecords: 0, RecordsRemaining: 0, EndOfResults: true };
    }

    async audit(tenantId: string, maxResults: number = 100): Promise<EnumerationResult<AuditRecordInfo>> {
        const response = await this.client.get<EnumerationResult<AuditRecordInfo>>(`/v1/tenants/${tenantId}/audit?maxResults=${maxResults}`);
        return response.Data || { TotalRecords: 0, RecordsRemaining: 0, EndOfResults: true };
    }

    async roles(tenantId: string, maxResults: number = 100): Promise<EnumerationResult<RoleInfo>> {
        const response = await this.client.get<EnumerationResult<RoleInfo>>(`/v1/tenants/${tenantId}/roles?maxResults=${maxResults}`);
        return response.Data || { TotalRecords: 0, RecordsRemaining: 0, EndOfResults: true };
    }

    async createRole(tenantId: string, name: string): Promise<RoleInfo> {
        const response = await this.client.put<RoleInfo>(`/v1/tenants/${tenantId}/roles`, { Name: name });
        if (!response.Data) throw new Error('No data returned from server');
        return response.Data;
    }

    async permissionsList(tenantId: string, maxResults: number = 100): Promise<EnumerationResult<PermissionInfo>> {
        const response = await this.client.get<EnumerationResult<PermissionInfo>>(`/v1/tenants/${tenantId}/permissions?maxResults=${maxResults}`);
        return response.Data || { TotalRecords: 0, RecordsRemaining: 0, EndOfResults: true };
    }

    async createPermission(tenantId: string, permission: Partial<PermissionInfo>): Promise<PermissionInfo> {
        const response = await this.client.put<PermissionInfo>(`/v1/tenants/${tenantId}/permissions`, permission);
        if (!response.Data) throw new Error('No data returned from server');
        return response.Data;
    }

    async assignUserRole(tenantId: string, userId: string, assignment: UserRoleAssignmentInfo): Promise<UserRoleAssignmentInfo> {
        const response = await this.client.put<UserRoleAssignmentInfo>(`/v1/tenants/${tenantId}/users/${userId}/roles`, assignment);
        if (!response.Data) throw new Error('No data returned from server');
        return response.Data;
    }
}

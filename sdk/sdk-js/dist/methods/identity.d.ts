import { HttpClient } from '../http-client';
import { EnumerationResult, TenantInfo, UserInfo, AuthSessionInfo, AuditRecordInfo, EffectivePermissionsInfo, RoleInfo, PermissionInfo, UserRoleAssignmentInfo } from '../models';
/**
 * Identity and security administration operations.
 */
export declare class IdentityMethods {
    private readonly client;
    constructor(client: HttpClient);
    permissions(): Promise<EffectivePermissionsInfo>;
    tenants(maxResults?: number): Promise<EnumerationResult<TenantInfo>>;
    createTenant(name: string): Promise<TenantInfo>;
    users(tenantId: string, maxResults?: number): Promise<EnumerationResult<UserInfo>>;
    createUser(tenantId: string, user: Partial<UserInfo> & {
        Password: string;
    }): Promise<UserInfo>;
    sessions(tenantId: string, maxResults?: number): Promise<EnumerationResult<AuthSessionInfo>>;
    audit(tenantId: string, maxResults?: number): Promise<EnumerationResult<AuditRecordInfo>>;
    roles(tenantId: string, maxResults?: number): Promise<EnumerationResult<RoleInfo>>;
    createRole(tenantId: string, name: string): Promise<RoleInfo>;
    permissionsList(tenantId: string, maxResults?: number): Promise<EnumerationResult<PermissionInfo>>;
    createPermission(tenantId: string, permission: Partial<PermissionInfo>): Promise<PermissionInfo>;
    assignUserRole(tenantId: string, userId: string, assignment: UserRoleAssignmentInfo): Promise<UserRoleAssignmentInfo>;
}
//# sourceMappingURL=identity.d.ts.map
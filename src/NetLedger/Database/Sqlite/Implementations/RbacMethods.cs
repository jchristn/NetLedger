namespace NetLedger.Database.Sqlite.Implementations
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Globalization;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using NetLedger.Database.Interfaces;
    using NetLedger.Database.Sqlite.Queries;

    /// <summary>
    /// SQLite RBAC methods.
    /// </summary>
    internal class RbacMethods : IRbacMethods
    {
        private readonly SqliteDatabaseDriver _Driver;

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="driver">SQLite driver.</param>
        internal RbacMethods(SqliteDatabaseDriver driver)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
        }

        /// <inheritdoc />
        public async Task<UserRole> CreateRoleAsync(UserRole role, CancellationToken token = default)
        {
            if (role == null) throw new ArgumentNullException(nameof(role));
            if (role.LastUpdateUtc == default) role.LastUpdateUtc = role.CreatedUtc;

            string query =
                "INSERT OR REPLACE INTO userroles (id, tenantid, name, isbuiltin, active, isprotected, createdutc, lastupdateutc) VALUES (" +
                "'" + Sanitize(role.Id) + "', " +
                "'" + Sanitize(role.TenantId) + "', " +
                "'" + Sanitize(role.Name) + "', " +
                (role.IsBuiltIn ? "1" : "0") + ", " +
                (role.Active ? "1" : "0") + ", " +
                (role.IsProtected ? "1" : "0") + ", " +
                "'" + role.CreatedUtc.ToString(SetupQueries.TimestampFormat) + "', " +
                "'" + role.LastUpdateUtc.ToString(SetupQueries.TimestampFormat) + "');";

            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
            return role;
        }

        /// <inheritdoc />
        public async Task<UserRole?> ReadRoleAsync(string? tenantId, string roleId, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(roleId)) throw new ArgumentNullException(nameof(roleId));
            string query = "SELECT * FROM userroles WHERE id = '" + Sanitize(roleId) + "'";
            if (tenantId != null) query += " AND (tenantid = '" + Sanitize(tenantId) + "' OR tenantid = '')";
            query += " LIMIT 1;";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            if (result == null || result.Rows.Count == 0) return null;
            return DataRowToRole(result.Rows[0]);
        }

        /// <inheritdoc />
        public async Task<UserRole?> ReadRoleByNameAsync(string? tenantId, string name, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));
            string query =
                "SELECT * FROM userroles WHERE name = '" + Sanitize(name) + "' AND " +
                "(tenantid = '" + Sanitize(tenantId ?? String.Empty) + "' OR tenantid = '') " +
                "ORDER BY CASE WHEN tenantid = '" + Sanitize(tenantId ?? String.Empty) + "' THEN 0 ELSE 1 END LIMIT 1;";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            if (result == null || result.Rows.Count == 0) return null;
            return DataRowToRole(result.Rows[0]);
        }

        /// <inheritdoc />
        public async Task<EnumerationResult<UserRole>> EnumerateRolesAsync(EnumerationQuery query, CancellationToken token = default)
        {
            EnumerationResult<UserRole> result = new EnumerationResult<UserRole>();
            string where = BuildTenantWhere(query?.TenantId);
            DataTable data = await _Driver.ExecuteQueryAsync("SELECT * FROM userroles" + where + " ORDER BY name ASC;", false, token).ConfigureAwait(false);
            if (data != null)
            {
                foreach (DataRow row in data.Rows)
                {
                    result.Objects.Add(DataRowToRole(row));
                }
            }

            result.TotalRecords = result.Objects.Count;
            result.EndOfResults = true;
            return result;
        }

        /// <inheritdoc />
        public async Task<Permission> CreatePermissionAsync(Permission permission, CancellationToken token = default)
        {
            if (permission == null) throw new ArgumentNullException(nameof(permission));
            if (permission.LastUpdateUtc == default) permission.LastUpdateUtc = permission.CreatedUtc;
            string query =
                "INSERT OR REPLACE INTO permissions (id, tenantid, name, resourcetypes, operationtypes, permissiontype, active, isprotected, createdutc, lastupdateutc) VALUES (" +
                "'" + Sanitize(permission.Id) + "', " +
                "'" + Sanitize(permission.TenantId) + "', " +
                "'" + Sanitize(permission.Name) + "', " +
                "'" + Sanitize(JsonSerializer.Serialize(permission.ResourceTypes)) + "', " +
                "'" + Sanitize(JsonSerializer.Serialize(permission.OperationTypes)) + "', " +
                "'" + Sanitize(permission.PermissionType) + "', " +
                (permission.Active ? "1" : "0") + ", " +
                (permission.IsProtected ? "1" : "0") + ", " +
                "'" + permission.CreatedUtc.ToString(SetupQueries.TimestampFormat) + "', " +
                "'" + permission.LastUpdateUtc.ToString(SetupQueries.TimestampFormat) + "');";
            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
            return permission;
        }

        /// <inheritdoc />
        public async Task<Permission?> ReadPermissionAsync(string? tenantId, string permissionId, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(permissionId)) throw new ArgumentNullException(nameof(permissionId));
            string query = "SELECT * FROM permissions WHERE id = '" + Sanitize(permissionId) + "'";
            if (tenantId != null) query += " AND (tenantid = '" + Sanitize(tenantId) + "' OR tenantid = '')";
            query += " LIMIT 1;";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            if (result == null || result.Rows.Count == 0) return null;
            return DataRowToPermission(result.Rows[0]);
        }

        /// <inheritdoc />
        public async Task<EnumerationResult<Permission>> EnumeratePermissionsAsync(EnumerationQuery query, CancellationToken token = default)
        {
            EnumerationResult<Permission> result = new EnumerationResult<Permission>();
            string where = BuildTenantWhere(query?.TenantId);
            DataTable data = await _Driver.ExecuteQueryAsync("SELECT * FROM permissions" + where + " ORDER BY name ASC;", false, token).ConfigureAwait(false);
            if (data != null)
            {
                foreach (DataRow row in data.Rows)
                {
                    result.Objects.Add(DataRowToPermission(row));
                }
            }

            result.TotalRecords = result.Objects.Count;
            result.EndOfResults = true;
            return result;
        }

        /// <inheritdoc />
        public async Task<RolePermissionMap> CreateRolePermissionMapAsync(RolePermissionMap map, CancellationToken token = default)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            string query =
                "INSERT OR REPLACE INTO rolepermissionmaps (id, tenantid, roleid, permissionid, active, isprotected, createdutc) VALUES (" +
                "'" + Sanitize(map.Id) + "', '" + Sanitize(map.TenantId) + "', '" + Sanitize(map.RoleId) + "', '" + Sanitize(map.PermissionId) + "', " +
                (map.Active ? "1" : "0") + ", " + (map.IsProtected ? "1" : "0") + ", '" + map.CreatedUtc.ToString(SetupQueries.TimestampFormat) + "');";
            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
            return map;
        }

        /// <inheritdoc />
        public async Task<List<RolePermissionMap>> EnumerateRolePermissionMapsAsync(string? tenantId, string roleId, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(roleId)) throw new ArgumentNullException(nameof(roleId));
            string query = "SELECT * FROM rolepermissionmaps WHERE roleid = '" + Sanitize(roleId) + "' AND active = 1";
            if (tenantId != null) query += " AND (tenantid = '" + Sanitize(tenantId) + "' OR tenantid = '')";
            query += ";";
            DataTable data = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            List<RolePermissionMap> result = new List<RolePermissionMap>();
            if (data != null)
            {
                foreach (DataRow row in data.Rows)
                {
                    result.Add(DataRowToRolePermissionMap(row));
                }
            }

            return result;
        }

        /// <inheritdoc />
        public async Task<UserRoleAssignment> CreateUserRoleAssignmentAsync(UserRoleAssignment assignment, CancellationToken token = default)
        {
            if (assignment == null) throw new ArgumentNullException(nameof(assignment));
            if (assignment.LastUpdateUtc == default) assignment.LastUpdateUtc = assignment.CreatedUtc;
            string query =
                "INSERT OR REPLACE INTO userroleassignments (id, tenantid, userid, roleid, rolename, resourcescope, resourceid, inheritstochildren, active, isprotected, createdutc, lastupdateutc) VALUES (" +
                "'" + Sanitize(assignment.Id) + "', '" + Sanitize(assignment.TenantId) + "', '" + Sanitize(assignment.UserId) + "', " +
                NullableSql(assignment.RoleId) + ", " + NullableSql(assignment.RoleName) + ", '" + Sanitize(assignment.ResourceScope) + "', " + NullableSql(assignment.ResourceId) + ", " +
                (assignment.InheritsToChildren ? "1" : "0") + ", " + (assignment.Active ? "1" : "0") + ", " + (assignment.IsProtected ? "1" : "0") + ", " +
                "'" + assignment.CreatedUtc.ToString(SetupQueries.TimestampFormat) + "', '" + assignment.LastUpdateUtc.ToString(SetupQueries.TimestampFormat) + "');";
            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
            return assignment;
        }

        /// <inheritdoc />
        public async Task<List<UserRoleAssignment>> EnumerateUserRoleAssignmentsAsync(string tenantId, string userId, CancellationToken token = default)
        {
            string query = "SELECT * FROM userroleassignments WHERE tenantid = '" + Sanitize(tenantId) + "' AND userid = '" + Sanitize(userId) + "' AND active = 1;";
            DataTable data = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            List<UserRoleAssignment> result = new List<UserRoleAssignment>();
            if (data != null)
            {
                foreach (DataRow row in data.Rows)
                {
                    result.Add(DataRowToUserRoleAssignment(row));
                }
            }

            return result;
        }

        /// <inheritdoc />
        public async Task<CredentialScopeAssignment> CreateCredentialScopeAssignmentAsync(CredentialScopeAssignment assignment, CancellationToken token = default)
        {
            if (assignment == null) throw new ArgumentNullException(nameof(assignment));
            if (assignment.LastUpdateUtc == default) assignment.LastUpdateUtc = assignment.CreatedUtc;
            string query =
                "INSERT OR REPLACE INTO credentialscopeassignments (id, tenantid, credentialid, roleid, rolename, resourcescope, resourceid, operationtypes, resourcetypes, active, isprotected, createdutc, lastupdateutc) VALUES (" +
                "'" + Sanitize(assignment.Id) + "', '" + Sanitize(assignment.TenantId) + "', '" + Sanitize(assignment.CredentialId) + "', " +
                NullableSql(assignment.RoleId) + ", " + NullableSql(assignment.RoleName) + ", '" + Sanitize(assignment.ResourceScope) + "', " + NullableSql(assignment.ResourceId) + ", " +
                "'" + Sanitize(JsonSerializer.Serialize(assignment.OperationTypes)) + "', '" + Sanitize(JsonSerializer.Serialize(assignment.ResourceTypes)) + "', " +
                (assignment.Active ? "1" : "0") + ", " + (assignment.IsProtected ? "1" : "0") + ", " +
                "'" + assignment.CreatedUtc.ToString(SetupQueries.TimestampFormat) + "', '" + assignment.LastUpdateUtc.ToString(SetupQueries.TimestampFormat) + "');";
            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
            return assignment;
        }

        /// <inheritdoc />
        public async Task<List<CredentialScopeAssignment>> EnumerateCredentialScopeAssignmentsAsync(string tenantId, string credentialId, CancellationToken token = default)
        {
            string query = "SELECT * FROM credentialscopeassignments WHERE tenantid = '" + Sanitize(tenantId) + "' AND credentialid = '" + Sanitize(credentialId) + "' AND active = 1;";
            DataTable data = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            List<CredentialScopeAssignment> result = new List<CredentialScopeAssignment>();
            if (data != null)
            {
                foreach (DataRow row in data.Rows)
                {
                    result.Add(DataRowToCredentialScopeAssignment(row));
                }
            }

            return result;
        }

        /// <inheritdoc />
        public async Task SeedBuiltInsAsync(CancellationToken token = default)
        {
            await SeedRoleAsync("TenantAdmin", new List<string> { "All" }, new List<string> { "All" }, token).ConfigureAwait(false);
            await SeedRoleAsync("SecurityAdmin", new List<string> { "User", "Credential", "Session", "Role", "Permission", "Assignment", "Audit", "Tenant" }, new List<string> { "Admin", "Read", "Create", "Update", "Delete" }, token).ConfigureAwait(false);
            await SeedRoleAsync("Auditor", new List<string> { "User", "Credential", "Session", "Role", "Permission", "Assignment", "Audit", "Tenant" }, new List<string> { "Read" }, token).ConfigureAwait(false);
            await SeedRoleAsync("ResourceAdmin", new List<string> { "Account", "Entry", "Balance" }, new List<string> { "All" }, token).ConfigureAwait(false);
            await SeedRoleAsync("Editor", new List<string> { "Account", "Entry", "Balance" }, new List<string> { "Read", "Write", "Create", "Update", "Delete", "Execute" }, token).ConfigureAwait(false);
            await SeedRoleAsync("Viewer", new List<string> { "Account", "Entry", "Balance" }, new List<string> { "Read" }, token).ConfigureAwait(false);
            await SeedRoleAsync("TenantMember", new List<string> { "User" }, new List<string> { "Read" }, token).ConfigureAwait(false);
            await CreateRoleAsync(new UserRole { Id = BuiltInId(IdentifierPrefixes.Role, "Custom"), Name = "Custom", IsBuiltIn = true, IsProtected = true }, token).ConfigureAwait(false);
        }

        private async Task SeedRoleAsync(string name, List<string> resourceTypes, List<string> operationTypes, CancellationToken token)
        {
            UserRole role = new UserRole
            {
                Id = BuiltInId(IdentifierPrefixes.Role, name),
                Name = name,
                IsBuiltIn = true,
                IsProtected = true
            };
            Permission permission = new Permission
            {
                Id = BuiltInId(IdentifierPrefixes.Permission, name),
                Name = name + " baseline",
                ResourceTypes = resourceTypes,
                OperationTypes = operationTypes,
                PermissionType = "Permit",
                IsProtected = true
            };
            await CreateRoleAsync(role, token).ConfigureAwait(false);
            await CreatePermissionAsync(permission, token).ConfigureAwait(false);
            await CreateRolePermissionMapAsync(new RolePermissionMap
            {
                Id = BuiltInId(IdentifierPrefixes.Assignment, name),
                RoleId = role.Id,
                PermissionId = permission.Id,
                IsProtected = true
            }, token).ConfigureAwait(false);
        }

        private string BuiltInId(string prefix, string name)
        {
            string suffix = "builtin" + name.ToLowerInvariant().Replace(" ", String.Empty).Replace("_", String.Empty);
            string id = prefix + suffix;
            if (id.Length > NetLedgerId.Length) return id.Substring(0, NetLedgerId.Length);
            return id.PadRight(NetLedgerId.Length, '0');
        }

        private string BuildTenantWhere(string? tenantId)
        {
            if (tenantId == null) return String.Empty;
            return " WHERE tenantid = '" + Sanitize(tenantId) + "' OR tenantid = ''";
        }

        private string NullableSql(string? value)
        {
            return String.IsNullOrEmpty(value) ? "NULL" : "'" + Sanitize(value) + "'";
        }

        private string Sanitize(string input)
        {
            if (String.IsNullOrEmpty(input)) return String.Empty;
            return input.Replace("'", "''");
        }

        private UserRole DataRowToRole(DataRow row)
        {
            return new UserRole
            {
                Id = GetString(row, "id"),
                TenantId = GetString(row, "tenantid"),
                Name = GetString(row, "name"),
                IsBuiltIn = GetString(row, "isbuiltin") == "1",
                Active = GetString(row, "active") != "0",
                IsProtected = GetString(row, "isprotected") == "1",
                CreatedUtc = ParseTimestamp(GetString(row, "createdutc")),
                LastUpdateUtc = ParseTimestamp(GetString(row, "lastupdateutc"))
            };
        }

        private Permission DataRowToPermission(DataRow row)
        {
            return new Permission
            {
                Id = GetString(row, "id"),
                TenantId = GetString(row, "tenantid"),
                Name = GetString(row, "name"),
                ResourceTypes = DeserializeList(GetString(row, "resourcetypes")),
                OperationTypes = DeserializeList(GetString(row, "operationtypes")),
                PermissionType = GetString(row, "permissiontype"),
                Active = GetString(row, "active") != "0",
                IsProtected = GetString(row, "isprotected") == "1",
                CreatedUtc = ParseTimestamp(GetString(row, "createdutc")),
                LastUpdateUtc = ParseTimestamp(GetString(row, "lastupdateutc"))
            };
        }

        private RolePermissionMap DataRowToRolePermissionMap(DataRow row)
        {
            return new RolePermissionMap
            {
                Id = GetString(row, "id"),
                TenantId = GetString(row, "tenantid"),
                RoleId = GetString(row, "roleid"),
                PermissionId = GetString(row, "permissionid"),
                Active = GetString(row, "active") != "0",
                IsProtected = GetString(row, "isprotected") == "1",
                CreatedUtc = ParseTimestamp(GetString(row, "createdutc"))
            };
        }

        private UserRoleAssignment DataRowToUserRoleAssignment(DataRow row)
        {
            return new UserRoleAssignment
            {
                Id = GetString(row, "id"),
                TenantId = GetString(row, "tenantid"),
                UserId = GetString(row, "userid"),
                RoleId = GetNullableString(row, "roleid"),
                RoleName = GetNullableString(row, "rolename"),
                ResourceScope = GetString(row, "resourcescope"),
                ResourceId = GetNullableString(row, "resourceid"),
                InheritsToChildren = GetString(row, "inheritstochildren") != "0",
                Active = GetString(row, "active") != "0",
                IsProtected = GetString(row, "isprotected") == "1",
                CreatedUtc = ParseTimestamp(GetString(row, "createdutc")),
                LastUpdateUtc = ParseTimestamp(GetString(row, "lastupdateutc"))
            };
        }

        private CredentialScopeAssignment DataRowToCredentialScopeAssignment(DataRow row)
        {
            return new CredentialScopeAssignment
            {
                Id = GetString(row, "id"),
                TenantId = GetString(row, "tenantid"),
                CredentialId = GetString(row, "credentialid"),
                RoleId = GetNullableString(row, "roleid"),
                RoleName = GetNullableString(row, "rolename"),
                ResourceScope = GetString(row, "resourcescope"),
                ResourceId = GetNullableString(row, "resourceid"),
                OperationTypes = DeserializeList(GetString(row, "operationtypes")),
                ResourceTypes = DeserializeList(GetString(row, "resourcetypes")),
                Active = GetString(row, "active") != "0",
                IsProtected = GetString(row, "isprotected") == "1",
                CreatedUtc = ParseTimestamp(GetString(row, "createdutc")),
                LastUpdateUtc = ParseTimestamp(GetString(row, "lastupdateutc"))
            };
        }

        private List<string> DeserializeList(string json)
        {
            if (String.IsNullOrEmpty(json)) return new List<string>();
            return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        }

        private DateTime ParseTimestamp(string timestamp)
        {
            if (String.IsNullOrEmpty(timestamp)) return DateTime.UtcNow;
            return DateTime.Parse(timestamp, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
        }

        private string GetString(DataRow row, string columnName)
        {
            if (!row.Table.Columns.Contains(columnName)) return String.Empty;
            return row[columnName]?.ToString() ?? String.Empty;
        }

        private string? GetNullableString(DataRow row, string columnName)
        {
            string value = GetString(row, columnName);
            return String.IsNullOrEmpty(value) ? null : value;
        }
    }
}

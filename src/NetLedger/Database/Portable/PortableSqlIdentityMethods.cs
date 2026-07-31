namespace NetLedger.Database.Portable
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Globalization;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using NetLedger.Database.Interfaces;

    internal sealed class PortableSqlTenantMethods : ITenantMethods
    {
        private readonly PortableSqlDialect _Sql;

        internal PortableSqlTenantMethods(DatabaseDriverBase driver, DatabaseTypeEnum databaseType)
        {
            _Sql = new PortableSqlDialect(driver, databaseType);
        }

        public async Task<Tenant> CreateAsync(Tenant tenant, CancellationToken token = default)
        {
            if (tenant == null) throw new ArgumentNullException(nameof(tenant));
            if (String.IsNullOrEmpty(tenant.Id)) tenant.Id = NetLedgerId.Generate(IdentifierPrefixes.Tenant);
            tenant.CreatedUtc = tenant.CreatedUtc == DateTime.MinValue ? DateTime.UtcNow : tenant.CreatedUtc;
            tenant.LastUpdateUtc = DateTime.UtcNow;

            await _Sql.ExecuteAsync(
                "DELETE FROM " + _Sql.Table("tenants") + " WHERE " + _Sql.Column("id") + " = '" + _Sql.Sanitize(tenant.Id) + "';" +
                "INSERT INTO " + _Sql.Table("tenants") + " (" + _Sql.Columns("id", "parentid", "name", "region", "active", "isprotected", "createdutc", "lastupdateutc") + ") VALUES (" +
                "'" + _Sql.Sanitize(tenant.Id) + "', " +
                _Sql.Nullable(tenant.ParentId) + ", " +
                "'" + _Sql.Sanitize(tenant.Name) + "', " +
                _Sql.Nullable(tenant.Region) + ", " +
                _Sql.Bool(tenant.Active) + ", " +
                _Sql.Bool(tenant.IsProtected) + ", " +
                "'" + _Sql.Timestamp(tenant.CreatedUtc) + "', " +
                "'" + _Sql.Timestamp(tenant.LastUpdateUtc) + "');",
                true,
                token).ConfigureAwait(false);

            return tenant;
        }

        public async Task<Tenant?> ReadAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));
            DataTable result = await _Sql.QueryOneAsync("tenants", "id = '" + _Sql.Sanitize(id) + "'", token).ConfigureAwait(false);
            return result.Rows.Count == 0 ? null : ToTenant(result.Rows[0]);
        }

        public async Task<EnumerationResult<Tenant>> EnumerateAsync(EnumerationQuery query, CancellationToken token = default)
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            string where = String.IsNullOrEmpty(query.SearchTerm) ? String.Empty : " WHERE " + _Sql.Column("name") + " LIKE '%" + _Sql.Sanitize(query.SearchTerm) + "%'";
            return await _Sql.EnumerateAsync("tenants", where, query, ToTenant, _Sql.Column("createdutc") + " DESC", token).ConfigureAwait(false);
        }

        public async Task<Tenant> UpdateAsync(Tenant tenant, CancellationToken token = default)
        {
            if (tenant == null) throw new ArgumentNullException(nameof(tenant));
            tenant.LastUpdateUtc = DateTime.UtcNow;
            await _Sql.ExecuteAsync(
                "UPDATE " + _Sql.Table("tenants") + " SET " +
                _Sql.Column("parentid") + " = " + _Sql.Nullable(tenant.ParentId) + ", " +
                _Sql.Column("name") + " = '" + _Sql.Sanitize(tenant.Name) + "', " +
                _Sql.Column("region") + " = " + _Sql.Nullable(tenant.Region) + ", " +
                _Sql.Column("active") + " = " + _Sql.Bool(tenant.Active) + ", " +
                _Sql.Column("isprotected") + " = " + _Sql.Bool(tenant.IsProtected) + ", " +
                _Sql.Column("lastupdateutc") + " = '" + _Sql.Timestamp(tenant.LastUpdateUtc) + "' " +
                "WHERE " + _Sql.Column("id") + " = '" + _Sql.Sanitize(tenant.Id) + "';",
                true,
                token).ConfigureAwait(false);
            return tenant;
        }

        public async Task<bool> DeleteAsync(string id, CancellationToken token = default)
        {
            Tenant? tenant = await ReadAsync(id, token).ConfigureAwait(false);
            if (tenant == null) return false;
            if (tenant.IsProtected) throw new InvalidOperationException("Tenant '" + id + "' is protected and cannot be deleted.");
            await _Sql.ExecuteAsync("DELETE FROM " + _Sql.Table("tenants") + " WHERE " + _Sql.Column("id") + " = '" + _Sql.Sanitize(id) + "';", true, token).ConfigureAwait(false);
            return true;
        }

        private Tenant ToTenant(DataRow row)
        {
            return new Tenant
            {
                Id = _Sql.Get(row, "id"),
                ParentId = _Sql.GetNull(row, "parentid"),
                Name = _Sql.Get(row, "name"),
                Region = _Sql.GetNull(row, "region"),
                Active = _Sql.GetBool(row, "active"),
                IsProtected = _Sql.GetBool(row, "isprotected"),
                CreatedUtc = _Sql.GetDate(row, "createdutc"),
                LastUpdateUtc = _Sql.GetDate(row, "lastupdateutc")
            };
        }
    }

    internal sealed class PortableSqlUserMethods : IUserMethods
    {
        private readonly PortableSqlDialect _Sql;

        internal PortableSqlUserMethods(DatabaseDriverBase driver, DatabaseTypeEnum databaseType)
        {
            _Sql = new PortableSqlDialect(driver, databaseType);
        }

        public async Task<User> CreateAsync(User user, CancellationToken token = default)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));
            if (String.IsNullOrEmpty(user.Id)) user.Id = NetLedgerId.Generate(IdentifierPrefixes.User);
            user.Email = user.Email.Trim().ToLowerInvariant();
            user.CreatedUtc = user.CreatedUtc == DateTime.MinValue ? DateTime.UtcNow : user.CreatedUtc;
            user.LastUpdateUtc = DateTime.UtcNow;

            await _Sql.ExecuteAsync(
                "DELETE FROM " + _Sql.Table("users") + " WHERE " + _Sql.Column("id") + " = '" + _Sql.Sanitize(user.Id) + "';" +
                "INSERT INTO " + _Sql.Table("users") + " (" + _Sql.Columns("id", "tenantid", "firstname", "lastname", "email", "passwordsha256", "isadmin", "istenantadmin", "active", "isprotected", "createdutc", "lastupdateutc") + ") VALUES (" +
                "'" + _Sql.Sanitize(user.Id) + "', " +
                "'" + _Sql.Sanitize(user.TenantId) + "', " +
                _Sql.Nullable(user.FirstName) + ", " +
                _Sql.Nullable(user.LastName) + ", " +
                "'" + _Sql.Sanitize(user.Email) + "', " +
                "'" + _Sql.Sanitize(user.PasswordSha256) + "', " +
                _Sql.Bool(user.IsAdmin) + ", " +
                _Sql.Bool(user.IsTenantAdmin) + ", " +
                _Sql.Bool(user.Active) + ", " +
                _Sql.Bool(user.IsProtected) + ", " +
                "'" + _Sql.Timestamp(user.CreatedUtc) + "', " +
                "'" + _Sql.Timestamp(user.LastUpdateUtc) + "');",
                true,
                token).ConfigureAwait(false);
            return user;
        }

        public async Task<User?> ReadAsync(string tenantId, string id, CancellationToken token = default)
        {
            DataTable result = await _Sql.QueryOneAsync("users", "tenantid = '" + _Sql.Sanitize(tenantId) + "' AND id = '" + _Sql.Sanitize(id) + "'", token).ConfigureAwait(false);
            return result.Rows.Count == 0 ? null : ToUser(result.Rows[0]);
        }

        public async Task<User?> ReadByEmailAsync(string tenantId, string email, CancellationToken token = default)
        {
            DataTable result = await _Sql.QueryOneAsync("users", "tenantid = '" + _Sql.Sanitize(tenantId) + "' AND email = '" + _Sql.Sanitize(email.Trim().ToLowerInvariant()) + "'", token).ConfigureAwait(false);
            return result.Rows.Count == 0 ? null : ToUser(result.Rows[0]);
        }

        public async Task<EnumerationResult<User>> EnumerateAsync(EnumerationQuery query, CancellationToken token = default)
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            StringBuilder where = new StringBuilder();
            string prefix = " WHERE ";
            if (!String.IsNullOrEmpty(query.TenantId))
            {
                where.Append(prefix).Append(_Sql.Column("tenantid")).Append(" = '").Append(_Sql.Sanitize(query.TenantId)).Append("'");
                prefix = " AND ";
            }
            if (!String.IsNullOrEmpty(query.SearchTerm))
            {
                string term = _Sql.Sanitize(query.SearchTerm);
                where.Append(prefix).Append("(").Append(_Sql.Column("email")).Append(" LIKE '%").Append(term).Append("%' OR ")
                    .Append(_Sql.Column("firstname")).Append(" LIKE '%").Append(term).Append("%' OR ")
                    .Append(_Sql.Column("lastname")).Append(" LIKE '%").Append(term).Append("%')");
            }
            return await _Sql.EnumerateAsync("users", where.ToString(), query, ToUser, _Sql.Column("createdutc") + " DESC", token).ConfigureAwait(false);
        }

        public async Task<User> UpdateAsync(User user, CancellationToken token = default)
        {
            user.Email = user.Email.Trim().ToLowerInvariant();
            user.LastUpdateUtc = DateTime.UtcNow;
            await _Sql.ExecuteAsync(
                "UPDATE " + _Sql.Table("users") + " SET " +
                _Sql.Column("firstname") + " = " + _Sql.Nullable(user.FirstName) + ", " +
                _Sql.Column("lastname") + " = " + _Sql.Nullable(user.LastName) + ", " +
                _Sql.Column("email") + " = '" + _Sql.Sanitize(user.Email) + "', " +
                _Sql.Column("passwordsha256") + " = '" + _Sql.Sanitize(user.PasswordSha256) + "', " +
                _Sql.Column("isadmin") + " = " + _Sql.Bool(user.IsAdmin) + ", " +
                _Sql.Column("istenantadmin") + " = " + _Sql.Bool(user.IsTenantAdmin) + ", " +
                _Sql.Column("active") + " = " + _Sql.Bool(user.Active) + ", " +
                _Sql.Column("isprotected") + " = " + _Sql.Bool(user.IsProtected) + ", " +
                _Sql.Column("lastupdateutc") + " = '" + _Sql.Timestamp(user.LastUpdateUtc) + "' " +
                "WHERE " + _Sql.Column("tenantid") + " = '" + _Sql.Sanitize(user.TenantId) + "' AND " + _Sql.Column("id") + " = '" + _Sql.Sanitize(user.Id) + "';",
                true,
                token).ConfigureAwait(false);
            return user;
        }

        public async Task<bool> DeleteAsync(string tenantId, string id, CancellationToken token = default)
        {
            User? user = await ReadAsync(tenantId, id, token).ConfigureAwait(false);
            if (user == null) return false;
            if (user.IsProtected) throw new InvalidOperationException("User '" + id + "' is protected and cannot be deleted.");
            await _Sql.ExecuteAsync("DELETE FROM " + _Sql.Table("users") + " WHERE " + _Sql.Column("tenantid") + " = '" + _Sql.Sanitize(tenantId) + "' AND " + _Sql.Column("id") + " = '" + _Sql.Sanitize(id) + "';", true, token).ConfigureAwait(false);
            return true;
        }

        private User ToUser(DataRow row)
        {
            return new User
            {
                Id = _Sql.Get(row, "id"),
                TenantId = _Sql.Get(row, "tenantid"),
                FirstName = _Sql.GetNull(row, "firstname"),
                LastName = _Sql.GetNull(row, "lastname"),
                Email = _Sql.Get(row, "email"),
                PasswordSha256 = _Sql.Get(row, "passwordsha256"),
                IsAdmin = _Sql.GetBool(row, "isadmin"),
                IsTenantAdmin = _Sql.GetBool(row, "istenantadmin"),
                Active = _Sql.GetBool(row, "active"),
                IsProtected = _Sql.GetBool(row, "isprotected"),
                CreatedUtc = _Sql.GetDate(row, "createdutc"),
                LastUpdateUtc = _Sql.GetDate(row, "lastupdateutc")
            };
        }
    }

    internal sealed class PortableSqlAuthSessionMethods : IAuthSessionMethods
    {
        private readonly PortableSqlDialect _Sql;

        internal PortableSqlAuthSessionMethods(DatabaseDriverBase driver, DatabaseTypeEnum databaseType)
        {
            _Sql = new PortableSqlDialect(driver, databaseType);
        }

        public async Task<AuthSession> CreateAsync(AuthSession session, CancellationToken token = default)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (String.IsNullOrEmpty(session.Id)) session.Id = NetLedgerId.Generate(IdentifierPrefixes.Session);
            if (String.IsNullOrEmpty(session.Token)) session.Token = NetLedgerId.Generate("tok_");
            session.CreatedUtc = session.CreatedUtc == DateTime.MinValue ? DateTime.UtcNow : session.CreatedUtc;

            await _Sql.ExecuteAsync(
                "DELETE FROM " + _Sql.Table("authsessions") + " WHERE " + _Sql.Column("id") + " = '" + _Sql.Sanitize(session.Id) + "';" +
                "INSERT INTO " + _Sql.Table("authsessions") + " (" + _Sql.Columns("id", "tenantid", "userid", "token", "active", "expiresutc", "revokedutc", "createdutc", "lastupdateutc") + ") VALUES (" +
                "'" + _Sql.Sanitize(session.Id) + "', " +
                "'" + _Sql.Sanitize(session.TenantId) + "', " +
                _Sql.Nullable(session.UserId) + ", " +
                "'" + _Sql.Sanitize(session.Token) + "', " +
                _Sql.Bool(session.Active) + ", " +
                "'" + _Sql.Timestamp(session.ExpiresUtc) + "', NULL, " +
                "'" + _Sql.Timestamp(session.CreatedUtc) + "', " +
                "'" + _Sql.Timestamp(DateTime.UtcNow) + "');",
                true,
                token).ConfigureAwait(false);
            return session;
        }

        public async Task<AuthSession?> ReadByTokenAsync(string tokenValue, CancellationToken token = default)
        {
            DataTable result = await _Sql.QueryOneAsync("authsessions", "token = '" + _Sql.Sanitize(tokenValue) + "'", token).ConfigureAwait(false);
            return result.Rows.Count == 0 ? null : ToSession(result.Rows[0]);
        }

        public async Task<bool> RevokeAsync(string tenantId, string id, string reason, CancellationToken token = default)
        {
            string now = _Sql.Timestamp(DateTime.UtcNow);
            await _Sql.ExecuteAsync(
                "UPDATE " + _Sql.Table("authsessions") + " SET " +
                _Sql.Column("active") + " = " + _Sql.Bool(false) + ", " +
                _Sql.Column("revokedutc") + " = '" + now + "', " +
                _Sql.Column("lastupdateutc") + " = '" + now + "' " +
                "WHERE " + _Sql.Column("tenantid") + " = '" + _Sql.Sanitize(tenantId) + "' AND " + _Sql.Column("id") + " = '" + _Sql.Sanitize(id) + "';",
                true,
                token).ConfigureAwait(false);
            return true;
        }

        public async Task<EnumerationResult<AuthSession>> EnumerateAsync(EnumerationQuery query, CancellationToken token = default)
        {
            string where = String.IsNullOrEmpty(query.TenantId) ? String.Empty : " WHERE " + _Sql.Column("tenantid") + " = '" + _Sql.Sanitize(query.TenantId) + "'";
            return await _Sql.EnumerateAsync("authsessions", where, query, ToSession, _Sql.Column("createdutc") + " DESC", token).ConfigureAwait(false);
        }

        private AuthSession ToSession(DataRow row)
        {
            return new AuthSession
            {
                Id = _Sql.Get(row, "id"),
                TenantId = _Sql.Get(row, "tenantid"),
                UserId = _Sql.GetNull(row, "userid"),
                Token = _Sql.Get(row, "token"),
                Active = _Sql.GetBool(row, "active"),
                ExpiresUtc = _Sql.GetDate(row, "expiresutc"),
                CreatedUtc = _Sql.GetDate(row, "createdutc")
            };
        }
    }

    internal sealed class PortableSqlAccountUserMapMethods : IAccountUserMapMethods
    {
        private readonly PortableSqlDialect _Sql;

        internal PortableSqlAccountUserMapMethods(DatabaseDriverBase driver, DatabaseTypeEnum databaseType)
        {
            _Sql = new PortableSqlDialect(driver, databaseType);
        }

        public async Task<AccountUserMap> CreateAsync(AccountUserMap map, CancellationToken token = default)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (String.IsNullOrEmpty(map.Id)) map.Id = NetLedgerId.Generate(IdentifierPrefixes.Assignment);
            map.CreatedUtc = map.CreatedUtc == DateTime.MinValue ? DateTime.UtcNow : map.CreatedUtc;
            string keyWhere = _Sql.Column("tenantid") + " = '" + _Sql.Sanitize(map.TenantId) + "' AND " + _Sql.Column("accountid") + " = '" + _Sql.Sanitize(map.AccountId) + "' AND " + _Sql.Column("userid") + " = '" + _Sql.Sanitize(map.UserId) + "'";
            await _Sql.ExecuteAsync(
                "DELETE FROM " + _Sql.Table("accountusermaps") + " WHERE " + keyWhere + ";" +
                "INSERT INTO " + _Sql.Table("accountusermaps") + " (" + _Sql.Columns("id", "tenantid", "accountid", "userid", "createdutc") + ") VALUES (" +
                "'" + _Sql.Sanitize(map.Id) + "', '" + _Sql.Sanitize(map.TenantId) + "', '" + _Sql.Sanitize(map.AccountId) + "', '" + _Sql.Sanitize(map.UserId) + "', '" + _Sql.Timestamp(map.CreatedUtc) + "');",
                true,
                token).ConfigureAwait(false);
            return map;
        }

        public async Task<bool> ExistsAsync(string tenantId, string accountId, string userId, CancellationToken token = default)
        {
            long count = await _Sql.CountAsync("accountusermaps", _Sql.Column("tenantid") + " = '" + _Sql.Sanitize(tenantId) + "' AND " + _Sql.Column("accountid") + " = '" + _Sql.Sanitize(accountId) + "' AND " + _Sql.Column("userid") + " = '" + _Sql.Sanitize(userId) + "'", token).ConfigureAwait(false);
            return count > 0;
        }

        public async Task<EnumerationResult<AccountUserMap>> EnumerateAsync(EnumerationQuery query, CancellationToken token = default)
        {
            string where = BuildWhereClause(query);
            return await _Sql.EnumerateAsync("accountusermaps", where, query, ToMap, _Sql.Column("createdutc") + " DESC", token).ConfigureAwait(false);
        }

        public async Task<bool> DeleteAsync(string tenantId, string accountId, string userId, CancellationToken token = default)
        {
            await _Sql.ExecuteAsync("DELETE FROM " + _Sql.Table("accountusermaps") + " WHERE " + _Sql.Column("tenantid") + " = '" + _Sql.Sanitize(tenantId) + "' AND " + _Sql.Column("accountid") + " = '" + _Sql.Sanitize(accountId) + "' AND " + _Sql.Column("userid") + " = '" + _Sql.Sanitize(userId) + "';", true, token).ConfigureAwait(false);
            return true;
        }

        private AccountUserMap ToMap(DataRow row)
        {
            return new AccountUserMap
            {
                Id = _Sql.Get(row, "id"),
                TenantId = _Sql.Get(row, "tenantid"),
                AccountId = _Sql.Get(row, "accountid"),
                UserId = _Sql.Get(row, "userid"),
                CreatedUtc = _Sql.GetDate(row, "createdutc")
            };
        }

        private string BuildWhereClause(EnumerationQuery query)
        {
            List<string> conditions = new List<string>();
            if (!String.IsNullOrEmpty(query.TenantId))
            {
                conditions.Add(_Sql.Column("tenantid") + " = '" + _Sql.Sanitize(query.TenantId) + "'");
            }

            if (!String.IsNullOrEmpty(query.UserId))
            {
                conditions.Add(_Sql.Column("userid") + " = '" + _Sql.Sanitize(query.UserId) + "'");
            }

            return conditions.Count == 0 ? String.Empty : " WHERE " + String.Join(" AND ", conditions);
        }
    }

    internal sealed class PortableSqlAuditRecordMethods : IAuditRecordMethods
    {
        private readonly PortableSqlDialect _Sql;

        internal PortableSqlAuditRecordMethods(DatabaseDriverBase driver, DatabaseTypeEnum databaseType)
        {
            _Sql = new PortableSqlDialect(driver, databaseType);
        }

        public async Task<AuditRecord> CreateAsync(AuditRecord record, CancellationToken token = default)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            if (String.IsNullOrEmpty(record.Id)) record.Id = NetLedgerId.Generate(IdentifierPrefixes.Audit);
            record.CreatedUtc = record.CreatedUtc == DateTime.MinValue ? DateTime.UtcNow : record.CreatedUtc;
            await _Sql.ExecuteAsync(
                "INSERT INTO " + _Sql.Table("auditrecords") + " (" + _Sql.Columns("id", "tenantid", "principalid", "principaltype", "eventtype", "resourcetype", "operationtype", "resourceid", "result", "reason", "requestid", "createdutc") + ") VALUES (" +
                "'" + _Sql.Sanitize(record.Id) + "', " + _Sql.Nullable(record.TenantId) + ", " + _Sql.Nullable(record.PrincipalId) + ", " + _Sql.Nullable(record.PrincipalType) + ", " +
                "'" + _Sql.Sanitize(record.EventType) + "', " + _Sql.Nullable(record.ResourceType) + ", " + _Sql.Nullable(record.OperationType) + ", " + _Sql.Nullable(record.ResourceId) + ", " +
                "'" + _Sql.Sanitize(record.Result) + "', " + _Sql.Nullable(record.Reason) + ", " + _Sql.Nullable(record.RequestId) + ", '" + _Sql.Timestamp(record.CreatedUtc) + "');",
                true,
                token).ConfigureAwait(false);
            return record;
        }

        public async Task<EnumerationResult<AuditRecord>> EnumerateAsync(EnumerationQuery query, CancellationToken token = default)
        {
            string where = String.IsNullOrEmpty(query.TenantId) ? String.Empty : " WHERE " + _Sql.Column("tenantid") + " = '" + _Sql.Sanitize(query.TenantId) + "'";
            return await _Sql.EnumerateAsync("auditrecords", where, query, ToRecord, _Sql.Column("createdutc") + " DESC", token).ConfigureAwait(false);
        }

        private AuditRecord ToRecord(DataRow row)
        {
            return new AuditRecord
            {
                Id = _Sql.Get(row, "id"),
                TenantId = _Sql.Get(row, "tenantid"),
                PrincipalId = _Sql.GetNull(row, "principalid"),
                PrincipalType = _Sql.GetNull(row, "principaltype"),
                EventType = _Sql.Get(row, "eventtype"),
                ResourceType = _Sql.GetNull(row, "resourcetype"),
                OperationType = _Sql.GetNull(row, "operationtype"),
                ResourceId = _Sql.GetNull(row, "resourceid"),
                Result = _Sql.Get(row, "result"),
                Reason = _Sql.GetNull(row, "reason"),
                RequestId = _Sql.GetNull(row, "requestid"),
                CreatedUtc = _Sql.GetDate(row, "createdutc")
            };
        }
    }

    internal sealed class PortableSqlRbacMethods : IRbacMethods
    {
        private readonly PortableSqlDialect _Sql;

        internal PortableSqlRbacMethods(DatabaseDriverBase driver, DatabaseTypeEnum databaseType)
        {
            _Sql = new PortableSqlDialect(driver, databaseType);
        }

        public async Task<UserRole> CreateRoleAsync(UserRole role, CancellationToken token = default)
        {
            if (role == null) throw new ArgumentNullException(nameof(role));
            if (role.LastUpdateUtc == default) role.LastUpdateUtc = role.CreatedUtc;
            await ReplaceByIdAsync("userroles", role.Id, _Sql.Columns("id", "tenantid", "name", "isbuiltin", "active", "isprotected", "createdutc", "lastupdateutc"),
                "'" + _Sql.Sanitize(role.Id) + "', '" + _Sql.Sanitize(role.TenantId) + "', '" + _Sql.Sanitize(role.Name) + "', " + _Sql.Bool(role.IsBuiltIn) + ", " + _Sql.Bool(role.Active) + ", " + _Sql.Bool(role.IsProtected) + ", '" + _Sql.Timestamp(role.CreatedUtc) + "', '" + _Sql.Timestamp(role.LastUpdateUtc) + "'", token).ConfigureAwait(false);
            return role;
        }

        public async Task<UserRole?> ReadRoleAsync(string? tenantId, string roleId, CancellationToken token = default)
        {
            string where = "id = '" + _Sql.Sanitize(roleId) + "'";
            if (tenantId != null) where += " AND (tenantid = '" + _Sql.Sanitize(tenantId) + "' OR tenantid = '')";
            DataTable result = await _Sql.QueryOneAsync("userroles", where, token).ConfigureAwait(false);
            return result.Rows.Count == 0 ? null : ToRole(result.Rows[0]);
        }

        public async Task<UserRole?> ReadRoleByNameAsync(string? tenantId, string name, CancellationToken token = default)
        {
            string tenant = _Sql.Sanitize(tenantId ?? String.Empty);
            string order = "CASE WHEN " + _Sql.Column("tenantid") + " = '" + tenant + "' THEN 0 ELSE 1 END";
            DataTable result = await _Sql.QueryOneAsync("userroles", "name = '" + _Sql.Sanitize(name) + "' AND (tenantid = '" + tenant + "' OR tenantid = '')", token, order).ConfigureAwait(false);
            return result.Rows.Count == 0 ? null : ToRole(result.Rows[0]);
        }

        public async Task<EnumerationResult<UserRole>> EnumerateRolesAsync(EnumerationQuery query, CancellationToken token = default)
        {
            string where = TenantWhere(query?.TenantId);
            return await _Sql.EnumerateAsync("userroles", where, query ?? new EnumerationQuery(), ToRole, _Sql.Column("name") + " ASC", token, false).ConfigureAwait(false);
        }

        public async Task<Permission> CreatePermissionAsync(Permission permission, CancellationToken token = default)
        {
            if (permission == null) throw new ArgumentNullException(nameof(permission));
            if (permission.LastUpdateUtc == default) permission.LastUpdateUtc = permission.CreatedUtc;
            await ReplaceByIdAsync("permissions", permission.Id, _Sql.Columns("id", "tenantid", "name", "resourcetypes", "operationtypes", "permissiontype", "active", "isprotected", "createdutc", "lastupdateutc"),
                "'" + _Sql.Sanitize(permission.Id) + "', '" + _Sql.Sanitize(permission.TenantId) + "', '" + _Sql.Sanitize(permission.Name) + "', '" + _Sql.Sanitize(JsonSerializer.Serialize(permission.ResourceTypes)) + "', '" + _Sql.Sanitize(JsonSerializer.Serialize(permission.OperationTypes)) + "', '" + _Sql.Sanitize(permission.PermissionType) + "', " + _Sql.Bool(permission.Active) + ", " + _Sql.Bool(permission.IsProtected) + ", '" + _Sql.Timestamp(permission.CreatedUtc) + "', '" + _Sql.Timestamp(permission.LastUpdateUtc) + "'", token).ConfigureAwait(false);
            return permission;
        }

        public async Task<Permission?> ReadPermissionAsync(string? tenantId, string permissionId, CancellationToken token = default)
        {
            string where = "id = '" + _Sql.Sanitize(permissionId) + "'";
            if (tenantId != null) where += " AND (tenantid = '" + _Sql.Sanitize(tenantId) + "' OR tenantid = '')";
            DataTable result = await _Sql.QueryOneAsync("permissions", where, token).ConfigureAwait(false);
            return result.Rows.Count == 0 ? null : ToPermission(result.Rows[0]);
        }

        public async Task<EnumerationResult<Permission>> EnumeratePermissionsAsync(EnumerationQuery query, CancellationToken token = default)
        {
            string where = TenantWhere(query?.TenantId);
            return await _Sql.EnumerateAsync("permissions", where, query ?? new EnumerationQuery(), ToPermission, _Sql.Column("name") + " ASC", token, false).ConfigureAwait(false);
        }

        public async Task<RolePermissionMap> CreateRolePermissionMapAsync(RolePermissionMap map, CancellationToken token = default)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            await ReplaceByIdAsync("rolepermissionmaps", map.Id, _Sql.Columns("id", "tenantid", "roleid", "permissionid", "active", "isprotected", "createdutc"),
                "'" + _Sql.Sanitize(map.Id) + "', '" + _Sql.Sanitize(map.TenantId) + "', '" + _Sql.Sanitize(map.RoleId) + "', '" + _Sql.Sanitize(map.PermissionId) + "', " + _Sql.Bool(map.Active) + ", " + _Sql.Bool(map.IsProtected) + ", '" + _Sql.Timestamp(map.CreatedUtc) + "'", token).ConfigureAwait(false);
            return map;
        }

        public async Task<List<RolePermissionMap>> EnumerateRolePermissionMapsAsync(string? tenantId, string roleId, CancellationToken token = default)
        {
            string where = " WHERE " + _Sql.Column("roleid") + " = '" + _Sql.Sanitize(roleId) + "' AND " + _Sql.Column("active") + " = " + _Sql.Bool(true);
            if (tenantId != null) where += " AND (" + _Sql.Column("tenantid") + " = '" + _Sql.Sanitize(tenantId) + "' OR " + _Sql.Column("tenantid") + " = '')";
            return await ListAsync("rolepermissionmaps", where, ToRolePermissionMap, token).ConfigureAwait(false);
        }

        public async Task<UserRoleAssignment> CreateUserRoleAssignmentAsync(UserRoleAssignment assignment, CancellationToken token = default)
        {
            if (assignment.LastUpdateUtc == default) assignment.LastUpdateUtc = assignment.CreatedUtc;
            await ReplaceByIdAsync("userroleassignments", assignment.Id, _Sql.Columns("id", "tenantid", "userid", "roleid", "rolename", "resourcescope", "resourceid", "inheritstochildren", "active", "isprotected", "createdutc", "lastupdateutc"),
                "'" + _Sql.Sanitize(assignment.Id) + "', '" + _Sql.Sanitize(assignment.TenantId) + "', '" + _Sql.Sanitize(assignment.UserId) + "', " + _Sql.Nullable(assignment.RoleId) + ", " + _Sql.Nullable(assignment.RoleName) + ", '" + _Sql.Sanitize(assignment.ResourceScope) + "', " + _Sql.Nullable(assignment.ResourceId) + ", " + _Sql.Bool(assignment.InheritsToChildren) + ", " + _Sql.Bool(assignment.Active) + ", " + _Sql.Bool(assignment.IsProtected) + ", '" + _Sql.Timestamp(assignment.CreatedUtc) + "', '" + _Sql.Timestamp(assignment.LastUpdateUtc) + "'", token).ConfigureAwait(false);
            return assignment;
        }

        public Task<List<UserRoleAssignment>> EnumerateUserRoleAssignmentsAsync(string tenantId, string userId, CancellationToken token = default)
        {
            string where = " WHERE " + _Sql.Column("tenantid") + " = '" + _Sql.Sanitize(tenantId) + "' AND " + _Sql.Column("userid") + " = '" + _Sql.Sanitize(userId) + "' AND " + _Sql.Column("active") + " = " + _Sql.Bool(true);
            return ListAsync("userroleassignments", where, ToUserRoleAssignment, token);
        }

        public async Task<CredentialScopeAssignment> CreateCredentialScopeAssignmentAsync(CredentialScopeAssignment assignment, CancellationToken token = default)
        {
            if (assignment.LastUpdateUtc == default) assignment.LastUpdateUtc = assignment.CreatedUtc;
            await ReplaceByIdAsync("credentialscopeassignments", assignment.Id, _Sql.Columns("id", "tenantid", "credentialid", "roleid", "rolename", "resourcescope", "resourceid", "operationtypes", "resourcetypes", "active", "isprotected", "createdutc", "lastupdateutc"),
                "'" + _Sql.Sanitize(assignment.Id) + "', '" + _Sql.Sanitize(assignment.TenantId) + "', '" + _Sql.Sanitize(assignment.CredentialId) + "', " + _Sql.Nullable(assignment.RoleId) + ", " + _Sql.Nullable(assignment.RoleName) + ", '" + _Sql.Sanitize(assignment.ResourceScope) + "', " + _Sql.Nullable(assignment.ResourceId) + ", '" + _Sql.Sanitize(JsonSerializer.Serialize(assignment.OperationTypes)) + "', '" + _Sql.Sanitize(JsonSerializer.Serialize(assignment.ResourceTypes)) + "', " + _Sql.Bool(assignment.Active) + ", " + _Sql.Bool(assignment.IsProtected) + ", '" + _Sql.Timestamp(assignment.CreatedUtc) + "', '" + _Sql.Timestamp(assignment.LastUpdateUtc) + "'", token).ConfigureAwait(false);
            return assignment;
        }

        public Task<List<CredentialScopeAssignment>> EnumerateCredentialScopeAssignmentsAsync(string tenantId, string credentialId, CancellationToken token = default)
        {
            string where = " WHERE " + _Sql.Column("tenantid") + " = '" + _Sql.Sanitize(tenantId) + "' AND " + _Sql.Column("credentialid") + " = '" + _Sql.Sanitize(credentialId) + "' AND " + _Sql.Column("active") + " = " + _Sql.Bool(true);
            return ListAsync("credentialscopeassignments", where, ToCredentialScopeAssignment, token);
        }

        public async Task SeedBuiltInsAsync(CancellationToken token = default)
        {
            await SeedRoleAsync("TenantAdmin", new List<string> { "All" }, new List<string> { "All" }, token).ConfigureAwait(false);
            await SeedRoleAsync("SecurityAdmin", new List<string> { "User", "Credential", "Session", "Role", "Permission", "Assignment", "Audit", "Tenant" }, new List<string> { "Admin", "Read", "Create", "Update", "Delete" }, token).ConfigureAwait(false);
            await SeedRoleAsync("Auditor", new List<string> { "User", "Credential", "Session", "Role", "Permission", "Assignment", "Audit", "Tenant" }, new List<string> { "Read" }, token).ConfigureAwait(false);
            await SeedRoleAsync("ResourceAdmin", new List<string> { "Account", "Entry", "Balance" }, new List<string> { "All" }, token).ConfigureAwait(false);
            await SeedRoleAsync("Editor", new List<string> { "Account", "Entry", "Balance" }, new List<string> { "Read", "Write", "Create", "Update", "Delete", "Execute" }, token).ConfigureAwait(false);
            await SeedRoleAsync("Viewer", new List<string> { "Account", "Entry", "Balance" }, new List<string> { "Read" }, token).ConfigureAwait(false);
            await CreateRoleAsync(new UserRole { Id = BuiltInId(IdentifierPrefixes.Role, "Custom"), Name = "Custom", IsBuiltIn = true, IsProtected = true }, token).ConfigureAwait(false);
        }

        private async Task SeedRoleAsync(string name, List<string> resourceTypes, List<string> operationTypes, CancellationToken token)
        {
            UserRole role = new UserRole { Id = BuiltInId(IdentifierPrefixes.Role, name), Name = name, IsBuiltIn = true, IsProtected = true };
            Permission permission = new Permission { Id = BuiltInId(IdentifierPrefixes.Permission, name), Name = name + " baseline", ResourceTypes = resourceTypes, OperationTypes = operationTypes, PermissionType = "Permit", IsProtected = true };
            await CreateRoleAsync(role, token).ConfigureAwait(false);
            await CreatePermissionAsync(permission, token).ConfigureAwait(false);
            await CreateRolePermissionMapAsync(new RolePermissionMap { Id = BuiltInId(IdentifierPrefixes.Assignment, name), RoleId = role.Id, PermissionId = permission.Id, IsProtected = true }, token).ConfigureAwait(false);
        }

        private async Task ReplaceByIdAsync(string table, string id, string columns, string values, CancellationToken token)
        {
            await _Sql.ExecuteAsync("DELETE FROM " + _Sql.Table(table) + " WHERE " + _Sql.Column("id") + " = '" + _Sql.Sanitize(id) + "';" +
                "INSERT INTO " + _Sql.Table(table) + " (" + columns + ") VALUES (" + values + ");", true, token).ConfigureAwait(false);
        }

        private async Task<List<T>> ListAsync<T>(string table, string where, Func<DataRow, T> mapper, CancellationToken token)
        {
            DataTable data = await _Sql.ExecuteAsync("SELECT * FROM " + _Sql.Table(table) + where + ";", false, token).ConfigureAwait(false);
            List<T> result = new List<T>();
            foreach (DataRow row in data.Rows) result.Add(mapper(row));
            return result;
        }

        private string TenantWhere(string? tenantId)
        {
            if (tenantId == null) return String.Empty;
            return " WHERE " + _Sql.Column("tenantid") + " = '" + _Sql.Sanitize(tenantId) + "' OR " + _Sql.Column("tenantid") + " = ''";
        }

        private string BuiltInId(string prefix, string name)
        {
            string id = prefix + "builtin" + name.ToLowerInvariant().Replace(" ", String.Empty).Replace("_", String.Empty);
            return id.Length > NetLedgerId.Length ? id.Substring(0, NetLedgerId.Length) : id.PadRight(NetLedgerId.Length, '0');
        }

        private UserRole ToRole(DataRow row) => new UserRole { Id = _Sql.Get(row, "id"), TenantId = _Sql.Get(row, "tenantid"), Name = _Sql.Get(row, "name"), IsBuiltIn = _Sql.GetBool(row, "isbuiltin"), Active = _Sql.GetBool(row, "active"), IsProtected = _Sql.GetBool(row, "isprotected"), CreatedUtc = _Sql.GetDate(row, "createdutc"), LastUpdateUtc = _Sql.GetDate(row, "lastupdateutc") };
        private Permission ToPermission(DataRow row) => new Permission { Id = _Sql.Get(row, "id"), TenantId = _Sql.Get(row, "tenantid"), Name = _Sql.Get(row, "name"), ResourceTypes = ListFromJson(_Sql.Get(row, "resourcetypes")), OperationTypes = ListFromJson(_Sql.Get(row, "operationtypes")), PermissionType = _Sql.Get(row, "permissiontype"), Active = _Sql.GetBool(row, "active"), IsProtected = _Sql.GetBool(row, "isprotected"), CreatedUtc = _Sql.GetDate(row, "createdutc"), LastUpdateUtc = _Sql.GetDate(row, "lastupdateutc") };
        private RolePermissionMap ToRolePermissionMap(DataRow row) => new RolePermissionMap { Id = _Sql.Get(row, "id"), TenantId = _Sql.Get(row, "tenantid"), RoleId = _Sql.Get(row, "roleid"), PermissionId = _Sql.Get(row, "permissionid"), Active = _Sql.GetBool(row, "active"), IsProtected = _Sql.GetBool(row, "isprotected"), CreatedUtc = _Sql.GetDate(row, "createdutc") };
        private UserRoleAssignment ToUserRoleAssignment(DataRow row) => new UserRoleAssignment { Id = _Sql.Get(row, "id"), TenantId = _Sql.Get(row, "tenantid"), UserId = _Sql.Get(row, "userid"), RoleId = _Sql.GetNull(row, "roleid"), RoleName = _Sql.GetNull(row, "rolename"), ResourceScope = _Sql.Get(row, "resourcescope"), ResourceId = _Sql.GetNull(row, "resourceid"), InheritsToChildren = _Sql.GetBool(row, "inheritstochildren"), Active = _Sql.GetBool(row, "active"), IsProtected = _Sql.GetBool(row, "isprotected"), CreatedUtc = _Sql.GetDate(row, "createdutc"), LastUpdateUtc = _Sql.GetDate(row, "lastupdateutc") };
        private CredentialScopeAssignment ToCredentialScopeAssignment(DataRow row) => new CredentialScopeAssignment { Id = _Sql.Get(row, "id"), TenantId = _Sql.Get(row, "tenantid"), CredentialId = _Sql.Get(row, "credentialid"), RoleId = _Sql.GetNull(row, "roleid"), RoleName = _Sql.GetNull(row, "rolename"), ResourceScope = _Sql.Get(row, "resourcescope"), ResourceId = _Sql.GetNull(row, "resourceid"), OperationTypes = ListFromJson(_Sql.Get(row, "operationtypes")), ResourceTypes = ListFromJson(_Sql.Get(row, "resourcetypes")), Active = _Sql.GetBool(row, "active"), IsProtected = _Sql.GetBool(row, "isprotected"), CreatedUtc = _Sql.GetDate(row, "createdutc"), LastUpdateUtc = _Sql.GetDate(row, "lastupdateutc") };
        private List<string> ListFromJson(string json) => String.IsNullOrEmpty(json) ? new List<string>() : JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
    }

    internal sealed class PortableSqlDialect
    {
        private readonly DatabaseDriverBase _Driver;
        private readonly DatabaseTypeEnum _DatabaseType;

        internal PortableSqlDialect(DatabaseDriverBase driver, DatabaseTypeEnum databaseType)
        {
            _Driver = driver;
            _DatabaseType = databaseType;
        }

        internal Task<DataTable> ExecuteAsync(string query, bool isWrite, CancellationToken token)
        {
            return _Driver.ExecuteQueryAsync(query, isWrite, token);
        }

        internal async Task<DataTable> QueryOneAsync(string table, string where, CancellationToken token, string? orderBy = null)
        {
            string order = String.IsNullOrEmpty(orderBy) ? String.Empty : " ORDER BY " + orderBy;
            string sql = _DatabaseType == DatabaseTypeEnum.SqlServer
                ? "SELECT TOP 1 * FROM " + Table(table) + " WHERE " + where + order + ";"
                : "SELECT * FROM " + Table(table) + " WHERE " + where + order + " LIMIT 1;";
            return await ExecuteAsync(sql, false, token).ConfigureAwait(false);
        }

        internal async Task<EnumerationResult<T>> EnumerateAsync<T>(string table, string whereClause, EnumerationQuery query, Func<DataRow, T> mapper, string orderBy, CancellationToken token, bool paginate = true)
        {
            EnumerationResult<T> result = new EnumerationResult<T> { MaxResults = query.MaxResults, Skip = query.Skip };
            DataTable count = await ExecuteAsync("SELECT COUNT(*) FROM " + Table(table) + whereClause + ";", false, token).ConfigureAwait(false);
            if (count.Rows.Count > 0) result.TotalRecords = Convert.ToInt64(count.Rows[0][0], CultureInfo.InvariantCulture);

            string paging = paginate ? " " + LimitOffset(query) : String.Empty;
            DataTable rows = await ExecuteAsync("SELECT * FROM " + Table(table) + whereClause + " ORDER BY " + orderBy + paging + ";", false, token).ConfigureAwait(false);
            foreach (DataRow row in rows.Rows) result.Objects.Add(mapper(row));

            result.RecordsRemaining = Math.Max(0, result.TotalRecords - query.Skip - result.Objects.Count);
            result.EndOfResults = result.RecordsRemaining == 0;
            if (!result.EndOfResults && result.Objects.Count > 0) result.ContinuationToken = String.Empty;
            return result;
        }

        internal async Task<long> CountAsync(string table, string where, CancellationToken token)
        {
            DataTable count = await ExecuteAsync("SELECT COUNT(*) FROM " + Table(table) + " WHERE " + where + ";", false, token).ConfigureAwait(false);
            return count.Rows.Count > 0 ? Convert.ToInt64(count.Rows[0][0], CultureInfo.InvariantCulture) : 0L;
        }

        internal string Table(string name) => _DatabaseType == DatabaseTypeEnum.SqlServer ? "[" + name + "]" : _DatabaseType == DatabaseTypeEnum.Mysql ? "`" + name + "`" : name;
        internal string Column(string name) => _DatabaseType == DatabaseTypeEnum.SqlServer ? "[" + name + "]" : _DatabaseType == DatabaseTypeEnum.Mysql ? "`" + name + "`" : name;
        internal string Columns(params string[] names) => String.Join(", ", Array.ConvertAll(names, Column));
        internal string Bool(bool value) => _DatabaseType == DatabaseTypeEnum.Postgresql ? (value ? "TRUE" : "FALSE") : (value ? "1" : "0");
        internal string Timestamp(DateTime value) => value.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss.ffffff", CultureInfo.InvariantCulture);
        internal string Nullable(string? value) => String.IsNullOrEmpty(value) ? "NULL" : "'" + Sanitize(value) + "'";
        internal string Sanitize(string? value) => String.IsNullOrEmpty(value) ? String.Empty : value.Replace("'", "''");
        internal string Get(DataRow row, string columnName) => row.Table.Columns.Contains(columnName) && row[columnName] != DBNull.Value ? row[columnName]?.ToString() ?? String.Empty : String.Empty;
        internal string? GetNull(DataRow row, string columnName)
        {
            string value = Get(row, columnName);
            return String.IsNullOrEmpty(value) ? null : value;
        }
        internal bool GetBool(DataRow row, string columnName)
        {
            string value = Get(row, columnName);
            return value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase);
        }
        internal DateTime GetDate(DataRow row, string columnName)
        {
            string value = Get(row, columnName);
            return String.IsNullOrEmpty(value)
                ? DateTime.MinValue
                : DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
        }
        private string LimitOffset(EnumerationQuery query)
        {
            return _DatabaseType == DatabaseTypeEnum.SqlServer
                ? "OFFSET " + query.Skip + " ROWS FETCH NEXT " + query.MaxResults + " ROWS ONLY"
                : "LIMIT " + query.MaxResults + " OFFSET " + query.Skip;
        }
    }
}

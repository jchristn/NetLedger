namespace NetLedger.Sdk
{
    /// <summary>
    /// User role assignment information.
    /// </summary>
    public class UserRoleAssignmentInfo
    {
        /// <summary>
        /// Assignment identifier.
        /// </summary>
        public string? Id { get; set; }

        /// <summary>
        /// Tenant identifier.
        /// </summary>
        public string? TenantId { get; set; }

        /// <summary>
        /// User identifier.
        /// </summary>
        public string? UserId { get; set; }

        /// <summary>
        /// Role identifier.
        /// </summary>
        public string? RoleId { get; set; }

        /// <summary>
        /// Role name.
        /// </summary>
        public string? RoleName { get; set; }

        /// <summary>
        /// Resource scope.
        /// </summary>
        public string ResourceScope { get; set; } = "Tenant";

        /// <summary>
        /// Resource identifier.
        /// </summary>
        public string? ResourceId { get; set; }

        /// <summary>
        /// Whether tenant grants inherit to child resources.
        /// </summary>
        public bool InheritsToChildren { get; set; } = true;

        /// <summary>
        /// Whether the assignment is active.
        /// </summary>
        public bool Active { get; set; } = true;
    }
}

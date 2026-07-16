namespace NetLedger
{
    /// <summary>
    /// Effective permission tuple for dashboard authorization checks.
    /// </summary>
    public class EffectivePermission
    {
        /// <summary>
        /// Resource type.
        /// </summary>
        public string ResourceType { get; set; } = "All";

        /// <summary>
        /// Operation type.
        /// </summary>
        public string OperationType { get; set; } = "All";

        /// <summary>
        /// Resource scope.
        /// </summary>
        public string ResourceScope { get; set; } = "Tenant";

        /// <summary>
        /// Resource identifier.
        /// </summary>
        public string? ResourceId { get; set; } = null;

        /// <summary>
        /// Permission type.
        /// </summary>
        public string PermissionType { get; set; } = "Permit";
    }
}

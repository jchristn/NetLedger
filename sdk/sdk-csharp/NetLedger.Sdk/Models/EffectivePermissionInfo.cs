namespace NetLedger.Sdk
{
    /// <summary>
    /// Effective permission tuple.
    /// </summary>
    public class EffectivePermissionInfo
    {
        /// <summary>
        /// Resource type.
        /// </summary>
        public string ResourceType { get; set; } = string.Empty;

        /// <summary>
        /// Operation type.
        /// </summary>
        public string OperationType { get; set; } = string.Empty;
    }
}

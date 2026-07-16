namespace NetLedger.Server.Models
{
    /// <summary>
    /// OpenAPI route descriptor.
    /// </summary>
    internal sealed class OpenApiRouteDescriptor
    {
        /// <summary>
        /// HTTP method.
        /// </summary>
        internal string Method { get; set; } = string.Empty;

        /// <summary>
        /// Route path.
        /// </summary>
        internal string Path { get; set; } = string.Empty;

        /// <summary>
        /// Route summary.
        /// </summary>
        internal string Summary { get; set; } = string.Empty;

        /// <summary>
        /// OpenAPI tag.
        /// </summary>
        internal string Tag { get; set; } = string.Empty;
    }
}

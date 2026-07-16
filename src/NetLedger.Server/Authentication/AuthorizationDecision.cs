namespace NetLedger.Server.Authentication
{
    /// <summary>
    /// Authorization decision.
    /// </summary>
    public class AuthorizationDecision
    {
        /// <summary>
        /// Whether the request is permitted.
        /// </summary>
        public bool Permitted { get; set; } = false;

        /// <summary>
        /// Denial reason.
        /// </summary>
        public string? Reason { get; set; } = null;

        /// <summary>
        /// Create a permit decision.
        /// </summary>
        /// <returns>Authorization decision.</returns>
        public static AuthorizationDecision Permit()
        {
            return new AuthorizationDecision { Permitted = true };
        }

        /// <summary>
        /// Create a deny decision.
        /// </summary>
        /// <param name="reason">Denial reason.</param>
        /// <returns>Authorization decision.</returns>
        public static AuthorizationDecision Deny(string reason)
        {
            return new AuthorizationDecision { Permitted = false, Reason = reason };
        }
    }
}

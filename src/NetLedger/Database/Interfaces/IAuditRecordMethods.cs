namespace NetLedger.Database.Interfaces
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Interface for audit record data access.
    /// </summary>
    public interface IAuditRecordMethods
    {
        /// <summary>
        /// Create an audit record.
        /// </summary>
        Task<AuditRecord> CreateAsync(AuditRecord record, CancellationToken token = default);

        /// <summary>
        /// Enumerate audit records.
        /// </summary>
        Task<EnumerationResult<AuditRecord>> EnumerateAsync(EnumerationQuery query, CancellationToken token = default);
    }
}

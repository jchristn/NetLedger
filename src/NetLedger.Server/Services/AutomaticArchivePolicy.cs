namespace NetLedger.Server.Services
{
    internal sealed class AutomaticArchivePolicy
    {
        internal bool Enabled { get; set; } = false;

        internal long MaxRetentionDays { get; set; } = 365;

        internal int IntervalSeconds { get; set; } = 3600;

        internal int MaxBatchRows { get; set; } = 50000;

        internal bool DeleteAfterCommit { get; set; } = false;

        internal string? StoragePoolId { get; set; } = null;

        internal int RetryMaxAttempts { get; set; } = 3;

        internal int RetryInitialDelaySeconds { get; set; } = 5;

        internal int RetryMaxDelaySeconds { get; set; } = 300;
    }
}

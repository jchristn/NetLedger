namespace NetLedger.Server.Settings
{
    using System;

    /// <summary>
    /// Automatic archival retry policy.
    /// </summary>
    public class ArchiveRetrySettings
    {
        #region Public-Members

        /// <summary>
        /// Maximum attempts per archival operation.
        /// Range is 1 through 100.
        /// </summary>
        public int MaxAttempts
        {
            get
            {
                return _MaxAttempts;
            }
            set
            {
                _MaxAttempts = Math.Clamp(value, 1, 100);
            }
        }

        /// <summary>
        /// Initial retry delay in seconds.
        /// Range is 0 through Int32.MaxValue.
        /// </summary>
        public int InitialDelaySeconds
        {
            get
            {
                return _InitialDelaySeconds;
            }
            set
            {
                _InitialDelaySeconds = Math.Clamp(value, 0, Int32.MaxValue);
            }
        }

        /// <summary>
        /// Maximum retry delay in seconds.
        /// Range is 0 through Int32.MaxValue.
        /// </summary>
        public int MaxDelaySeconds
        {
            get
            {
                return _MaxDelaySeconds;
            }
            set
            {
                _MaxDelaySeconds = Math.Clamp(value, 0, Int32.MaxValue);
            }
        }

        #endregion

        #region Private-Members

        private int _MaxAttempts = 3;
        private int _InitialDelaySeconds = 5;
        private int _MaxDelaySeconds = 300;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public ArchiveRetrySettings()
        {
        }

        #endregion
    }
}

namespace NetLedger
{
    using System;
    using PrettyId;

    /// <summary>
    /// NetLedger identifier generation helper.
    /// </summary>
    public static class NetLedgerId
    {
        #region Public-Members

        /// <summary>
        /// Total identifier length including prefix.
        /// </summary>
        public const int Length = 32;

        #endregion

        #region Private-Members

        private static readonly IdGenerator _Generator = new IdGenerator();

        #endregion

        #region Public-Methods

        /// <summary>
        /// Generate a K-sortable NetLedger identifier.
        /// </summary>
        /// <param name="prefix">Entity prefix.</param>
        /// <returns>Generated identifier.</returns>
        /// <exception cref="ArgumentNullException">Thrown when prefix is null or empty.</exception>
        public static string Generate(string prefix)
        {
            if (String.IsNullOrEmpty(prefix)) throw new ArgumentNullException(nameof(prefix));
            return _Generator.GenerateKSortable(prefix, Length);
        }

        #endregion
    }
}


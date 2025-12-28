namespace NetLedger.Database
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    /// <summary>
    /// Dynamic search filter builder for constructing SQL WHERE clauses.
    /// </summary>
    public class FilterBuilder
    {
        #region Public-Members

        /// <summary>
        /// Start time UTC filter (created on or after).
        /// </summary>
        public DateTime? StartTimeUtc
        {
            get { return _StartTimeUtc; }
            set { _StartTimeUtc = value; }
        }

        /// <summary>
        /// End time UTC filter (created on or before).
        /// </summary>
        public DateTime? EndTimeUtc
        {
            get { return _EndTimeUtc; }
            set { _EndTimeUtc = value; }
        }

        /// <summary>
        /// Search term for description/notes.
        /// </summary>
        public string SearchTerm
        {
            get { return _SearchTerm; }
            set { _SearchTerm = value; }
        }

        /// <summary>
        /// Entry type filter.
        /// </summary>
        public EntryType? EntryType
        {
            get { return _EntryType; }
            set { _EntryType = value; }
        }

        /// <summary>
        /// Minimum amount filter.
        /// </summary>
        public decimal? AmountMinimum
        {
            get { return _AmountMinimum; }
            set { _AmountMinimum = value; }
        }

        /// <summary>
        /// Maximum amount filter.
        /// </summary>
        public decimal? AmountMaximum
        {
            get { return _AmountMaximum; }
            set { _AmountMaximum = value; }
        }

        /// <summary>
        /// Is committed filter.
        /// </summary>
        public bool? IsCommitted
        {
            get { return _IsCommitted; }
            set { _IsCommitted = value; }
        }

        /// <summary>
        /// Exclude balance entries.
        /// Default is true.
        /// </summary>
        public bool ExcludeBalanceEntries
        {
            get { return _ExcludeBalanceEntries; }
            set { _ExcludeBalanceEntries = value; }
        }

        /// <summary>
        /// Ordering.
        /// Default is CreatedDescending.
        /// </summary>
        public EnumerationOrderEnum Ordering
        {
            get { return _Ordering; }
            set { _Ordering = value; }
        }

        /// <summary>
        /// Skip count (offset).
        /// Default is 0.
        /// </summary>
        public int Skip
        {
            get { return _Skip; }
            set
            {
                if (value < 0) throw new ArgumentOutOfRangeException(nameof(Skip), "Skip must be zero or greater.");
                _Skip = value;
            }
        }

        /// <summary>
        /// Maximum number of results to return.
        /// Minimum value is 1.
        /// Maximum value is 1000.
        /// Default value is 1000.
        /// </summary>
        public int MaxResults
        {
            get { return _MaxResults; }
            set
            {
                if (value < 1) throw new ArgumentException("MaxResults must be greater than zero.");
                if (value > 1000) throw new ArgumentException("MaxResults must be one thousand or less.");
                _MaxResults = value;
            }
        }

        #endregion

        #region Private-Members

        private DateTime? _StartTimeUtc = null;
        private DateTime? _EndTimeUtc = null;
        private string _SearchTerm = null;
        private EntryType? _EntryType = null;
        private decimal? _AmountMinimum = null;
        private decimal? _AmountMaximum = null;
        private bool? _IsCommitted = null;
        private bool _ExcludeBalanceEntries = true;
        private EnumerationOrderEnum _Ordering = EnumerationOrderEnum.CreatedDescending;
        private int _Skip = 0;
        private int _MaxResults = 1000;

        private static readonly string _TimestampFormat = "yyyy-MM-dd HH:mm:ss.ffffffZ";

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the filter builder.
        /// </summary>
        public FilterBuilder()
        {
        }

        /// <summary>
        /// Create a filter builder from an enumeration query.
        /// </summary>
        /// <param name="query">Enumeration query.</param>
        /// <returns>Filter builder.</returns>
        public static FilterBuilder FromEnumerationQuery(EnumerationQuery query)
        {
            if (query == null) throw new ArgumentNullException(nameof(query));

            FilterBuilder filter = new FilterBuilder();
            filter.StartTimeUtc = query.CreatedAfterUtc;
            filter.EndTimeUtc = query.CreatedBeforeUtc;
            filter.SearchTerm = query.SearchTerm;
            filter.AmountMinimum = query.AmountMinimum;
            filter.AmountMaximum = query.AmountMaximum;
            filter.Ordering = query.Ordering;
            filter.Skip = query.Skip;
            filter.MaxResults = query.MaxResults;
            return filter;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Build the WHERE clause conditions for entries (without WHERE keyword).
        /// </summary>
        /// <param name="dbType">Database type for syntax differences.</param>
        /// <returns>WHERE clause conditions or empty string.</returns>
        public string BuildEntryConditions(DatabaseTypeEnum dbType)
        {
            List<string> conditions = new List<string>();

            if (StartTimeUtc.HasValue)
            {
                conditions.Add("createdutc >= " + FormatTimestamp(StartTimeUtc.Value, dbType));
            }

            if (EndTimeUtc.HasValue)
            {
                conditions.Add("createdutc <= " + FormatTimestamp(EndTimeUtc.Value, dbType));
            }

            if (!String.IsNullOrEmpty(SearchTerm))
            {
                conditions.Add("description LIKE " + FormatLikePattern("%" + SanitizeString(SearchTerm) + "%", dbType));
            }

            if (EntryType.HasValue)
            {
                conditions.Add("type = " + FormatEntryType(EntryType.Value, dbType));
            }

            if (AmountMinimum.HasValue)
            {
                conditions.Add("amount >= " + AmountMinimum.Value.ToString());
            }

            if (AmountMaximum.HasValue)
            {
                conditions.Add("amount <= " + AmountMaximum.Value.ToString());
            }

            if (IsCommitted.HasValue)
            {
                conditions.Add(FormatIsCommittedCondition(IsCommitted.Value, dbType));
            }

            if (ExcludeBalanceEntries)
            {
                conditions.Add("type != " + FormatEntryType(NetLedger.EntryType.Balance, dbType));
            }

            if (conditions.Count == 0)
                return String.Empty;

            return String.Join(" AND ", conditions);
        }

        /// <summary>
        /// Build the WHERE clause conditions for accounts (without WHERE keyword).
        /// </summary>
        /// <param name="dbType">Database type for syntax differences.</param>
        /// <returns>WHERE clause conditions or empty string.</returns>
        public string BuildAccountConditions(DatabaseTypeEnum dbType)
        {
            List<string> conditions = new List<string>();

            if (StartTimeUtc.HasValue)
            {
                conditions.Add("createdutc >= " + FormatTimestamp(StartTimeUtc.Value, dbType));
            }

            if (EndTimeUtc.HasValue)
            {
                conditions.Add("createdutc <= " + FormatTimestamp(EndTimeUtc.Value, dbType));
            }

            if (!String.IsNullOrEmpty(SearchTerm))
            {
                conditions.Add("name LIKE " + FormatLikePattern("%" + SanitizeString(SearchTerm) + "%", dbType));
            }

            if (conditions.Count == 0)
                return String.Empty;

            return String.Join(" AND ", conditions);
        }

        /// <summary>
        /// Get the ORDER BY clause.
        /// </summary>
        /// <param name="dbType">Database type for syntax differences.</param>
        /// <returns>ORDER BY clause.</returns>
        public string GetOrderByClause(DatabaseTypeEnum dbType)
        {
            switch (Ordering)
            {
                case EnumerationOrderEnum.CreatedAscending:
                    return "ORDER BY createdutc ASC";
                case EnumerationOrderEnum.CreatedDescending:
                    return "ORDER BY createdutc DESC";
                case EnumerationOrderEnum.AmountAscending:
                    return "ORDER BY amount ASC";
                case EnumerationOrderEnum.AmountDescending:
                    return "ORDER BY amount DESC";
                default:
                    return "ORDER BY createdutc DESC";
            }
        }

        /// <summary>
        /// Get the LIMIT/OFFSET clause.
        /// </summary>
        /// <param name="dbType">Database type for syntax differences.</param>
        /// <returns>LIMIT/OFFSET clause.</returns>
        public string GetLimitOffsetClause(DatabaseTypeEnum dbType)
        {
            StringBuilder sb = new StringBuilder();

            switch (dbType)
            {
                case DatabaseTypeEnum.SqlServer:
                    sb.Append("OFFSET " + Skip + " ROWS");
                    sb.Append(" FETCH NEXT " + MaxResults + " ROWS ONLY");
                    break;

                case DatabaseTypeEnum.Sqlite:
                    sb.Append("LIMIT " + MaxResults);
                    if (Skip > 0)
                    {
                        sb.Append(" OFFSET " + Skip);
                    }
                    break;

                case DatabaseTypeEnum.Mysql:
                    sb.Append("LIMIT " + MaxResults);
                    if (Skip > 0)
                    {
                        sb.Append(" OFFSET " + Skip);
                    }
                    break;

                case DatabaseTypeEnum.Postgresql:
                default:
                    sb.Append("LIMIT " + MaxResults);
                    if (Skip > 0)
                    {
                        sb.Append(" OFFSET " + Skip);
                    }
                    break;
            }

            return sb.ToString();
        }

        /// <summary>
        /// Sanitize a string for SQL injection prevention.
        /// </summary>
        /// <param name="input">Input string.</param>
        /// <returns>Sanitized string.</returns>
        public static string SanitizeString(string input)
        {
            if (String.IsNullOrEmpty(input)) return String.Empty;
            return input.Replace("'", "''");
        }

        #endregion

        #region Private-Methods

        private string FormatTimestamp(DateTime dt, DatabaseTypeEnum dbType)
        {
            switch (dbType)
            {
                case DatabaseTypeEnum.SqlServer:
                    return "'" + dt.ToString("yyyy-MM-dd HH:mm:ss.fffffff") + "'";
                case DatabaseTypeEnum.Mysql:
                    return "'" + dt.ToString("yyyy-MM-dd HH:mm:ss.ffffff") + "'";
                case DatabaseTypeEnum.Postgresql:
                    return "'" + dt.ToString("yyyy-MM-dd HH:mm:ss.ffffff") + "'";
                case DatabaseTypeEnum.Sqlite:
                default:
                    return "'" + dt.ToString(_TimestampFormat) + "'";
            }
        }

        private string FormatLikePattern(string pattern, DatabaseTypeEnum dbType)
        {
            return "'" + pattern + "'";
        }

        private string FormatEntryType(EntryType entryType, DatabaseTypeEnum dbType)
        {
            // All databases store entry types as strings
            return "'" + entryType.ToString() + "'";
        }

        private string FormatIsCommittedCondition(bool isCommitted, DatabaseTypeEnum dbType)
        {
            switch (dbType)
            {
                case DatabaseTypeEnum.Sqlite:
                    // SQLite uses 'committed' column with INTEGER (0/1)
                    return "committed = " + (isCommitted ? "1" : "0");

                case DatabaseTypeEnum.Postgresql:
                    // PostgreSQL uses 'iscommitted' column with BOOLEAN (TRUE/FALSE)
                    return "iscommitted = " + (isCommitted ? "TRUE" : "FALSE");

                case DatabaseTypeEnum.Mysql:
                case DatabaseTypeEnum.SqlServer:
                default:
                    // MySQL and SQL Server use 'iscommitted' column with TINYINT/BIT (0/1)
                    return "iscommitted = " + (isCommitted ? "1" : "0");
            }
        }

        #endregion
    }
}

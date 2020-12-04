using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Watson.ORM.Core;
using Watson.ORM.Sqlite;

namespace NetLedger
{
    /// <summary>
    /// NetLedger.
    /// </summary>
    public class Ledger
    {
        #region Public-Members

        /// <summary>
        /// Event fired when an account is created.
        /// </summary>
        public event EventHandler<AccountEventArgs> AccountCreated;

        /// <summary>
        /// Event fired when an account is deleted.
        /// </summary>
        public event EventHandler<AccountEventArgs> AccountDeleted;

        /// <summary>
        /// Event fired when a credit is added.
        /// </summary>
        public event EventHandler<EntryEventArgs> CreditAdded;

        /// <summary>
        /// Event fired when a debit is added.
        /// </summary>
        public event EventHandler<EntryEventArgs> DebitAdded;

        /// <summary>
        /// Event fired when an entry is canceled.
        /// </summary>
        public event EventHandler<EntryEventArgs> EntryCanceled;

        /// <summary>
        /// Event fired when entries are committed successfully.
        /// </summary>
        public event EventHandler<CommitEventArgs> EntriesCommitted;

        #endregion

        #region Private-Members

        private string _DatabaseFile = null;
        private DatabaseSettings _DatabaseSettings = null;
        private WatsonORM _ORM = null; 
        private ConcurrentDictionary<string, DateTime> _LockedAccounts = new ConcurrentDictionary<string, DateTime>();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the ledger.
        /// </summary>
        /// <param name="filename">Sqlite database filename.</param>
        public Ledger(string filename)
        {
            if (String.IsNullOrEmpty(filename)) throw new ArgumentNullException(nameof(filename));

            _DatabaseFile = filename;
            _DatabaseSettings = new DatabaseSettings(_DatabaseFile);
            _ORM = new WatsonORM(_DatabaseSettings);
            _ORM.InitializeDatabase();
            _ORM.InitializeTable(typeof(Account));
            _ORM.InitializeTable(typeof(Entry));
        }

        #endregion

        #region Public-Account-Methods

        /// <summary>
        /// Creates an account with the specified name.
        /// </summary>
        /// <param name="name">Name of the account.</param>
        /// <param name="initialBalance">Initial balance of the account.</param>
        /// <returns>String containing the GUID of the newly-created account.</returns>
        public string CreateAccount(string name, decimal? initialBalance = null)
        {
            if (String.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name)); 
            Account a = new Account(name);
            a = _ORM.Insert<Account>(a);

            try
            {
                LockAccount(a.GUID);

                Entry balance = new Entry();
                balance.GUID = Guid.NewGuid().ToString();
                balance.AccountGUID = a.GUID;
                balance.Type = EntryType.Balance;

                balance.Amount = 0m;
                if (initialBalance != null) balance.Amount = initialBalance.Value;

                balance.Description = "Initial balance";
                balance.SummarizedGUIDs = null;
                balance.IsCommitted = true;

                DateTime ts = DateTime.Now.ToUniversalTime();
                balance.CreatedUtc = ts;
                balance.CommittedUtc = ts;
                balance = _ORM.Insert<Entry>(balance);
            }
            finally
            {
                UnlockAccount(a.GUID);
                Task.Run(() => AccountCreated?.Invoke(this, new AccountEventArgs(a)));
            }

            return a.GUID;
        }

        /// <summary>
        /// Delete an account and associated entries by name.
        /// </summary>
        /// <param name="name">Name of the account.</param>
        public void DeleteAccountByName(string name)
        {
            if (String.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));
            DbExpression e1 = new DbExpression(_ORM.GetColumnName<Account>(nameof(Account.Name)), DbOperators.Equals, name);
            Account a = _ORM.SelectFirst<Account>(e1);
            if (a != null)
            {
                try
                {
                    LockAccount(a.GUID);
                    DbExpression e2 = new DbExpression(_ORM.GetColumnName<Entry>(nameof(Entry.AccountGUID)), DbOperators.Equals, a.GUID);
                    _ORM.DeleteMany<Entry>(e2);
                    _ORM.Delete<Account>(a);
                }
                finally
                {
                    UnlockAccount(a.GUID);
                    Task.Run(() => AccountDeleted?.Invoke(this, new AccountEventArgs(a)));
                }
            }
        }

        /// <summary>
        /// Delete an account and associated entries by account GUID.
        /// </summary>
        /// <param name="guid">GUID of the account.</param>
        public void DeleteAccountByGuid(string guid)
        {
            if (String.IsNullOrEmpty(guid)) throw new ArgumentNullException(nameof(guid));
            DbExpression e1 = new DbExpression(_ORM.GetColumnName<Account>(nameof(Account.GUID)), DbOperators.Equals, guid);
            Account a = _ORM.SelectFirst<Account>(e1);
            if (a != null)
            {
                try
                {
                    LockAccount(a.GUID);
                    DbExpression e2 = new DbExpression(_ORM.GetColumnName<Entry>(nameof(Entry.AccountGUID)), DbOperators.Equals, a.GUID);
                    _ORM.DeleteMany<Entry>(e2);
                    _ORM.Delete<Account>(a);
                }
                finally
                {
                    UnlockAccount(a.GUID);
                    Task.Run(() => AccountDeleted?.Invoke(this, new AccountEventArgs(a)));
                }
            }
        }

        /// <summary>
        /// Retrieve an account by name.
        /// </summary>
        /// <param name="name">Name of the account.</param>
        /// <returns>Account or null if it does not exist.</returns>
        public Account GetAccountByName(string name)
        {
            if (String.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));
            DbExpression e1 = new DbExpression(_ORM.GetColumnName<Account>(nameof(Account.Name)), DbOperators.Equals, name);
            return _ORM.SelectFirst<Account>(e1);
        }

        /// <summary>
        /// Retrieve an account by GUID.
        /// </summary>
        /// <param name="guid">GUID of the account.</param>
        /// <returns>Account or null if it does not exist.</returns>
        public Account GetAccountByGuid(string guid)
        {
            if (String.IsNullOrEmpty(guid)) throw new ArgumentNullException(nameof(guid));
            DbExpression e1 = new DbExpression(_ORM.GetColumnName<Account>(nameof(Account.GUID)), DbOperators.Equals, guid);
            return _ORM.SelectFirst<Account>(e1);
        }

        /// <summary>
        /// Retrieve all accounts.
        /// </summary>
        /// <param name="searchTerm">Term to search within account names.</param>
        /// <returns>List of Account objects.</returns>
        public List<Account> GetAllAccounts(string searchTerm = null)
        {
            DbResultOrder[] ro = new DbResultOrder[1];
            ro[0] = new DbResultOrder(_ORM.GetColumnName<Entry>(nameof(Account.CreatedUtc)), DbOrderDirection.Descending);
            DbExpression e1 = new DbExpression(_ORM.GetColumnName<Account>(nameof(Account.Id)), DbOperators.GreaterThan, 0);
            if (!String.IsNullOrEmpty(searchTerm)) e1.PrependAnd(_ORM.GetColumnName<Account>(nameof(Account.Name)), DbOperators.Contains, searchTerm);
            return _ORM.SelectMany<Account>(null, null, e1, ro);
        }

        #endregion

        #region Public-Entry-Methods

        /// <summary>
        /// Add a credit.
        /// </summary>
        /// <param name="accountGuid">GUID of the account.</param>
        /// <param name="amount">Amount of the credit (zero or greater).</param>
        /// <param name="isCommitted">Indicates if the transaction has already been commited to the current committed balance.</param>
        /// <param name="notes">Notes for the transaction.</param>
        /// <returns>String containing the GUID of the newly-created entry.</returns>
        public string AddCredit(string accountGuid, decimal amount, string notes = null, bool isCommitted = false)
        {
            if (String.IsNullOrEmpty(accountGuid)) throw new ArgumentNullException(nameof(accountGuid));
            if (amount < 0) throw new ArgumentException("Amount must be zero or greater.");
            DbExpression e1 = new DbExpression(_ORM.GetColumnName<Account>(nameof(Account.GUID)), DbOperators.Equals, accountGuid);
            Account a = _ORM.SelectFirst<Account>(e1);
            if (a == null) throw new KeyNotFoundException("Unable to find account with GUID " + accountGuid + ".");

            Entry entry = null;

            try
            {
                LockAccount(accountGuid);
                entry = new Entry(accountGuid, EntryType.Credit, amount, notes, null, isCommitted);
                entry = _ORM.Insert<Entry>(entry);
                return entry.GUID;
            }
            finally
            {
                UnlockAccount(accountGuid);
                if (entry != null) Task.Run(() => CreditAdded?.Invoke(this, new EntryEventArgs(a, entry)));
            }
        }

        /// <summary>
        /// Add a debit.
        /// </summary>
        /// <param name="accountGuid">GUID of the account.</param>
        /// <param name="amount">Amount of the debit (zero or greater).</param>
        /// <param name="isCommitted">Indicates if the transaction has already been commited to the current committed balance.</param>
        /// <param name="notes">Notes for the transaction.</param>
        /// <returns>String containing the GUID of the newly-created entry.</returns>
        public string AddDebit(string accountGuid, decimal amount, string notes = null, bool isCommitted = false)
        {
            if (String.IsNullOrEmpty(accountGuid)) throw new ArgumentNullException(nameof(accountGuid));
            if (amount < 0) throw new ArgumentException("Amount must be zero or greater.");
            DbExpression e1 = new DbExpression(_ORM.GetColumnName<Account>(nameof(Account.GUID)), DbOperators.Equals, accountGuid);
            Account a = _ORM.SelectFirst<Account>(e1);
            if (a == null) throw new KeyNotFoundException("Unable to find account with GUID " + accountGuid + ".");

            Entry entry = null;

            try
            {
                LockAccount(accountGuid);
                entry = new Entry(accountGuid, EntryType.Debit, amount, notes, null, isCommitted);
                entry = _ORM.Insert<Entry>(entry);
                return entry.GUID;
            }
            finally
            {
                UnlockAccount(accountGuid);
                if (entry != null) Task.Run(() => DebitAdded?.Invoke(this, new EntryEventArgs(a, entry)));
            }
        }

        /// <summary>
        /// Cancel a pending entry.
        /// </summary>
        /// <param name="accountGuid">GUID of the account.</param>
        /// <param name="entryGuid">GUID of the entry.</param>
        /// <returns></returns>
        public void CancelPending(string accountGuid, string entryGuid)
        {
            if (String.IsNullOrEmpty(accountGuid)) throw new ArgumentNullException(nameof(accountGuid));
            if (String.IsNullOrEmpty(entryGuid)) throw new ArgumentNullException(nameof(entryGuid));
            DbExpression e1 = new DbExpression(_ORM.GetColumnName<Account>(nameof(Account.GUID)), DbOperators.Equals, accountGuid);
            Account a = _ORM.SelectFirst<Account>(e1);
            if (a == null) throw new KeyNotFoundException("Unable to find account with GUID " + accountGuid + ".");

            Entry entry = null;

            try
            {
                LockAccount(accountGuid);
                DbExpression e2 = new DbExpression(_ORM.GetColumnName<Entry>(nameof(Entry.AccountGUID)), DbOperators.Equals, accountGuid);
                e2.PrependAnd(_ORM.GetColumnName<Entry>(nameof(Entry.IsCommitted)), DbOperators.Equals, false);
                e2.PrependAnd(_ORM.GetColumnName<Entry>(nameof(Entry.CommittedUtc)), DbOperators.IsNull, null);
                e2.PrependAnd(_ORM.GetColumnName<Entry>(nameof(Entry.GUID)), DbOperators.Equals, entryGuid);
                entry = _ORM.SelectFirst<Entry>(e2);
                if (entry == null) throw new KeyNotFoundException("Unable to find pending entry with GUID " + entryGuid + ".");

                _ORM.Delete<Entry>(entry);
            }
            finally
            {
                UnlockAccount(accountGuid);
                if (entry != null) Task.Run(() => EntryCanceled?.Invoke(this, new EntryEventArgs(a, entry)));
            }
        }

        /// <summary>
        /// Retrieve balance details for a given account.
        /// </summary>
        /// <param name="accountGuid">GUID of the account.</param>
        /// <param name="applyLock">Indicate whether or not the account should be locked during retrieval of balance details.  Leave this value as 'true'.</param>
        /// <returns>Balance details.</returns>
        public Balance GetBalance(string accountGuid, bool applyLock = true)
        {
            if (String.IsNullOrEmpty(accountGuid)) throw new ArgumentNullException(nameof(accountGuid));
            DbExpression e1 = new DbExpression(_ORM.GetColumnName<Account>(nameof(Account.GUID)), DbOperators.Equals, accountGuid);
            Account a = _ORM.SelectFirst<Account>(e1);
            if (a == null) throw new KeyNotFoundException("Unable to find account with GUID " + accountGuid + ".");

            try
            {
                if (applyLock) LockAccount(accountGuid);
                Balance balance = new Balance();

                // Get current balance
                DbExpression e2 = new DbExpression(_ORM.GetColumnName<Entry>(nameof(Entry.AccountGUID)), DbOperators.Equals, accountGuid);
                e2.PrependAnd(_ORM.GetColumnName<Entry>(nameof(Entry.Type)), DbOperators.Equals, EntryType.Balance);

                DbResultOrder[] ro = new DbResultOrder[1];
                ro[0] = new DbResultOrder(_ORM.GetColumnName<Entry>(nameof(Entry.CreatedUtc)), DbOrderDirection.Descending);
                List<Entry> balanceEntries = _ORM.SelectMany<Entry>(null, 1, e2, ro);

                Entry balanceEntry = null;
                if (balanceEntries != null && balanceEntries.Count > 0) balanceEntry = balanceEntries[0];
                if (balanceEntry == null) throw new InvalidOperationException("No balance entry found for account with GUID " + accountGuid + ".");

                balance.BalanceTimestampUtc = balanceEntry.CreatedUtc;
                balance.CommittedBalance = balanceEntry.Amount;
            
                // Get pending transactions
                DbExpression e3 = new DbExpression(_ORM.GetColumnName<Entry>(nameof(Entry.AccountGUID)), DbOperators.Equals, accountGuid);
                e3.PrependAnd(_ORM.GetColumnName<Entry>(nameof(Entry.IsCommitted)), DbOperators.Equals, false);
                List<Entry> pendingEntries = _ORM.SelectMany<Entry>(null, null, e3, ro);

                if (pendingEntries != null && pendingEntries.Count > 0)
                {
                    foreach (Entry entry in pendingEntries)
                    {
                        if (entry.Type == EntryType.Balance) continue;
                        else if (entry.Type == EntryType.Credit)
                        {
                            balance.PendingCredits.Count++;
                            balance.PendingCredits.Total += entry.Amount;
                            balance.PendingCredits.Entries.Add(entry);
                        }
                        else if (entry.Type == EntryType.Debit)
                        {
                            balance.PendingDebits.Count++;
                            balance.PendingDebits.Total += entry.Amount;
                            balance.PendingDebits.Entries.Add(entry);
                        }
                    }
                }

                balance.PendingBalance = balance.CommittedBalance - balance.PendingDebits.Total + balance.PendingCredits.Total;
                return balance;
            }
            finally
            {
                if (applyLock) UnlockAccount(accountGuid);
            }
        }

        /// <summary>
        /// Retrieve a list of pending entries for a given account.
        /// </summary>
        /// <param name="accountGuid">GUID of the account.</param>
        /// <returns>List of pending entries.</returns>
        public List<Entry> GetPendingEntries(string accountGuid)
        {
            if (String.IsNullOrEmpty(accountGuid)) throw new ArgumentNullException(nameof(accountGuid));
            DbExpression e1 = new DbExpression(_ORM.GetColumnName<Account>(nameof(Account.GUID)), DbOperators.Equals, accountGuid);
            Account a = _ORM.SelectFirst<Account>(e1);
            if (a == null) throw new KeyNotFoundException("Unable to find account with GUID " + accountGuid + ".");

            try
            {
                LockAccount(accountGuid);
                DbExpression e2 = new DbExpression(_ORM.GetColumnName<Entry>(nameof(Entry.AccountGUID)), DbOperators.Equals, accountGuid);
                e2.PrependAnd(_ORM.GetColumnName<Entry>(nameof(Entry.IsCommitted)), DbOperators.Equals, false);
                e2.PrependAnd(_ORM.GetColumnName<Entry>(nameof(Entry.CommittedUtc)), DbOperators.IsNull, null);

                DbResultOrder[] ro = new DbResultOrder[1];
                ro[0] = new DbResultOrder(_ORM.GetColumnName<Entry>(nameof(Entry.CreatedUtc)), DbOrderDirection.Descending);
                return _ORM.SelectMany<Entry>(null, null, e2, ro);
            }
            finally
            {
                UnlockAccount(accountGuid);
            }
        }

        /// <summary>
        /// Retrieve a list of pending credit entries for a given account.
        /// </summary>
        /// <param name="accountGuid">GUID of the account.</param>
        /// <returns>List of pending credit entries.</returns>
        public List<Entry> GetPendingCredits(string accountGuid)
        {
            if (String.IsNullOrEmpty(accountGuid)) throw new ArgumentNullException(nameof(accountGuid));
            DbExpression e1 = new DbExpression(_ORM.GetColumnName<Account>(nameof(Account.GUID)), DbOperators.Equals, accountGuid);
            Account a = _ORM.SelectFirst<Account>(e1);
            if (a == null) throw new KeyNotFoundException("Unable to find account with GUID " + accountGuid + ".");

            try
            {
                LockAccount(accountGuid);
                DbExpression e2 = new DbExpression(_ORM.GetColumnName<Entry>(nameof(Entry.AccountGUID)), DbOperators.Equals, accountGuid);
                e2.PrependAnd(_ORM.GetColumnName<Entry>(nameof(Entry.IsCommitted)), DbOperators.Equals, false);
                e2.PrependAnd(_ORM.GetColumnName<Entry>(nameof(Entry.CommittedUtc)), DbOperators.IsNull, null);
                e2.PrependAnd(_ORM.GetColumnName<Entry>(nameof(Entry.Type)), DbOperators.Equals, EntryType.Credit);

                DbResultOrder[] ro = new DbResultOrder[1];
                ro[0] = new DbResultOrder(_ORM.GetColumnName<Entry>(nameof(Entry.CreatedUtc)), DbOrderDirection.Descending);
                return _ORM.SelectMany<Entry>(null, null, e2, ro);
            }
            finally
            {
                UnlockAccount(accountGuid);
            }
        }

        /// <summary>
        /// Retrieve a list of pending debit entries for a given account.
        /// </summary>
        /// <param name="accountGuid">GUID of the account.</param>
        /// <returns>List of pending debit entries.</returns>
        public List<Entry> GetPendingDebits(string accountGuid)
        {
            if (String.IsNullOrEmpty(accountGuid)) throw new ArgumentNullException(nameof(accountGuid));
            DbExpression e1 = new DbExpression(_ORM.GetColumnName<Account>(nameof(Account.GUID)), DbOperators.Equals, accountGuid);
            Account a = _ORM.SelectFirst<Account>(e1);
            if (a == null) throw new KeyNotFoundException("Unable to find account with GUID " + accountGuid + ".");

            try
            {
                LockAccount(accountGuid);
                DbExpression e2 = new DbExpression(_ORM.GetColumnName<Entry>(nameof(Entry.AccountGUID)), DbOperators.Equals, accountGuid);
                e2.PrependAnd(_ORM.GetColumnName<Entry>(nameof(Entry.IsCommitted)), DbOperators.Equals, false);
                e2.PrependAnd(_ORM.GetColumnName<Entry>(nameof(Entry.CommittedUtc)), DbOperators.IsNull, null);
                e2.PrependAnd(_ORM.GetColumnName<Entry>(nameof(Entry.Type)), DbOperators.Equals, EntryType.Debit);

                DbResultOrder[] ro = new DbResultOrder[1];
                ro[0] = new DbResultOrder(_ORM.GetColumnName<Entry>(nameof(Entry.CreatedUtc)), DbOrderDirection.Descending);
                return _ORM.SelectMany<Entry>(null, null, e2, ro);
            }
            finally
            {
                UnlockAccount(accountGuid);
            }
        }

        /// <summary>
        /// Retrieve a list of entries matching the specified conditions.
        /// </summary>
        /// <param name="accountGuid">GUID of the account.</param>
        /// <param name="startTimeUtc">Start time UTC.</param>
        /// <param name="endTimeUtc">End time UTC.</param>
        /// <param name="searchTerm">Search term that must appear in the entry description.</param>
        /// <param name="entryType">The type of entry.</param>
        /// <param name="amountMin">Minimum amount.</param>
        /// <param name="amountMax">Maximum amount.</param>
        /// <returns>List of matching entries.</returns>
        public List<Entry> GetEntries(string accountGuid, 
            DateTime? startTimeUtc = null, 
            DateTime? endTimeUtc = null, 
            string searchTerm = null, 
            EntryType? entryType = null,
            decimal? amountMin = null,
            decimal? amountMax = null)
        {
            if (startTimeUtc != null && endTimeUtc != null)
            {
                if (DateTime.Compare(Convert.ToDateTime(endTimeUtc), Convert.ToDateTime(startTimeUtc)) < 0)
                {
                    throw new ArgumentException("Specified end time must be later than the specified start time.");
                }
            }

            if (amountMin != null && amountMin.Value < 0) throw new ArgumentException("Minimum amount must be zero or greater.");
            if (amountMax != null && amountMax.Value < 0) throw new ArgumentException("Maximum amount must be zero or greater.");

            if (String.IsNullOrEmpty(accountGuid)) throw new ArgumentNullException(nameof(accountGuid));
            DbExpression e1 = new DbExpression(_ORM.GetColumnName<Account>(nameof(Account.GUID)), DbOperators.Equals, accountGuid);
            Account a = _ORM.SelectFirst<Account>(e1);
            if (a == null) throw new KeyNotFoundException("Unable to find account with GUID " + accountGuid + ".");

            try
            {
                LockAccount(accountGuid);
                DbExpression e2 = new DbExpression(_ORM.GetColumnName<Entry>(nameof(Entry.AccountGUID)), DbOperators.Equals, accountGuid);
                if (startTimeUtc != null) e2.PrependAnd(_ORM.GetColumnName<Entry>(nameof(Entry.CreatedUtc)), DbOperators.GreaterThanOrEqualTo, startTimeUtc.Value);
                if (endTimeUtc != null) e2.PrependAnd(_ORM.GetColumnName<Entry>(nameof(Entry.CreatedUtc)), DbOperators.LessThanOrEqualTo, endTimeUtc.Value);
                if (!String.IsNullOrEmpty(searchTerm)) e2.PrependAnd(_ORM.GetColumnName<Entry>(nameof(Entry.Description)), DbOperators.Contains, searchTerm);
                if (amountMin != null) e2.PrependAnd(_ORM.GetColumnName<Entry>(nameof(Entry.Amount)), DbOperators.GreaterThanOrEqualTo, amountMin.Value);
                if (amountMax != null) e2.PrependAnd(_ORM.GetColumnName<Entry>(nameof(Entry.Amount)), DbOperators.LessThanOrEqualTo, amountMax.Value);

                DbResultOrder[] ro = new DbResultOrder[1];
                ro[0] = new DbResultOrder(_ORM.GetColumnName<Entry>(nameof(Entry.CreatedUtc)), DbOrderDirection.Descending);
                return _ORM.SelectMany<Entry>(null, null, e2, ro);
            }
            finally
            {
                UnlockAccount(accountGuid);
            }
        }

        #endregion

        #region Public-Ledgering-Methods

        /// <summary>
        /// Commit pending entries to the balance.  
        /// Specify entries to commit using the guids property, or leave it null to commit all pending entries.
        /// </summary>
        /// <param name="accountGuid">GUID of the account.</param>
        /// <param name="guids">List of entry GUIDs to commit.  Leave null to commit all pending entries.</param>
        /// <returns>Balance details.</returns>
        public Balance CommitEntries(string accountGuid, List<string> guids = null)
        {
            if (String.IsNullOrEmpty(accountGuid)) throw new ArgumentNullException(nameof(accountGuid));

            Account account = null;
            Balance balanceBefore = null;
            Balance balanceAfter = null;
            List<string> summarized = new List<string>();

            try
            {
                LockAccount(accountGuid);

                // get account
                account = GetAccountByGuid(accountGuid);

                // get current balance
                balanceBefore = GetBalance(accountGuid, false);
                 
                // get old balance entry
                DbExpression e1 = new DbExpression(_ORM.GetColumnName<Entry>(nameof(Entry.AccountGUID)), DbOperators.Equals, accountGuid);
                e1.PrependAnd(_ORM.GetColumnName<Entry>(nameof(Entry.Type)), DbOperators.Equals, EntryType.Balance);

                DbResultOrder[] ro = new DbResultOrder[1];
                ro[0] = new DbResultOrder(_ORM.GetColumnName<Entry>(nameof(Entry.CreatedUtc)), DbOrderDirection.Descending);

                List<Entry> previousBalanceEntries = _ORM.SelectMany<Entry>(null, 1, e1, ro);
                if (previousBalanceEntries == null || previousBalanceEntries.Count != 1) throw new InvalidOperationException("No balance entry found for account with GUID " + accountGuid + ".");
                Entry balanceOld = previousBalanceEntries[0]; 

                // validate requested GUIDs
                if (guids != null && guids.Count > 0)
                {
                    DbExpression e3 = new DbExpression(_ORM.GetColumnName<Entry>(nameof(Entry.AccountGUID)), DbOperators.Equals, accountGuid);
                    e3.PrependAnd(_ORM.GetColumnName<Entry>(nameof(Entry.GUID)), DbOperators.In, guids);
                    List<Entry> requestedEntries = _ORM.SelectMany<Entry>(e3);
                    if (requestedEntries == null || requestedEntries.Count < 1) throw new KeyNotFoundException("One or more requested entries to commit were not found.");

                    foreach (Entry entry in requestedEntries)
                    {
                        if (entry.Type != EntryType.Debit && entry.Type != EntryType.Credit)
                        {
                            throw new InvalidOperationException("Commit cannot be performed on entry with GUID " + entry.GUID + " because it is not of type Debit or Credit.");
                        }

                        if (entry.IsCommitted)
                        {
                            throw new InvalidOperationException("Commit cannot be performed on entry with GUID " + entry.GUID + " because it is already committed.");
                        }

                        if (entry.CommittedUtc != null)
                        {
                            throw new InvalidOperationException("Commit cannot be performed on entry with GUID " + entry.GUID + " because it already contains a committed timestamp.");
                        }
                    }

                    foreach (string guid in guids)
                    {
                        if (!requestedEntries.Any(e => e.GUID.Equals(guid)))
                        {
                            throw new KeyNotFoundException("No entry found with GUID " + guid + ".");
                        }
                    }
                }
                 
                // commit credits
                decimal committedCreditsTotal = 0m;
                if (balanceBefore.PendingCredits.Entries != null && balanceBefore.PendingCredits.Entries.Count > 0)
                {
                    foreach (Entry entry in balanceBefore.PendingCredits.Entries)
                    {
                        if (guids != null && guids.Count > 0 && !guids.Contains(entry.GUID)) continue;
                        summarized.Add(entry.GUID);
                        entry.IsCommitted = true;
                        entry.CommittedUtc = DateTime.Now.ToUniversalTime(); 
                        _ORM.Update<Entry>(entry);
                        committedCreditsTotal += entry.Amount;
                    }
                }

                // commit debits
                decimal committedDebitsTotal = 0m;
                if (balanceBefore.PendingDebits.Entries != null && balanceBefore.PendingDebits.Entries.Count > 0)
                {
                    foreach (Entry entry in balanceBefore.PendingDebits.Entries)
                    {
                        if (guids != null && guids.Count > 0 && !guids.Contains(entry.GUID)) continue;
                        summarized.Add(entry.GUID);
                        entry.IsCommitted = true;
                        entry.CommittedUtc = DateTime.Now.ToUniversalTime(); 
                        _ORM.Update<Entry>(entry);
                        committedDebitsTotal += entry.Amount;
                    }
                }

                // create new balance entry
                Entry balanceNew = new Entry();
                balanceNew.GUID = Guid.NewGuid().ToString();
                balanceNew.AccountGUID = accountGuid;
                balanceNew.Type = EntryType.Balance;
                balanceNew.Amount = balanceBefore.CommittedBalance + committedCreditsTotal - committedDebitsTotal;
                balanceNew.Description = "Summarized balance after committing pending entries";
                balanceNew.SummarizedGUIDs = Common.StringListToCsv(summarized);
                balanceNew.IsCommitted = true;
                balanceNew.Replaces = balanceOld.GUID;
                DateTime ts = DateTime.Now.ToUniversalTime();
                balanceNew.CreatedUtc = ts;
                balanceNew.CommittedUtc = ts;
                balanceNew = _ORM.Insert<Entry>(balanceNew);

                balanceAfter = GetBalance(accountGuid, false);

                // return new balance
                return balanceAfter;
            }
            finally
            {
                UnlockAccount(accountGuid);
                if (balanceBefore != null && balanceAfter != null)
                    Task.Run(() => EntriesCommitted?.Invoke(this, new CommitEventArgs(account, balanceBefore, balanceAfter, summarized)));
            }
        }

        #endregion

        #region Private-Methods

        private void LockAccount(string accountGuid)
        {
            while (!_LockedAccounts.TryAdd(accountGuid, DateTime.Now.ToUniversalTime()))
            {
                Task.Delay(100).Wait();
            }
        }

        private void UnlockAccount(string accountGuid)
        {
            _LockedAccounts.TryRemove(accountGuid, out _);
        }

        #endregion
    }
}

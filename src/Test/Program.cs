using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GetSomeInput;
using NetLedger;

namespace Test
{
    class Program
    {
        static string _Filename = null;
        static Ledger _Ledger = null;
        static bool _RunForever = true;
        static Guid? _LastAccountGuid = null;

        static async Task Main(string[] args)
        {
            Console.WriteLine("");
            Console.WriteLine("NetLedger");
            Console.WriteLine("");
            _Filename = Inputty.GetString("Database file:", "netledger.db", false);
            _Ledger = new Ledger(_Filename);
            
            _Ledger.CreditAdded += CreditAddedEvent;
            _Ledger.DebitAdded += DebitAddedEvent;
            _Ledger.EntryCanceled += EntryCanceledEvent;
            _Ledger.AccountCreated += AccountCreatedEvent;
            _Ledger.AccountDeleted += AccountDeletedEvent;
            _Ledger.EntriesCommitted += EntriesCommittedEvent;

            Console.WriteLine("");

            while (_RunForever)
            {
                string cmd = Inputty.GetString("Command [? for help]:", null, false);
                string subCmd = null;

                if (cmd.Equals("q"))
                {
                    _RunForever = false;
                }
                else if (cmd.Equals("?"))
                {
                    Menu();
                }
                else if (cmd.Equals("c") || cmd.Equals("cls"))
                {
                    Console.Clear();
                }
                else if (cmd.StartsWith("acct "))
                {
                    subCmd = cmd.Substring(5);
                    if (subCmd.Equals("all"))
                    {
                        await AccountAll();
                    }
                    else if (subCmd.Equals("add"))
                    {
                        await AccountAdd();
                    }
                    else if (subCmd.Equals("by name"))
                    {
                        await AccountByName();
                    }
                    else if (subCmd.Equals("by guid"))
                    {
                        await AccountByGuid();
                    }
                    else if (subCmd.Equals("del by name"))
                    {
                        await AccountDeleteByName();
                    }
                    else if (subCmd.Equals("del by guid"))
                    {
                        await AccountDeleteByGuid();
                    }
                    else if (subCmd.Equals("balance"))
                    {
                        await AccountBalance();
                    }
                    else if (subCmd.Equals("commit"))
                    {
                        await AccountCommit();
                    }
                }
                else if (cmd.StartsWith("credit "))
                {
                    subCmd = cmd.Substring(7);
                    if (subCmd.Equals("add"))
                    {
                        await CreditAdd();
                    }
                    else if (subCmd.Equals("pending"))
                    {
                        await CreditsPending();
                    }
                }
                else if (cmd.StartsWith("debit "))
                {
                    subCmd = cmd.Substring(6);
                    if (subCmd.Equals("add"))
                    {
                        await DebitAdd();
                    }
                    else if (subCmd.Equals("pending"))
                    {
                        await DebitsPending();
                    }
                }
                else if (cmd.StartsWith("entry "))
                {
                    subCmd = cmd.Substring(6);
                    if (subCmd.Equals("pending"))
                    {
                        await EntriesPending();
                    }
                    else if (subCmd.Equals("search"))
                    {
                        await EntrySearch();
                    }
                    else if (subCmd.Equals("cancel"))
                    {
                        await EntryCancel();
                    }
                }
                else if (cmd.Equals("enum"))
                {
                    await EnumerateTransactions();
                }
            }
        }

        static void Menu()
        {
            Console.WriteLine("--- Available Commands ---");
            Console.WriteLine("");
            Console.WriteLine(" General");
            Console.WriteLine(" q                   quit");
            Console.WriteLine(" ?                   help, this menu");
            Console.WriteLine(" cls                 clear the screen");
            Console.WriteLine("");
            Console.WriteLine(" Accounts, i.e. acct [command]");
            Console.WriteLine(" acct all            list all accounts, or search by name");
            Console.WriteLine("      add            create an account");
            Console.WriteLine("      by name        retrieve an account by name");
            Console.WriteLine("      by guid        retrieve an account by GUID");
            Console.WriteLine("      del by name    delete an account by name");
            Console.WriteLine("      del by guid    delete an account by GUID");
            Console.WriteLine("      balance        retrieve an account balance");
            Console.WriteLine("      commit         commit pending entries to the balance");
            Console.WriteLine("");
            Console.WriteLine(" Credits, i.e. credit [command]");
            Console.WriteLine(" credit add          add a credit to an account");
            Console.WriteLine("        pending      list pending credits");
            Console.WriteLine("");
            Console.WriteLine(" Debits, i.e. debit [command]");
            Console.WriteLine(" debit add           add a debit to an account");
            Console.WriteLine("       pending       list pending debits");
            Console.WriteLine("");
            Console.WriteLine(" Entries, i.e. entry [command]");
            Console.WriteLine(" entry pending       list pending entries");
            Console.WriteLine("       search        search entries");
            Console.WriteLine("       cancel        cancel a pending entry");
            Console.WriteLine("");
            Console.WriteLine(" Enumeration");
            Console.WriteLine(" enum                enumerate transactions (paginated)");
            Console.WriteLine("");
        }

        #region Account-APIs

        static async Task AccountAll()
        {
            Console.Write("Search term: ");
            string searchTerm = Console.ReadLine();
            List<Account> accounts = await _Ledger.GetAllAccountsAsync(searchTerm);
            if (accounts == null || accounts.Count < 1) Console.WriteLine("(none)");
            else Console.WriteLine(SerializationHelper.SerializeJson(accounts, true));
        }

        static async Task AccountAdd()
        {
            string name = Inputty.GetString("Name:", null, true);
            if (!String.IsNullOrEmpty(name))
            {
                Guid guid = await _Ledger.CreateAccountAsync(name);
                _LastAccountGuid = guid;
                Console.WriteLine(guid);
            }
        }

        static async Task AccountByName()
        {
            string name = Inputty.GetString("Name:", null, true);
            if (!String.IsNullOrEmpty(name))
            {
                Account a = await _Ledger.GetAccountByNameAsync(name);
                if (a != null)
                {
                    _LastAccountGuid = a.GUID;
                    Console.WriteLine(SerializationHelper.SerializeJson(a, true));
                }
                else Console.WriteLine("(none)");
            }
        }

        static async Task AccountByGuid()
        {
            Guid guid = Inputty.GetGuid("GUID:", _LastAccountGuid ?? Guid.Empty);
            if (guid != Guid.Empty)
            {
                Account a = await _Ledger.GetAccountByGuidAsync(guid);
                if (a != null)
                {
                    _LastAccountGuid = a.GUID;
                    Console.WriteLine(SerializationHelper.SerializeJson(a, true));
                }
                else Console.WriteLine("(none)");
            }
        }

        static async Task AccountDeleteByName()
        {
            string name = Inputty.GetString("Name:", null, true);
            if (!String.IsNullOrEmpty(name))
            {
                await _Ledger.DeleteAccountByNameAsync(name);
            }
        }

        static async Task AccountDeleteByGuid()
        {
            Guid guid = Inputty.GetGuid("GUID:", _LastAccountGuid ?? Guid.Empty);
            if (guid != Guid.Empty)
            {
                await _Ledger.DeleteAccountByGuidAsync(guid);
            }
        }

        static async Task AccountBalance()
        {
            Guid guid = Inputty.GetGuid("GUID:", _LastAccountGuid ?? Guid.Empty);
            if (guid != Guid.Empty)
            {
                Balance b = await _Ledger.GetBalanceAsync(guid);
                if (b != null) Console.WriteLine(SerializationHelper.SerializeJson(b, true));
                else Console.WriteLine("(none)");
            }
        }

        static async Task AccountCommit()
        {
            Guid guid = Inputty.GetGuid("GUID:", _LastAccountGuid ?? Guid.Empty);
            if (guid != Guid.Empty)
            {
                List<Guid> entries = new List<Guid>();
                while (true)
                {
                    string entryGuidStr = Inputty.GetString("Entry GUID (leave blank to finish):", null, true);
                    if (String.IsNullOrEmpty(entryGuidStr)) break;
                    if (Guid.TryParse(entryGuidStr, out Guid entryGuid))
                    {
                        entries.Add(entryGuid);
                    }
                    else
                    {
                        Console.WriteLine("Invalid GUID format, please try again.");
                    }
                }
                Balance b = await _Ledger.CommitEntriesAsync(guid, entries.Count > 0 ? entries : null);
                if (b != null) Console.WriteLine(SerializationHelper.SerializeJson(b, true));
                else Console.WriteLine("(none)");
            }
        }

        #endregion

        #region Credit-APIs

        static async Task CreditAdd()
        {
            Guid guid = Inputty.GetGuid("GUID:", _LastAccountGuid ?? Guid.Empty);
            if (guid != Guid.Empty)
            {
                decimal amount = Inputty.GetDecimal("Amount:", 1m, true, true);
                string notes = Inputty.GetString("Notes:", null, true);
                Guid? summarizedBy = null;
                string summarizedByStr = Inputty.GetString("Summarized By (leave blank for none):", null, true);
                if (!String.IsNullOrEmpty(summarizedByStr) && Guid.TryParse(summarizedByStr, out Guid parsedGuid))
                {
                    summarizedBy = parsedGuid;
                }
                bool isCommitted = Inputty.GetBoolean("Already Committed", false);
                Guid entryGuid = await _Ledger.AddCreditAsync(guid, amount, notes, summarizedBy, isCommitted);
                Console.WriteLine(entryGuid);
            }
        }

        static async Task CreditsPending()
        {
            Guid guid = Inputty.GetGuid("GUID:", _LastAccountGuid ?? Guid.Empty);
            if (guid != Guid.Empty)
            {
                List<Entry> entries = await _Ledger.GetPendingCreditsAsync(guid);
                if (entries != null && entries.Count > 0) Console.WriteLine(SerializationHelper.SerializeJson(entries, true));
                else Console.WriteLine("(none)");
            }
        }

        #endregion

        #region Debit-APIs

        static async Task DebitAdd()
        {
            Guid guid = Inputty.GetGuid("GUID:", _LastAccountGuid ?? Guid.Empty);
            if (guid != Guid.Empty)
            {
                decimal amount = Inputty.GetDecimal("Amount:", 1m, true, true);
                string notes = Inputty.GetString("Notes:", null, true);
                Guid? summarizedBy = null;
                string summarizedByStr = Inputty.GetString("Summarized By (leave blank for none):", null, true);
                if (!String.IsNullOrEmpty(summarizedByStr) && Guid.TryParse(summarizedByStr, out Guid parsedGuid))
                {
                    summarizedBy = parsedGuid;
                }
                bool isCommitted = Inputty.GetBoolean("Already Committed", false);
                Guid entryGuid = await _Ledger.AddDebitAsync(guid, amount, notes, summarizedBy, isCommitted);
                Console.WriteLine(entryGuid);
            }
        }

        static async Task DebitsPending()
        {
            Guid guid = Inputty.GetGuid("GUID:", _LastAccountGuid ?? Guid.Empty);
            if (guid != Guid.Empty)
            {
                List<Entry> entries = await _Ledger.GetPendingDebitsAsync(guid);
                if (entries != null && entries.Count > 0) Console.WriteLine(SerializationHelper.SerializeJson(entries, true));
                else Console.WriteLine("(none)");
            }
        }

        #endregion

        #region Entry-APIs

        static async Task EntriesPending()
        {
            Guid guid = Inputty.GetGuid("GUID:", _LastAccountGuid ?? Guid.Empty);
            if (guid != Guid.Empty)
            {
                List<Entry> entries = await _Ledger.GetPendingEntriesAsync(guid);
                if (entries != null && entries.Count > 0) Console.WriteLine(SerializationHelper.SerializeJson(entries, true));
                else Console.WriteLine("(none)");
            }
        }

        static async Task EntrySearch()
        {
            Guid guid = Inputty.GetGuid("GUID:", _LastAccountGuid ?? Guid.Empty);
            if (guid != Guid.Empty)
            {
                string startDateStr = Inputty.GetString("Start date:", null, true);
                string endDateStr = Inputty.GetString("End date:", null, true);
                DateTime? startDate = null;
                DateTime? endDate = null;
                if (!String.IsNullOrEmpty(startDateStr)) startDate = Convert.ToDateTime(startDateStr);
                if (!String.IsNullOrEmpty(endDateStr)) endDate = Convert.ToDateTime(endDateStr);

                string searchTerm = Inputty.GetString("Search term:", null, true);

                string minAmountStr = Inputty.GetString("Minimum amount:", null, true);
                string maxAmountStr = Inputty.GetString("Maximum amount:", null, true);
                decimal? minAmount = null;
                decimal? maxAmount = null;
                if (!String.IsNullOrEmpty(minAmountStr)) minAmount = Convert.ToDecimal(minAmountStr);
                if (!String.IsNullOrEmpty(maxAmountStr)) maxAmount = Convert.ToDecimal(maxAmountStr);

                List<Entry> entries = await _Ledger.GetEntriesAsync(guid, startDate, endDate, searchTerm, null, minAmount, maxAmount);
                if (entries != null && entries.Count > 0) Console.WriteLine(SerializationHelper.SerializeJson(entries, true));
                else Console.WriteLine("(none)");
            }
        }

        static async Task EntryCancel()
        {
            Guid acctGuid = Inputty.GetGuid("Account GUID:", _LastAccountGuid ?? Guid.Empty);
            if (acctGuid != Guid.Empty)
            {
                Guid entryGuid = Inputty.GetGuid("Entry GUID:", Guid.Empty);
                if (entryGuid != Guid.Empty)
                {
                    await _Ledger.CancelPendingAsync(acctGuid, entryGuid);
                }
            }
        }

        static async Task EnumerateTransactions()
        {
            Guid acctGuid = Inputty.GetGuid("Account GUID:", _LastAccountGuid ?? Guid.Empty);
            if (acctGuid != Guid.Empty)
            {
                int maxResults = Inputty.GetInteger("MaxResults:", 10, true, false);
                int skip = Inputty.GetInteger("Skip:", 0, true, false);

                Console.WriteLine("Ordering:");
                Console.WriteLine("  0 = CreatedAscending");
                Console.WriteLine("  1 = CreatedDescending");
                Console.WriteLine("  2 = AmountAscending");
                Console.WriteLine("  3 = AmountDescending");
                int orderingInt = Inputty.GetInteger("Ordering:", 1, true, false);
                EnumerationOrderEnum ordering = (EnumerationOrderEnum)orderingInt;

                string createdAfterStr = Inputty.GetString("Created After (UTC, leave blank for none):", null, true);
                DateTime? createdAfter = null;
                if (!String.IsNullOrEmpty(createdAfterStr)) createdAfter = Convert.ToDateTime(createdAfterStr);

                string createdBeforeStr = Inputty.GetString("Created Before (UTC, leave blank for none):", null, true);
                DateTime? createdBefore = null;
                if (!String.IsNullOrEmpty(createdBeforeStr)) createdBefore = Convert.ToDateTime(createdBeforeStr);

                string amountMinimumStr = Inputty.GetString("Amount Minimum (leave blank for none):", null, true);
                decimal? amountMinimum = null;
                if (!String.IsNullOrEmpty(amountMinimumStr)) amountMinimum = Convert.ToDecimal(amountMinimumStr);

                string amountMaximumStr = Inputty.GetString("Amount Maximum (leave blank for none):", null, true);
                decimal? amountMaximum = null;
                if (!String.IsNullOrEmpty(amountMaximumStr)) amountMaximum = Convert.ToDecimal(amountMaximumStr);

                EnumerationQuery query = new EnumerationQuery
                {
                    AccountGUID = acctGuid,
                    MaxResults = maxResults,
                    Skip = skip,
                    Ordering = ordering,
                    CreatedAfterUtc = createdAfter,
                    CreatedBeforeUtc = createdBefore,
                    AmountMinimum = amountMinimum,
                    AmountMaximum = amountMaximum
                };

                EnumerationResult<Entry> result = await _Ledger.EnumerateTransactionsAsync(query);

                Console.WriteLine("");
                Console.WriteLine("--- Enumeration Result ---");
                Console.WriteLine("Total Records:      " + result.TotalRecords);
                Console.WriteLine("Records Remaining:  " + result.RecordsRemaining);
                Console.WriteLine("End of Results:     " + result.EndOfResults);
                Console.WriteLine("Continuation Token: " + (result.ContinuationToken.HasValue ? result.ContinuationToken.Value.ToString() : "(none)"));
                Console.WriteLine("Max Results:        " + result.MaxResults);
                Console.WriteLine("Skip:               " + result.Skip);
                Console.WriteLine("");
                Console.WriteLine("--- Entries (" + (result.Objects != null ? result.Objects.Count : 0) + ") ---");
                if (result.Objects != null && result.Objects.Count > 0)
                {
                    Console.WriteLine(SerializationHelper.SerializeJson(result.Objects, true));
                }
                else
                {
                    Console.WriteLine("(none)");
                }
            }
        }

        #endregion

        #region Events

        static void CreditAddedEvent(object sender, EntryEventArgs args)
        {
            Console.WriteLine("");
            Console.WriteLine("Credit added event:" + Environment.NewLine + SerializationHelper.SerializeJson(args, true));
            Console.WriteLine("");
        }

        static void DebitAddedEvent(object sender, EntryEventArgs args)
        {
            Console.WriteLine("");
            Console.WriteLine("Debit added event:" + Environment.NewLine + SerializationHelper.SerializeJson(args, true));
            Console.WriteLine("");
        }

        static void EntryCanceledEvent(object sender, EntryEventArgs args)
        {
            Console.WriteLine("");
            Console.WriteLine("Entry canceled event:" + Environment.NewLine + SerializationHelper.SerializeJson(args, true));
            Console.WriteLine("");
        }

        static void AccountCreatedEvent(object sender, AccountEventArgs args)
        {
            Console.WriteLine("");
            Console.WriteLine("Account created event:" + Environment.NewLine + SerializationHelper.SerializeJson(args, true));
            Console.WriteLine("");
        }

        static void AccountDeletedEvent(object sender, AccountEventArgs args)
        {
            Console.WriteLine("");
            Console.WriteLine("Account deleted event:" + Environment.NewLine + SerializationHelper.SerializeJson(args, true));
            Console.WriteLine("");
        }

        static void EntriesCommittedEvent(object sender, CommitEventArgs args)
        {
            Console.WriteLine("");
            Console.WriteLine("Entries committed event:" + Environment.NewLine + SerializationHelper.SerializeJson(args, true));
            Console.WriteLine("");
        }

        #endregion
    }
}

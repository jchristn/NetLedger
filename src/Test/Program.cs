using System;
using System.Collections.Generic;
using GetSomeInput;
using NetLedger;

namespace Test
{
    class Program
    {
        static string _Filename = null;
        static Ledger _Ledger = null;
        static bool _RunForever = true; 
        static string _LastAccountGuid = null; 

        static void Main(string[] args)
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
                        AccountAll();
                    }
                    else if (subCmd.Equals("add"))
                    {
                        AccountAdd();
                    }
                    else if (subCmd.Equals("by name"))
                    {
                        AccountByName();
                    }
                    else if (subCmd.Equals("by guid"))
                    {
                        AccountByGuid();
                    }
                    else if (subCmd.Equals("del by name"))
                    {
                        AccountDeleteByName();
                    }
                    else if (subCmd.Equals("del by guid"))
                    {
                        AccountDeleteByGuid();
                    }
                    else if (subCmd.Equals("balance"))
                    {
                        AccountBalance();
                    }
                    else if (subCmd.Equals("commit"))
                    {
                        AccountCommit();
                    } 
                }
                else if (cmd.StartsWith("credit "))
                {
                    subCmd = cmd.Substring(7);
                    if (subCmd.Equals("add"))
                    {
                        CreditAdd();
                    }
                    else if (subCmd.Equals("pending"))
                    {
                        CreditsPending();
                    } 
                }
                else if (cmd.StartsWith("debit "))
                {
                    subCmd = cmd.Substring(6);
                    if (subCmd.Equals("add"))
                    {
                        DebitAdd();
                    }
                    else if (subCmd.Equals("pending"))
                    {
                        DebitsPending();
                    }
                }
                else if (cmd.StartsWith("entry "))
                {
                    subCmd = cmd.Substring(6);
                    if (subCmd.Equals("pending"))
                    {
                        EntriesPending();
                    }
                    else if (subCmd.Equals("search"))
                    {
                        EntrySearch();
                    } 
                    else if (subCmd.Equals("cancel"))
                    {
                        EntryCancel();
                    }
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
        }

        #region Account-APIs

        static void AccountAll()
        {
            Console.Write("Search term: ");
            string searchTerm = Console.ReadLine();
            List<Account> accounts = _Ledger.GetAllAccounts(searchTerm);
            if (accounts == null || accounts.Count < 1) Console.WriteLine("(none)");
            else Console.WriteLine(SerializationHelper.SerializeJson(accounts, true));
        }

        static void AccountAdd()
        {
            string name = Inputty.GetString("Name:", null, true);
            if (!String.IsNullOrEmpty(name))
            {
                string guid = _Ledger.CreateAccount(name);
                _LastAccountGuid = guid;
                Console.WriteLine(guid);
            }
        }

        static void AccountByName()
        {
            string name = Inputty.GetString("Name:", null, true);
            if (!String.IsNullOrEmpty(name))
            {
                Account a = _Ledger.GetAccountByName(name);
                if (a != null)
                {
                    _LastAccountGuid = a.GUID;
                    Console.WriteLine(SerializationHelper.SerializeJson(a, true));
                }
                else Console.WriteLine("(none)");
            }
        }

        static void AccountByGuid()
        {
            string guid = Inputty.GetString("GUID:", _LastAccountGuid, true);
            if (!String.IsNullOrEmpty(guid))
            {
                Account a = _Ledger.GetAccountByGuid(guid);
                if (a != null)
                {
                    _LastAccountGuid = a.GUID;
                    Console.WriteLine(SerializationHelper.SerializeJson(a, true));
                }
                else Console.WriteLine("(none)");
            }
        }

        static void AccountDeleteByName()
        {
            string name = Inputty.GetString("Name:", null, true);
            if (!String.IsNullOrEmpty(name))
            {
                _Ledger.DeleteAccountByName(name);
            }
        }

        static void AccountDeleteByGuid()
        {
            string guid = Inputty.GetString("GUID:", _LastAccountGuid, true);
            if (!String.IsNullOrEmpty(guid))
            {
                _Ledger.DeleteAccountByGuid(guid);
            }
        }

        static void AccountBalance()
        {
            string guid = Inputty.GetString("GUID:", _LastAccountGuid, true);
            if (!String.IsNullOrEmpty(guid))
            {
                Balance b = _Ledger.GetBalance(guid);
                if (b != null) Console.WriteLine(SerializationHelper.SerializeJson(b, true));
                else Console.WriteLine("(none)");
            }
        }

        static void AccountCommit()
        {
            string guid = Inputty.GetString("GUID:", _LastAccountGuid, true);
            if (!String.IsNullOrEmpty(guid))
            {
                List<string> entries = Inputty.GetStringList("Entry GUID:", true);
                Balance b = _Ledger.CommitEntries(guid, entries); 
                if (b != null) Console.WriteLine(SerializationHelper.SerializeJson(b, true));
                else Console.WriteLine("(none)");
            }
        }

        #endregion

        #region Credit-APIs

        static void CreditAdd()
        {
            string guid = Inputty.GetString("GUID:", _LastAccountGuid, true);
            if (!String.IsNullOrEmpty(guid))
            {
                decimal amount = Inputty.GetDecimal("Amount:", 1m, true, true);
                string notes = Inputty.GetString("Notes:", null, true);
                string summarizedBy = Inputty.GetString("Summarized By:", null, true);
                bool isCommitted = Inputty.GetBoolean("Already Committed", false);
                string entryGuid = _Ledger.AddCredit(guid, amount, notes, summarizedBy, isCommitted);
                Console.WriteLine(entryGuid);
            }
        }

        static void CreditsPending()
        {
            string guid = Inputty.GetString("GUID:", _LastAccountGuid, true);
            if (!String.IsNullOrEmpty(guid))
            {
                List<Entry> entries = _Ledger.GetPendingCredits(guid);
                if (entries != null && entries.Count > 0) Console.WriteLine(SerializationHelper.SerializeJson(entries, true));
                else Console.WriteLine("(none)");
            }
        }

        #endregion

        #region Debit-APIs

        static void DebitAdd()
        {
            string guid = Inputty.GetString("GUID:", _LastAccountGuid, true);
            if (!String.IsNullOrEmpty(guid))
            {
                decimal amount = Inputty.GetDecimal("Amount:", 1m, true, true);
                string notes = Inputty.GetString("Notes:", null, true);
                string summarizedBy = Inputty.GetString("Summarized By:", null, true);
                bool isCommitted = Inputty.GetBoolean("Already Committed", false);
                string entryGuid = _Ledger.AddDebit(guid, amount, notes, summarizedBy, isCommitted);
                Console.WriteLine(entryGuid);
            }
        }

        static void DebitsPending()
        {
            string guid = Inputty.GetString("GUID:", _LastAccountGuid, true);
            if (!String.IsNullOrEmpty(guid))
            {
                List<Entry> entries = _Ledger.GetPendingDebits(guid);
                if (entries != null && entries.Count > 0) Console.WriteLine(SerializationHelper.SerializeJson(entries, true));
                else Console.WriteLine("(none)");
            }
        }

        #endregion

        #region Entry-APIs

        static void EntriesPending()
        {
            string guid = Inputty.GetString("GUID:", _LastAccountGuid, true);
            if (!String.IsNullOrEmpty(guid))
            {
                List<Entry> entries = _Ledger.GetPendingEntries(guid);
                if (entries != null && entries.Count > 0) Console.WriteLine(SerializationHelper.SerializeJson(entries, true));
                else Console.WriteLine("(none)");
            }
        }

        static void EntrySearch()
        {
            string guid = Inputty.GetString("GUID:", _LastAccountGuid, true);
            if (!String.IsNullOrEmpty(guid))
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

                List<Entry> entries = _Ledger.GetEntries(guid, startDate, endDate, searchTerm, null, minAmount, maxAmount);
                if (entries != null && entries.Count > 0) Console.WriteLine(SerializationHelper.SerializeJson(entries, true));
                else Console.WriteLine("(none)");
            }
        }

        static void EntryCancel()
        {
            string acctGuid = Inputty.GetString("Account GUID:", _LastAccountGuid, true);
            if (!String.IsNullOrEmpty(acctGuid))
            {
                string entryGuid = Inputty.GetString("Entry GUID:", null, true);
                if (!String.IsNullOrEmpty(entryGuid))
                {
                    _Ledger.CancelPending(acctGuid, entryGuid);
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

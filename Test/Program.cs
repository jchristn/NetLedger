using System;
using System.Collections.Generic;
using NetLedger;
using Newtonsoft.Json;

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
            _Filename = InputString("Database file:", "netledger.db", false);
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
                string cmd = InputString("Command [? for help]:", null, false);
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
            else Console.WriteLine(SerializeJson(accounts, true));
        }

        static void AccountAdd()
        {
            string name = InputString("Name:", null, true);
            if (!String.IsNullOrEmpty(name))
            {
                string guid = _Ledger.CreateAccount(name);
                _LastAccountGuid = guid;
                Console.WriteLine(guid);
            }
        }

        static void AccountByName()
        {
            string name = InputString("Name:", null, true);
            if (!String.IsNullOrEmpty(name))
            {
                Account a = _Ledger.GetAccountByName(name);
                if (a != null)
                {
                    _LastAccountGuid = a.GUID;
                    Console.WriteLine(SerializeJson(a, true));
                }
                else Console.WriteLine("(none)");
            }
        }

        static void AccountByGuid()
        {
            string guid = InputString("GUID:", _LastAccountGuid, true);
            if (!String.IsNullOrEmpty(guid))
            {
                Account a = _Ledger.GetAccountByGuid(guid);
                if (a != null)
                {
                    _LastAccountGuid = a.GUID;
                    Console.WriteLine(SerializeJson(a, true));
                }
                else Console.WriteLine("(none)");
            }
        }

        static void AccountDeleteByName()
        {
            string name = InputString("Name:", null, true);
            if (!String.IsNullOrEmpty(name))
            {
                _Ledger.DeleteAccountByName(name);
            }
        }

        static void AccountDeleteByGuid()
        {
            string guid = InputString("GUID:", _LastAccountGuid, true);
            if (!String.IsNullOrEmpty(guid))
            {
                _Ledger.DeleteAccountByGuid(guid);
            }
        }

        static void AccountBalance()
        {
            string guid = InputString("GUID:", _LastAccountGuid, true);
            if (!String.IsNullOrEmpty(guid))
            {
                Balance b = _Ledger.GetBalance(guid);
                if (b != null) Console.WriteLine(SerializeJson(b, true));
                else Console.WriteLine("(none)");
            }
        }

        static void AccountCommit()
        {
            string guid = InputString("GUID:", _LastAccountGuid, true);
            if (!String.IsNullOrEmpty(guid))
            {
                List<string> entries = InputStringList("Entry GUID:", true);
                Balance b = _Ledger.CommitEntries(guid, entries); 
                if (b != null) Console.WriteLine(SerializeJson(b, true));
                else Console.WriteLine("(none)");
            }
        }

        #endregion

        #region Credit-APIs

        static void CreditAdd()
        {
            string guid = InputString("GUID:", _LastAccountGuid, true);
            if (!String.IsNullOrEmpty(guid))
            {
                decimal amount = InputDecimal("Amount:", 1m, true, true);
                string notes = InputString("Notes:", null, true);
                string summarizedBy = InputString("Summarized By:", null, true);
                bool isCommitted = InputBoolean("Already Committed", false);
                string entryGuid = _Ledger.AddCredit(guid, amount, notes, summarizedBy, isCommitted);
                Console.WriteLine(entryGuid);
            }
        }

        static void CreditsPending()
        {
            string guid = InputString("GUID:", _LastAccountGuid, true);
            if (!String.IsNullOrEmpty(guid))
            {
                List<Entry> entries = _Ledger.GetPendingCredits(guid);
                if (entries != null && entries.Count > 0) Console.WriteLine(SerializeJson(entries, true));
                else Console.WriteLine("(none)");
            }
        }

        #endregion

        #region Debit-APIs

        static void DebitAdd()
        {
            string guid = InputString("GUID:", _LastAccountGuid, true);
            if (!String.IsNullOrEmpty(guid))
            {
                decimal amount = InputDecimal("Amount:", 1m, true, true);
                string notes = InputString("Notes:", null, true);
                string summarizedBy = InputString("Summarized By:", null, true);
                bool isCommitted = InputBoolean("Already Committed", false);
                string entryGuid = _Ledger.AddDebit(guid, amount, notes, summarizedBy, isCommitted);
                Console.WriteLine(entryGuid);
            }
        }

        static void DebitsPending()
        {
            string guid = InputString("GUID:", _LastAccountGuid, true);
            if (!String.IsNullOrEmpty(guid))
            {
                List<Entry> entries = _Ledger.GetPendingDebits(guid);
                if (entries != null && entries.Count > 0) Console.WriteLine(SerializeJson(entries, true));
                else Console.WriteLine("(none)");
            }
        }

        #endregion

        #region Entry-APIs

        static void EntriesPending()
        {
            string guid = InputString("GUID:", _LastAccountGuid, true);
            if (!String.IsNullOrEmpty(guid))
            {
                List<Entry> entries = _Ledger.GetPendingEntries(guid);
                if (entries != null && entries.Count > 0) Console.WriteLine(SerializeJson(entries, true));
                else Console.WriteLine("(none)");
            }
        }

        static void EntrySearch()
        {
            string guid = InputString("GUID:", _LastAccountGuid, true);
            if (!String.IsNullOrEmpty(guid))
            {
                string startDateStr = InputString("Start date:", null, true);
                string endDateStr = InputString("End date:", null, true);
                DateTime? startDate = null;
                DateTime? endDate = null;
                if (!String.IsNullOrEmpty(startDateStr)) startDate = Convert.ToDateTime(startDateStr);
                if (!String.IsNullOrEmpty(endDateStr)) endDate = Convert.ToDateTime(endDateStr);

                string searchTerm = InputString("Search term:", null, true);

                string minAmountStr = InputString("Minimum amount:", null, true);
                string maxAmountStr = InputString("Maximum amount:", null, true);
                decimal? minAmount = null;
                decimal? maxAmount = null;
                if (!String.IsNullOrEmpty(minAmountStr)) minAmount = Convert.ToDecimal(minAmountStr);
                if (!String.IsNullOrEmpty(maxAmountStr)) maxAmount = Convert.ToDecimal(maxAmountStr);

                List<Entry> entries = _Ledger.GetEntries(guid, startDate, endDate, searchTerm, null, minAmount, maxAmount);
                if (entries != null && entries.Count > 0) Console.WriteLine(SerializeJson(entries, true));
                else Console.WriteLine("(none)");
            }
        }

        static void EntryCancel()
        {
            string acctGuid = InputString("Account GUID:", _LastAccountGuid, true);
            if (!String.IsNullOrEmpty(acctGuid))
            {
                string entryGuid = InputString("Entry GUID:", null, true);
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
            Console.WriteLine("Credit added event:" + Environment.NewLine + SerializeJson(args, true));
            Console.WriteLine("");
        }

        static void DebitAddedEvent(object sender, EntryEventArgs args)
        {
            Console.WriteLine("");
            Console.WriteLine("Debit added event:" + Environment.NewLine + SerializeJson(args, true));
            Console.WriteLine("");
        }

        static void EntryCanceledEvent(object sender, EntryEventArgs args)
        {
            Console.WriteLine("");
            Console.WriteLine("Entry canceled event:" + Environment.NewLine + SerializeJson(args, true));
            Console.WriteLine("");
        }

        static void AccountCreatedEvent(object sender, AccountEventArgs args)
        {
            Console.WriteLine("");
            Console.WriteLine("Account created event:" + Environment.NewLine + SerializeJson(args, true));
            Console.WriteLine("");
        }

        static void AccountDeletedEvent(object sender, AccountEventArgs args)
        {
            Console.WriteLine("");
            Console.WriteLine("Account deleted event:" + Environment.NewLine + SerializeJson(args, true));
            Console.WriteLine("");
        }

        static void EntriesCommittedEvent(object sender, CommitEventArgs args)
        {
            Console.WriteLine("");
            Console.WriteLine("Entries committed event:" + Environment.NewLine + SerializeJson(args, true));
            Console.WriteLine("");
        }

        #endregion

        #region Misc

        static string SerializeJson(object obj, bool pretty)
        {
            if (obj == null) return null;
            string json;

            if (pretty)
            {
                json = JsonConvert.SerializeObject(
                  obj,
                  Newtonsoft.Json.Formatting.Indented,
                  new JsonSerializerSettings
                  {
                      NullValueHandling = NullValueHandling.Ignore,
                      DateTimeZoneHandling = DateTimeZoneHandling.Utc,
                  });
            }
            else
            {
                json = JsonConvert.SerializeObject(obj,
                  new JsonSerializerSettings
                  {
                      NullValueHandling = NullValueHandling.Ignore,
                      DateTimeZoneHandling = DateTimeZoneHandling.Utc
                  });
            }

            return json;
        }

        static bool InputBoolean(string question, bool yesDefault)
        {
            Console.Write(question);

            if (yesDefault) Console.Write(" [Y/n]? ");
            else Console.Write(" [y/N]? ");

            string userInput = Console.ReadLine();

            if (String.IsNullOrEmpty(userInput))
            {
                if (yesDefault) return true;
                return false;
            }

            userInput = userInput.ToLower();

            if (yesDefault)
            {
                if (
                    (String.Compare(userInput, "n") == 0)
                    || (String.Compare(userInput, "no") == 0)
                   )
                {
                    return false;
                }

                return true;
            }
            else
            {
                if (
                    (String.Compare(userInput, "y") == 0)
                    || (String.Compare(userInput, "yes") == 0)
                   )
                {
                    return true;
                }

                return false;
            }
        }

        static string InputString(string question, string defaultAnswer, bool allowNull)
        {
            while (true)
            {
                Console.Write(question);

                if (!String.IsNullOrEmpty(defaultAnswer))
                {
                    Console.Write(" [" + defaultAnswer + "]");
                }

                Console.Write(" ");

                string userInput = Console.ReadLine();

                if (String.IsNullOrEmpty(userInput))
                {
                    if (!String.IsNullOrEmpty(defaultAnswer)) return defaultAnswer;
                    if (allowNull) return null;
                    else continue;
                }

                return userInput;
            }
        }

        static int InputInteger(string question, int defaultAnswer, bool positiveOnly, bool allowZero)
        {
            while (true)
            {
                Console.Write(question);
                Console.Write(" [" + defaultAnswer + "] ");

                string userInput = Console.ReadLine();

                if (String.IsNullOrEmpty(userInput))
                {
                    return defaultAnswer;
                }

                int ret = 0;
                if (!Int32.TryParse(userInput, out ret))
                {
                    Console.WriteLine("Please enter a valid integer.");
                    continue;
                }

                if (ret == 0)
                {
                    if (allowZero)
                    {
                        return 0;
                    }
                }

                if (ret < 0)
                {
                    if (positiveOnly)
                    {
                        Console.WriteLine("Please enter a value greater than zero.");
                        continue;
                    }
                }

                return ret;
            }
        }

        static decimal InputDecimal(string question, decimal defaultAnswer, bool positiveOnly, bool allowZero)
        {
            while (true)
            {
                Console.Write(question);
                Console.Write(" [" + defaultAnswer + "] ");

                string userInput = Console.ReadLine();

                if (String.IsNullOrEmpty(userInput))
                {
                    return defaultAnswer;
                }

                decimal ret = 0;
                if (!Decimal.TryParse(userInput, out ret))
                {
                    Console.WriteLine("Please enter a valid decimal.");
                    continue;
                }

                if (ret == 0)
                {
                    if (allowZero)
                    {
                        return 0;
                    }
                }

                if (ret < 0)
                {
                    if (positiveOnly)
                    {
                        Console.WriteLine("Please enter a value greater than zero.");
                        continue;
                    }
                }

                return ret;
            }
        }

        static List<string> InputStringList(string question, bool allowEmpty)
        {
            List<string> ret = new List<string>();

            while (true)
            {
                Console.Write(question);

                Console.Write(" ");

                string userInput = Console.ReadLine();

                if (String.IsNullOrEmpty(userInput))
                {
                    if (ret.Count < 1 && !allowEmpty) continue;
                    return ret;
                }

                ret.Add(userInput);
            }
        }

        #endregion
    }
}

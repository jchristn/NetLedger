<img src="https://github.com/jchristn/NetLedger/raw/main/Assets/icon.jpg" height="128" width="128">

# NetLedger

[![NuGet Version](https://img.shields.io/nuget/v/NetLedger.svg?style=flat)](https://www.nuget.org/packages/NetLedger/) [![NuGet](https://img.shields.io/nuget/dt/NetLedger.svg)](https://www.nuget.org/packages/NetLedger) 

NetLedger is a simple, self-contained ledgering library for adding debits and credits, checking balances, and performing commits on pending entries.  NetLedger uses Sqlite and is self-contained.  If you wish to have a version that uses an external database, please contact us.

## New in v1.0.0

- Initial release
- Creation and deletion of accounts
- Creation and canceling of pending debits and credits
- Balance APIs
- Select and full commits of pending transactions
- Events
 
## Simple Example

Refer to the ```Test``` project for a full example.

```csharp
using NetLedger;

Ledger ledger = new Ledger("netledger.db");
string accountGuid  = ledger.CreateAccount("My account");
Console.WriteLine("My account identifier is: " + accountGuid);

string creditGuid   = ledger.AddCredit (accountGuid, 25.00, "$25 credit!");
string debitGuid    = ledger.AddDebit  (accountGuid, 5.00,  "$5 debit :(");
Balance balance     = ledger.Commit    (accountGuid); 
Console.WriteLine("My current balance is: $" + balance.CommittedBalance);

creditGuid          = ledger.AddCredit (accountGuid, 50.00, "$50 credit!");
Console.WriteLine("My pending balance is: $" + balance.PendingBalance);
balance             = ledger.Commit    (accountGuid, new List<string> { creditGuid });
Console.WriteLine("My current balance is: $" + balance.CommittedBalance);

ledger.DeleteAccountByGuid(accountGuid);
```

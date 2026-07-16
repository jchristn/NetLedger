namespace LoadGenerator
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using NetLedger;
    using NetLedger.Database;

    internal static class Program
    {
        private static async Task<int> Main(string[] args)
        {
            try
            {
                LoadGeneratorOptions options = LoadGeneratorOptions.Parse(args);
                if (options.ShowHelp)
                {
                    Console.WriteLine(LoadGeneratorOptions.HelpText);
                    return 0;
                }

                Console.WriteLine("NetLedger LoadGenerator");
                Console.WriteLine("Database: " + options.DatabaseType);
                Console.WriteLine("Range:    " + FormatUtc(options.FromUtc) + " to " + FormatUtc(options.ToUtc));
                Console.WriteLine("Density:  " + options.DensityName);
                Console.WriteLine("Seed:     " + options.Seed);
                Console.WriteLine();

                await using Ledger ledger = new Ledger(options.ToDatabaseSettings());
                DemoDataGenerator generator = new DemoDataGenerator(ledger, options);
                GenerationSummary summary = await generator.GenerateAsync(CancellationToken.None).ConfigureAwait(false);

                Console.WriteLine();
                Console.WriteLine("Generation complete");
                Console.WriteLine("Tenants:                " + summary.Tenants);
                Console.WriteLine("Users:                  " + summary.Users);
                Console.WriteLine("Accounts:               " + summary.Accounts);
                Console.WriteLine("Account-user mappings:  " + summary.AccountUserMaps);
                Console.WriteLine("Transaction entries:    " + summary.TransactionEntries);
                Console.WriteLine("Committed entries:      " + summary.CommittedEntries);
                Console.WriteLine("Pending entries:        " + summary.PendingEntries);
                Console.WriteLine("Balance snapshots:      " + summary.BalanceEntries);
                Console.WriteLine("Request history rows:   " + summary.RequestHistoryEntries);

                return 0;
            }
            catch (ArgumentException e)
            {
                Console.Error.WriteLine(e.Message);
                Console.Error.WriteLine();
                Console.Error.WriteLine(LoadGeneratorOptions.HelpText);
                return 2;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e.ToString());
                return 1;
            }
        }

        private static string FormatUtc(DateTime value)
        {
            return value.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss.ffffff", CultureInfo.InvariantCulture) + "Z";
        }
    }

    internal sealed class DemoDataGenerator
    {
        private const string DefaultPassword = "password";
        private readonly Ledger _Ledger;
        private readonly LoadGeneratorOptions _Options;
        private readonly Random _Random;
        private readonly List<Tenant> _Tenants = new List<Tenant>();
        private readonly List<User> _Users = new List<User>();
        private readonly List<DemoAccount> _Accounts = new List<DemoAccount>();
        private readonly List<string> _Regions = new List<string> { "us-west", "us-central", "us-east", "eu-west", "ap-south" };
        private readonly List<string> _Labels = new List<string> { "blue", "green", "gold", "red", "payroll", "vendor", "revenue", "refund", "tax", "marketing", "ops", "card", "wire", "ach" };
        private readonly List<string> _Channels = new List<string> { "card", "wire", "ach", "cash", "check", "internal" };
        private readonly List<string> _Categories = new List<string> { "subscription", "invoice", "payroll", "vendor", "refund", "tax", "marketing", "transfer", "fee", "settlement" };
        private readonly List<RequestTemplate> _RequestTemplates = new List<RequestTemplate>
        {
            new RequestTemplate("GET", "/v1/accounts"),
            new RequestTemplate("PUT", "/v1/accounts"),
            new RequestTemplate("GET", "/v1/accounts/{accountId}"),
            new RequestTemplate("DELETE", "/v1/accounts/{accountId}"),
            new RequestTemplate("GET", "/v1/accounts/{accountId}/entries"),
            new RequestTemplate("GET", "/v1/accounts/{accountId}/entries/pending"),
            new RequestTemplate("POST", "/v1/accounts/{accountId}/entries/enumerate"),
            new RequestTemplate("PUT", "/v1/accounts/{accountId}/credits"),
            new RequestTemplate("PUT", "/v1/accounts/{accountId}/debits"),
            new RequestTemplate("GET", "/v1/accounts/{accountId}/balance"),
            new RequestTemplate("POST", "/v1/accounts/{accountId}/commit"),
            new RequestTemplate("GET", "/v1/balances"),
            new RequestTemplate("GET", "/v1/request-history"),
            new RequestTemplate("GET", "/v1/request-history/summary"),
            new RequestTemplate("GET", "/v1/tenants"),
            new RequestTemplate("GET", "/v1/tenants/{tenantId}/users"),
            new RequestTemplate("POST", "/v1/auth/login")
        };

        internal DemoDataGenerator(Ledger ledger, LoadGeneratorOptions options)
        {
            _Ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
            _Options = options ?? throw new ArgumentNullException(nameof(options));
            _Random = new Random(options.Seed);
        }

        internal async Task<GenerationSummary> GenerateAsync(CancellationToken token)
        {
            GenerationSummary summary = new GenerationSummary();

            await CreateTenantsAsync(summary, token).ConfigureAwait(false);
            await CreateUsersAsync(summary, token).ConfigureAwait(false);
            await CreateAccountsAsync(summary, token).ConfigureAwait(false);
            await CreateEntriesAsync(summary, token).ConfigureAwait(false);

            if (_Options.IncludeRequestHistory)
            {
                await CreateRequestHistoryAsync(summary, token).ConfigureAwait(false);
            }

            return summary;
        }

        private async Task CreateTenantsAsync(GenerationSummary summary, CancellationToken token)
        {
            for (int i = 0; i < _Options.Tenants; i++)
            {
                DateTime created = Jitter(_Options.FromUtc, TimeSpan.FromHours(3));
                Tenant tenant = new Tenant
                {
                    Name = _Options.Prefix + " Tenant " + (i + 1).ToString("00", CultureInfo.InvariantCulture),
                    Region = Pick(_Regions),
                    Active = true,
                    CreatedUtc = created,
                    LastUpdateUtc = created
                };

                tenant = await _Ledger.Driver.Tenants.CreateAsync(tenant, token).ConfigureAwait(false);
                _Tenants.Add(tenant);
                summary.Tenants++;
            }
        }

        private async Task CreateUsersAsync(GenerationSummary summary, CancellationToken token)
        {
            string run = _Options.RunId.ToLowerInvariant();

            foreach (Tenant tenant in _Tenants)
            {
                for (int i = 0; i < _Options.UsersPerTenant; i++)
                {
                    DateTime created = Jitter(tenant.CreatedUtc.AddMinutes(5 + i), TimeSpan.FromHours(2));
                    bool tenantAdmin = i == 0;
                    User user = new User
                    {
                        TenantId = tenant.Id,
                        FirstName = tenantAdmin ? "Admin" : "User",
                        LastName = (i + 1).ToString("00", CultureInfo.InvariantCulture),
                        Email = run + "-t" + (_Tenants.IndexOf(tenant) + 1).ToString(CultureInfo.InvariantCulture) + "-u" + (i + 1).ToString(CultureInfo.InvariantCulture) + "@example.invalid",
                        PasswordSha256 = Sha256(DefaultPassword),
                        IsTenantAdmin = tenantAdmin,
                        Active = true,
                        CreatedUtc = created,
                        LastUpdateUtc = created
                    };

                    user = await _Ledger.Driver.Users.CreateAsync(user, token).ConfigureAwait(false);
                    _Users.Add(user);
                    summary.Users++;
                }
            }
        }

        private async Task CreateAccountsAsync(GenerationSummary summary, CancellationToken token)
        {
            string[] names =
            {
                "Operating Cash",
                "Revenue Clearing",
                "Payroll",
                "Vendor Payments",
                "Refund Reserve",
                "Marketing Spend",
                "Tax Holding",
                "Card Settlement",
                "Wire Settlement",
                "Customer Deposits"
            };

            foreach (Tenant tenant in _Tenants)
            {
                List<User> tenantUsers = _Users.Where(user => user.TenantId == tenant.Id).ToList();
                int accountCount = Math.Max(1, _Options.UsersPerTenant * _Options.AccountsPerUser);

                for (int i = 0; i < accountCount; i++)
                {
                    DateTime created = Jitter(tenant.CreatedUtc.AddMinutes(10 + i), TimeSpan.FromHours(4));
                    AccountPersonality personality = AccountPersonality.ForName(names[i % names.Length]);
                    Account account = new Account
                    {
                        TenantId = tenant.Id,
                        Name = tenant.Name + " " + names[i % names.Length] + " " + (i + 1).ToString("00", CultureInfo.InvariantCulture),
                        Notes = "Synthetic account generated by LoadGenerator.",
                        Labels = BuildLabels(personality.Category),
                        Tags = BuildTags(personality.Category, tenant.Region ?? "unknown"),
                        CreatedUtc = created,
                        LastUpdateUtc = created,
                        Active = true
                    };

                    account = await _Ledger.Driver.Accounts.CreateAsync(account, token).ConfigureAwait(false);

                    decimal initialBalance = Money(1000m, 50000m);
                    Entry initialBalanceEntry = new Entry
                    {
                        TenantId = tenant.Id,
                        AccountId = account.Id,
                        Type = EntryType.Balance,
                        Amount = initialBalance,
                        Description = "Initial demo balance",
                        IsCommitted = true,
                        CommittedUtc = created,
                        CreatedUtc = created,
                        LastUpdateUtc = created,
                        Labels = new List<string> { "initial" },
                        Tags = new Dictionary<string, string> { { "source", "load-generator" }, { "category", personality.Category } }
                    };
                    await _Ledger.Driver.Entries.CreateAsync(initialBalanceEntry, token).ConfigureAwait(false);

                    DemoAccount demoAccount = new DemoAccount(account, personality, initialBalance, initialBalanceEntry);
                    _Accounts.Add(demoAccount);
                    summary.Accounts++;
                    summary.BalanceEntries++;

                    int ownerIndex = i % tenantUsers.Count;
                    int maps = Math.Min(tenantUsers.Count, Math.Max(1, _Random.Next(1, 4)));
                    for (int j = 0; j < maps; j++)
                    {
                        User user = tenantUsers[(ownerIndex + j) % tenantUsers.Count];
                        await _Ledger.Driver.AccountUserMaps.CreateAsync(new AccountUserMap
                        {
                            TenantId = tenant.Id,
                            AccountId = account.Id,
                            UserId = user.Id,
                            CreatedUtc = created.AddMinutes(j)
                        }, token).ConfigureAwait(false);
                        summary.AccountUserMaps++;
                    }
                }
            }
        }

        private async Task CreateEntriesAsync(GenerationSummary summary, CancellationToken token)
        {
            long targetRecords = _Options.Records ?? EstimateRecordCount();
            int perAccountBase = (int)Math.Max(1, targetRecords / Math.Max(1, _Accounts.Count));
            int remainder = (int)Math.Max(0, targetRecords % Math.Max(1, _Accounts.Count));

            for (int index = 0; index < _Accounts.Count; index++)
            {
                DemoAccount account = _Accounts[index];
                int count = perAccountBase + (index < remainder ? 1 : 0);
                double jitter = 0.75d + _Random.NextDouble() * 0.6d;
                count = Math.Max(1, (int)Math.Round(count * jitter));

                List<Entry> entries = BuildEntriesForAccount(account, count);
                await CreateInChunksAsync(entries, _Options.InsertBatchSize, token).ConfigureAwait(false);

                summary.TransactionEntries += entries.Count;
                List<Entry> committed = entries.Where(entry => _Random.NextDouble() <= _Options.CommitRatio).OrderBy(entry => entry.CreatedUtc).ToList();
                summary.PendingEntries += entries.Count - committed.Count;

                for (int i = 0; i < committed.Count; i += _Options.CommitBatchSize)
                {
                    List<Entry> batch = committed.Skip(i).Take(_Options.CommitBatchSize).ToList();
                    if (batch.Count == 0) continue;

                    DateTime commitUtc = Clamp(
                        batch.Max(entry => entry.CreatedUtc).AddMinutes(1 + _Random.Next(0, 45)).AddMilliseconds(_Random.Next(0, 1000)),
                        _Options.FromUtc,
                        _Options.ToUtc);

                    decimal credits = batch.Where(entry => entry.Type == EntryType.Credit).Sum(entry => entry.Amount);
                    decimal debits = batch.Where(entry => entry.Type == EntryType.Debit).Sum(entry => entry.Amount);
                    account.CurrentBalance += credits - debits;

                    Entry balanceEntry = new Entry
                    {
                        TenantId = account.Account.TenantId,
                        AccountId = account.Account.Id,
                        Type = EntryType.Balance,
                        Amount = account.CurrentBalance,
                        Description = "Balance after generated commit",
                        Replaces = account.LatestBalanceEntry.Id,
                        IsCommitted = true,
                        CommittedUtc = commitUtc,
                        CreatedUtc = commitUtc,
                        LastUpdateUtc = commitUtc,
                        Labels = new List<string> { "generated", "balance" },
                        Tags = new Dictionary<string, string> { { "source", "load-generator" }, { "category", account.Personality.Category } }
                    };

                    foreach (Entry entry in batch)
                    {
                        entry.IsCommitted = true;
                        entry.CommittedUtc = commitUtc;
                        entry.CommittedById = balanceEntry.Id;
                    }

                    await _Ledger.Driver.Entries.ApplyCommitAsync(batch, balanceEntry, token).ConfigureAwait(false);
                    account.LatestBalanceEntry = balanceEntry;
                    summary.CommittedEntries += batch.Count;
                    summary.BalanceEntries++;
                }

                await StabilizeAccountBalanceAsync(account, summary, token).ConfigureAwait(false);

                Console.WriteLine("Generated " + entries.Count.ToString(CultureInfo.InvariantCulture) + " entries for " + account.Account.Name + ".");
            }
        }

        private List<Entry> BuildEntriesForAccount(DemoAccount account, int count)
        {
            List<Entry> entries = new List<Entry>(count);
            List<DateTime> timestamps = Enumerable.Range(0, count).Select(_ => RandomTimestamp()).OrderBy(timestamp => timestamp).ToList();
            decimal projectedBalance = account.CurrentBalance;
            decimal floor = MinimumGeneratedBalance(account);
            DateTime tailStart = _Options.FromUtc.AddMilliseconds((_Options.ToUtc - _Options.FromUtc).TotalMilliseconds * 0.80d);

            for (int i = 0; i < count; i++)
            {
                DateTime timestamp = timestamps[i];
                double creditRatio = account.Personality.CreditRatio;
                if (projectedBalance <= floor * 1.25m) creditRatio = 1d;
                else if (timestamp >= tailStart && projectedBalance <= floor * 3m) creditRatio = Math.Max(creditRatio, 0.90d);
                else if (projectedBalance <= floor * 2m) creditRatio = Math.Max(creditRatio, 0.82d);

                EntryType type = _Random.NextDouble() <= creditRatio ? EntryType.Credit : EntryType.Debit;
                decimal amount = AmountFor(account.Personality);
                if (type == EntryType.Debit && projectedBalance - amount < floor)
                {
                    decimal maximumDebit = Math.Round(projectedBalance - floor, 2, MidpointRounding.AwayFromZero);
                    if (maximumDebit >= 1m)
                    {
                        amount = Math.Min(amount, maximumDebit);
                    }
                    else
                    {
                        type = EntryType.Credit;
                        amount = AmountFor(account.Personality);
                    }
                }

                string category = type == EntryType.Credit ? account.Personality.CreditDescription : account.Personality.DebitDescription;

                entries.Add(new Entry
                {
                    TenantId = account.Account.TenantId,
                    AccountId = account.Account.Id,
                    Type = type,
                    Amount = amount,
                    Description = category + " " + (i + 1).ToString("000000", CultureInfo.InvariantCulture),
                    IsCommitted = false,
                    CreatedUtc = timestamp,
                    LastUpdateUtc = timestamp,
                    Labels = BuildLabels(account.Personality.Category),
                    Tags = BuildTags(account.Personality.Category, Pick(_Regions))
                });

                projectedBalance += type == EntryType.Credit ? amount : -amount;
            }

            return entries;
        }

        private async Task CreateInChunksAsync(List<Entry> entries, int chunkSize, CancellationToken token)
        {
            for (int i = 0; i < entries.Count; i += chunkSize)
            {
                await _Ledger.Driver.Entries.CreateManyAsync(entries.Skip(i).Take(chunkSize).ToList(), token).ConfigureAwait(false);
            }
        }

        private async Task CreateRequestHistoryAsync(GenerationSummary summary, CancellationToken token)
        {
            int rows = _Options.RequestHistoryRecords ?? (int)Math.Min(Int32.MaxValue, EstimateRequestHistoryCount());
            if (rows <= 0) return;

            for (int i = 0; i < rows; i++)
            {
                Tenant tenant = Pick(_Tenants);
                List<User> tenantUsers = _Users.Where(user => user.TenantId == tenant.Id).ToList();
                User user = tenantUsers.Count > 0 ? Pick(tenantUsers) : Pick(_Users);
                List<DemoAccount> tenantAccounts = _Accounts.Where(account => account.Account.TenantId == tenant.Id).ToList();
                DemoAccount account = tenantAccounts.Count > 0 ? Pick(tenantAccounts) : Pick(_Accounts);
                RequestTemplate template = Pick(_RequestTemplates);
                DateTime created = RandomTimestamp();
                double duration = Math.Round(4d + _Random.NextDouble() * _Random.Next(10, 600), 3);
                int status = PickStatusCode(template.Method);
                string path = BuildRequestPath(template, tenant, user, account);
                string url = path + BuildQueryString(template);
                string? requestBody = BuildRequestBody(template, tenant, user, account);
                string? responseBody = BuildResponseBody(status, template, account);

                RequestHistoryEntry entry = new RequestHistoryEntry
                {
                    TenantId = tenant.Id,
                    PrincipalId = user.Id,
                    PrincipalType = "User",
                    Method = template.Method,
                    Path = path,
                    Url = url,
                    StatusCode = status,
                    DurationMs = duration,
                    SourceIp = "10." + _Random.Next(0, 255).ToString(CultureInfo.InvariantCulture) + "." + _Random.Next(0, 255).ToString(CultureInfo.InvariantCulture) + "." + _Random.Next(1, 255).ToString(CultureInfo.InvariantCulture),
                    RequestHeaders = new Dictionary<string, string>
                    {
                        { "user-agent", "NetLedger LoadGenerator" },
                        { "accept", "application/json" },
                        { "x-tenant-id", tenant.Id }
                    },
                    RequestBody = requestBody,
                    RequestBodyBytes = requestBody == null ? 0 : Encoding.UTF8.GetByteCount(requestBody),
                    ResponseHeaders = new Dictionary<string, string> { { "content-type", "application/json" } },
                    ResponseBody = responseBody,
                    ResponseBodyBytes = responseBody == null ? 0 : Encoding.UTF8.GetByteCount(responseBody),
                    CreatedUtc = created,
                    CompletedUtc = created.AddMilliseconds(duration)
                };

                await _Ledger.Driver.RequestHistory.CreateAsync(entry, token).ConfigureAwait(false);
                summary.RequestHistoryEntries++;

                if (summary.RequestHistoryEntries % 1000 == 0)
                {
                    Console.WriteLine("Generated " + summary.RequestHistoryEntries.ToString(CultureInfo.InvariantCulture) + " request-history rows.");
                }
            }
        }

        private long EstimateRecordCount()
        {
            double days = Math.Max(1d, (_Options.ToUtc - _Options.FromUtc).TotalDays);
            return Math.Max(1L, (long)Math.Round(_Accounts.Count * days * _Options.EntriesPerAccountDay));
        }

        private async Task StabilizeAccountBalanceAsync(DemoAccount account, GenerationSummary summary, CancellationToken token)
        {
            decimal floor = MinimumGeneratedBalance(account);
            if (account.CurrentBalance >= floor) return;

            DateTime timestamp = Clamp(_Options.ToUtc.AddMinutes(-1 * _Random.Next(1, 120)).AddMilliseconds(_Random.Next(0, 1000)), _Options.FromUtc, _Options.ToUtc);
            decimal target = floor + Money(250m, Math.Max(500m, floor));
            decimal amount = Math.Round(target - account.CurrentBalance, 2, MidpointRounding.AwayFromZero);
            if (amount <= 0m) return;

            Entry credit = new Entry
            {
                TenantId = account.Account.TenantId,
                AccountId = account.Account.Id,
                Type = EntryType.Credit,
                Amount = amount,
                Description = "Generated balance stabilization credit",
                IsCommitted = false,
                CreatedUtc = timestamp,
                LastUpdateUtc = timestamp,
                Labels = new List<string> { "generated", "stabilization" },
                Tags = new Dictionary<string, string> { { "source", "load-generator" }, { "category", account.Personality.Category } }
            };

            credit = await _Ledger.Driver.Entries.CreateAsync(credit, token).ConfigureAwait(false);
            account.CurrentBalance += amount;

            Entry balanceEntry = new Entry
            {
                TenantId = account.Account.TenantId,
                AccountId = account.Account.Id,
                Type = EntryType.Balance,
                Amount = account.CurrentBalance,
                Description = "Balance after generated stabilization",
                Replaces = account.LatestBalanceEntry.Id,
                IsCommitted = true,
                CommittedUtc = timestamp,
                CreatedUtc = timestamp,
                LastUpdateUtc = timestamp,
                Labels = new List<string> { "generated", "balance", "stabilization" },
                Tags = new Dictionary<string, string> { { "source", "load-generator" }, { "category", account.Personality.Category } }
            };

            credit.IsCommitted = true;
            credit.CommittedUtc = timestamp;
            credit.CommittedById = balanceEntry.Id;
            await _Ledger.Driver.Entries.ApplyCommitAsync(new List<Entry> { credit }, balanceEntry, token).ConfigureAwait(false);

            account.LatestBalanceEntry = balanceEntry;
            summary.TransactionEntries++;
            summary.CommittedEntries++;
            summary.BalanceEntries++;
        }

        private decimal MinimumGeneratedBalance(DemoAccount account)
        {
            return Math.Max(250m, Math.Round(account.InitialBalance * 0.12m, 2, MidpointRounding.AwayFromZero));
        }

        private long EstimateRequestHistoryCount()
        {
            double days = Math.Max(1d, (_Options.ToUtc - _Options.FromUtc).TotalDays);
            double records = _Accounts.Count * days * _Options.RequestHistoryPerAccountDay * _Options.RequestHistoryRatio;
            return Math.Max(1L, (long)Math.Round(records));
        }

        private string BuildRequestPath(RequestTemplate template, Tenant tenant, User user, DemoAccount account)
        {
            return template.Path
                .Replace("{tenantId}", tenant.Id)
                .Replace("{userId}", user.Id)
                .Replace("{accountId}", account.Account.Id);
        }

        private string BuildQueryString(RequestTemplate template)
        {
            List<string> parts = new List<string>
            {
                "demo=true",
                "seed=" + _Options.Seed.ToString(CultureInfo.InvariantCulture)
            };

            if (template.Method == "GET" && (template.Path.EndsWith("/entries", StringComparison.OrdinalIgnoreCase) || template.Path.EndsWith("/request-history", StringComparison.OrdinalIgnoreCase) || template.Path.EndsWith("/accounts", StringComparison.OrdinalIgnoreCase)))
            {
                parts.Add("maxResults=" + _Random.Next(25, 101).ToString(CultureInfo.InvariantCulture));
                parts.Add("skip=" + _Random.Next(0, 5).ToString(CultureInfo.InvariantCulture));
            }

            if (template.Path.EndsWith("/request-history/summary", StringComparison.OrdinalIgnoreCase))
            {
                parts.Add("bucketMinutes=" + Pick(new List<int> { 1, 15, 60, 240 }).ToString(CultureInfo.InvariantCulture));
            }

            return "?" + String.Join("&", parts);
        }

        private string? BuildRequestBody(RequestTemplate template, Tenant tenant, User user, DemoAccount account)
        {
            if (template.Method == "GET" || template.Method == "DELETE") return null;

            if (template.Path.EndsWith("/credits", StringComparison.OrdinalIgnoreCase) || template.Path.EndsWith("/debits", StringComparison.OrdinalIgnoreCase))
            {
                return "{\"tenantId\":\"" + tenant.Id + "\",\"accountId\":\"" + account.Account.Id + "\",\"amount\":" + AmountFor(account.Personality).ToString(CultureInfo.InvariantCulture) + ",\"description\":\"synthetic request history traffic\",\"labels\":[\"" + account.Personality.Category + "\"],\"tags\":{\"source\":\"load-generator\"}}";
            }

            if (template.Path.EndsWith("/entries/enumerate", StringComparison.OrdinalIgnoreCase))
            {
                return "{\"tenantId\":\"" + tenant.Id + "\",\"accountId\":\"" + account.Account.Id + "\",\"labels\":[\"" + Pick(_Labels) + "\"],\"tags\":{\"color\":\"" + Pick(new List<string> { "blue", "green", "gold", "red" }) + "\"},\"orderBy\":\"createdutc\",\"orderDirection\":\"descending\",\"maxResults\":50}";
            }

            if (template.Path.EndsWith("/commit", StringComparison.OrdinalIgnoreCase))
            {
                return "{\"tenantId\":\"" + tenant.Id + "\",\"accountId\":\"" + account.Account.Id + "\",\"userId\":\"" + user.Id + "\"}";
            }

            if (template.Path.EndsWith("/accounts", StringComparison.OrdinalIgnoreCase))
            {
                return "{\"tenantId\":\"" + tenant.Id + "\",\"name\":\"Synthetic API Account\",\"labels\":[\"api\",\"demo\"],\"tags\":{\"source\":\"load-generator\"}}";
            }

            if (template.Path.EndsWith("/auth/login", StringComparison.OrdinalIgnoreCase))
            {
                return "{\"email\":\"" + user.Email + "\",\"password\":\"redacted\"}";
            }

            return "{\"tenantId\":\"" + tenant.Id + "\",\"source\":\"load-generator\"}";
        }

        private string? BuildResponseBody(int status, RequestTemplate template, DemoAccount account)
        {
            if (status >= 400)
            {
                return "{\"error\":\"synthetic " + status.ToString(CultureInfo.InvariantCulture) + " response\"}";
            }

            if (template.Path.EndsWith("/balance", StringComparison.OrdinalIgnoreCase))
            {
                return "{\"accountId\":\"" + account.Account.Id + "\",\"balance\":" + account.CurrentBalance.ToString(CultureInfo.InvariantCulture) + "}";
            }

            if (template.Path.EndsWith("/request-history/summary", StringComparison.OrdinalIgnoreCase))
            {
                return "{\"totalCount\":" + _Random.Next(10, 500).ToString(CultureInfo.InvariantCulture) + ",\"averageDurationMs\":" + Math.Round(10d + _Random.NextDouble() * 90d, 3).ToString(CultureInfo.InvariantCulture) + "}";
            }

            if (status == 204) return null;
            return "{\"success\":true,\"id\":\"" + NetLedgerId.Generate("demo_") + "\"}";
        }

        private List<string> BuildLabels(string category)
        {
            HashSet<string> labels = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                category,
                Pick(_Labels)
            };

            if (_Random.NextDouble() < _Options.MetadataRichness) labels.Add(Pick(_Labels));
            if (_Random.NextDouble() < _Options.MetadataRichness / 2d) labels.Add(Pick(_Labels));

            return labels.ToList();
        }

        private Dictionary<string, string> BuildTags(string category, string region)
        {
            Dictionary<string, string> tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "source", "load-generator" },
                { "category", category },
                { "channel", Pick(_Channels) },
                { "region", region },
                { "color", Pick(new List<string> { "blue", "green", "gold", "red" }) }
            };

            if (_Random.NextDouble() < _Options.MetadataRichness)
            {
                tags["campaign"] = "campaign-" + _Random.Next(1, 12).ToString("00", CultureInfo.InvariantCulture);
            }

            return tags;
        }

        private DateTime RandomTimestamp()
        {
            double totalSeconds = (_Options.ToUtc - _Options.FromUtc).TotalSeconds;
            DateTime timestamp = _Options.FromUtc.AddSeconds(_Random.NextDouble() * totalSeconds);

            if (_Random.NextDouble() < _Options.BusinessHoursBias)
            {
                int hour = 8 + _Random.Next(0, 10);
                timestamp = new DateTime(timestamp.Year, timestamp.Month, timestamp.Day, hour, _Random.Next(0, 60), _Random.Next(0, 60), DateTimeKind.Utc);
            }

            if (_Random.NextDouble() < _Options.WeekdayBias)
            {
                while (timestamp.DayOfWeek == DayOfWeek.Saturday || timestamp.DayOfWeek == DayOfWeek.Sunday)
                {
                    timestamp = timestamp.AddDays(-1);
                }
            }

            if (_Random.NextDouble() < _Options.SpikeRatio)
            {
                timestamp = timestamp.AddMinutes(_Random.Next(-90, 91));
            }

            timestamp = timestamp.AddTicks(_Random.Next(0, 10000000) / 10 * 10);
            return Clamp(timestamp, _Options.FromUtc, _Options.ToUtc);
        }

        private DateTime Jitter(DateTime value, TimeSpan range)
        {
            return Clamp(value.AddMilliseconds(_Random.NextDouble() * range.TotalMilliseconds), _Options.FromUtc, _Options.ToUtc);
        }

        private decimal AmountFor(AccountPersonality personality)
        {
            decimal value;
            double roll = _Random.NextDouble();
            if (roll < 0.65d)
            {
                value = Money(personality.SmallMin, personality.SmallMax);
            }
            else if (roll < 0.93d)
            {
                value = Money(personality.MediumMin, personality.MediumMax);
            }
            else
            {
                value = Money(personality.LargeMin, personality.LargeMax);
            }

            return value;
        }

        private decimal Money(decimal minimum, decimal maximum)
        {
            double curve = Math.Pow(_Random.NextDouble(), 1.8d);
            decimal value = minimum + (maximum - minimum) * (decimal)curve;
            return Math.Round(value, 2, MidpointRounding.AwayFromZero);
        }

        private int PickStatusCode(string method)
        {
            double roll = _Random.NextDouble();
            if (roll < 0.84d) return method == "PUT" ? 201 : 200;
            if (roll < 0.90d) return method == "DELETE" ? 204 : 200;
            if (roll < 0.96d) return 400;
            if (roll < 0.985d) return 401;
            return 500;
        }

        private T Pick<T>(List<T> values)
        {
            return values[_Random.Next(values.Count)];
        }

        private static DateTime Clamp(DateTime value, DateTime minimum, DateTime maximum)
        {
            if (value < minimum) return minimum;
            if (value > maximum) return maximum;
            return DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }

        private static string Sha256(string value)
        {
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
            StringBuilder sb = new StringBuilder(hash.Length * 2);
            foreach (byte b in hash)
            {
                sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }
    }

    internal sealed class DemoAccount
    {
        internal Account Account { get; }
        internal AccountPersonality Personality { get; }
        internal decimal InitialBalance { get; }
        internal decimal CurrentBalance { get; set; }
        internal Entry LatestBalanceEntry { get; set; }

        internal DemoAccount(Account account, AccountPersonality personality, decimal currentBalance, Entry latestBalanceEntry)
        {
            Account = account;
            Personality = personality;
            InitialBalance = currentBalance;
            CurrentBalance = currentBalance;
            LatestBalanceEntry = latestBalanceEntry;
        }
    }

    internal sealed class RequestTemplate
    {
        internal string Method { get; }
        internal string Path { get; }

        internal RequestTemplate(string method, string path)
        {
            Method = method;
            Path = path;
        }
    }

    internal sealed class AccountPersonality
    {
        internal string Category { get; private set; } = "ops";
        internal string CreditDescription { get; private set; } = "Generated credit";
        internal string DebitDescription { get; private set; } = "Generated debit";
        internal double CreditRatio { get; private set; } = 0.52d;
        internal decimal SmallMin { get; private set; } = 5m;
        internal decimal SmallMax { get; private set; } = 150m;
        internal decimal MediumMin { get; private set; } = 150m;
        internal decimal MediumMax { get; private set; } = 2500m;
        internal decimal LargeMin { get; private set; } = 2500m;
        internal decimal LargeMax { get; private set; } = 50000m;

        internal static AccountPersonality ForName(string name)
        {
            string lower = name.ToLowerInvariant();
            if (lower.Contains("revenue") || lower.Contains("deposit") || lower.Contains("settlement"))
            {
                return new AccountPersonality
                {
                    Category = "revenue",
                    CreditDescription = "Customer payment",
                    DebitDescription = "Settlement adjustment",
                    CreditRatio = 0.72d,
                    SmallMin = 10m,
                    SmallMax = 300m,
                    MediumMin = 300m,
                    MediumMax = 6000m,
                    LargeMin = 6000m,
                    LargeMax = 120000m
                };
            }

            if (lower.Contains("payroll"))
            {
                return new AccountPersonality
                {
                    Category = "payroll",
                    CreditDescription = "Payroll funding",
                    DebitDescription = "Payroll disbursement",
                    CreditRatio = 0.28d,
                    SmallMin = 50m,
                    SmallMax = 500m,
                    MediumMin = 500m,
                    MediumMax = 9000m,
                    LargeMin = 9000m,
                    LargeMax = 180000m
                };
            }

            if (lower.Contains("vendor") || lower.Contains("marketing") || lower.Contains("tax"))
            {
                return new AccountPersonality
                {
                    Category = lower.Contains("tax") ? "tax" : lower.Contains("marketing") ? "marketing" : "vendor",
                    CreditDescription = "Funding transfer",
                    DebitDescription = "Vendor payment",
                    CreditRatio = 0.35d,
                    SmallMin = 25m,
                    SmallMax = 750m,
                    MediumMin = 750m,
                    MediumMax = 12000m,
                    LargeMin = 12000m,
                    LargeMax = 250000m
                };
            }

            if (lower.Contains("refund"))
            {
                return new AccountPersonality
                {
                    Category = "refund",
                    CreditDescription = "Refund reserve funding",
                    DebitDescription = "Customer refund",
                    CreditRatio = 0.45d,
                    SmallMin = 5m,
                    SmallMax = 300m,
                    MediumMin = 300m,
                    MediumMax = 4000m,
                    LargeMin = 4000m,
                    LargeMax = 50000m
                };
            }

            return new AccountPersonality();
        }
    }

    internal sealed class GenerationSummary
    {
        internal int Tenants { get; set; }
        internal int Users { get; set; }
        internal int Accounts { get; set; }
        internal int AccountUserMaps { get; set; }
        internal int TransactionEntries { get; set; }
        internal int CommittedEntries { get; set; }
        internal int PendingEntries { get; set; }
        internal int BalanceEntries { get; set; }
        internal int RequestHistoryEntries { get; set; }
    }

    internal sealed class LoadGeneratorOptions
    {
        internal DatabaseTypeEnum DatabaseType { get; private set; } = DatabaseTypeEnum.Sqlite;
        internal string Filename { get; private set; } = "./netledger-demo.db";
        internal string? Hostname { get; private set; } = "localhost";
        internal int Port { get; private set; }
        internal string? DatabaseName { get; private set; } = "netledger";
        internal string? Username { get; private set; }
        internal string? Password { get; private set; }
        internal string? Schema { get; private set; }
        internal string? Instance { get; private set; }
        internal bool RequireEncryption { get; private set; }
        internal int MaxPoolSize { get; private set; } = 100;
        internal DateTime FromUtc { get; private set; } = DateTime.UtcNow.Date.AddDays(-30);
        internal DateTime ToUtc { get; private set; } = DateTime.UtcNow.Date;
        internal string DensityName { get; private set; } = "medium";
        internal long? Records { get; private set; }
        internal int? RequestHistoryRecords { get; private set; }
        internal int Tenants { get; private set; } = 3;
        internal int UsersPerTenant { get; private set; } = 8;
        internal int AccountsPerUser { get; private set; } = 3;
        internal double EntriesPerAccountDay { get; private set; } = 3d;
        internal double RequestHistoryPerAccountDay { get; private set; } = 3d;
        internal double CommitRatio { get; private set; } = 0.88d;
        internal double RequestHistoryRatio { get; private set; } = 1d;
        internal double MetadataRichness { get; private set; } = 0.7d;
        internal double BusinessHoursBias { get; private set; } = 0.72d;
        internal double WeekdayBias { get; private set; } = 0.8d;
        internal double SpikeRatio { get; private set; } = 0.08d;
        internal int CommitBatchSize { get; private set; } = 12;
        internal int InsertBatchSize { get; private set; } = 500;
        internal int Seed { get; private set; } = Environment.TickCount;
        internal string Prefix { get; private set; } = "Demo";
        internal string RunId { get; private set; } = NetLedgerId.Generate("run_").Substring(0, 8);
        internal bool IncludeRequestHistory { get; private set; } = true;
        internal bool ShowHelp { get; private set; }

        internal DatabaseSettings ToDatabaseSettings()
        {
            return new DatabaseSettings
            {
                Type = DatabaseType,
                Filename = Filename,
                Hostname = Hostname ?? String.Empty,
                Port = Port,
                Username = Username ?? String.Empty,
                Password = Password ?? String.Empty,
                DatabaseName = DatabaseName ?? String.Empty,
                Schema = Schema ?? String.Empty,
                Instance = Instance ?? String.Empty,
                RequireEncryption = RequireEncryption,
                MaxPoolSize = MaxPoolSize
            };
        }

        internal static LoadGeneratorOptions Parse(string[] args)
        {
            LoadGeneratorOptions options = new LoadGeneratorOptions();
            options.ApplyDensity("medium");

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                switch (arg.ToLowerInvariant())
                {
                    case "-h":
                    case "--help":
                        options.ShowHelp = true;
                        return options;
                    case "--db":
                    case "--database-type":
                        options.DatabaseType = ParseDatabaseType(RequireValue(args, ref i, arg));
                        break;
                    case "--file":
                    case "--filename":
                        options.Filename = RequireValue(args, ref i, arg);
                        break;
                    case "--host":
                    case "--hostname":
                        options.Hostname = RequireValue(args, ref i, arg);
                        break;
                    case "--port":
                        options.Port = ParseInt(RequireValue(args, ref i, arg), arg, 0, 65535);
                        break;
                    case "--database":
                    case "--database-name":
                        options.DatabaseName = RequireValue(args, ref i, arg);
                        break;
                    case "--username":
                    case "--user":
                        options.Username = RequireValue(args, ref i, arg);
                        break;
                    case "--password":
                        options.Password = RequireValue(args, ref i, arg);
                        break;
                    case "--schema":
                        options.Schema = RequireValue(args, ref i, arg);
                        break;
                    case "--instance":
                        options.Instance = RequireValue(args, ref i, arg);
                        break;
                    case "--require-encryption":
                        options.RequireEncryption = true;
                        break;
                    case "--max-pool-size":
                        options.MaxPoolSize = ParseInt(RequireValue(args, ref i, arg), arg, 1, 500);
                        break;
                    case "--from":
                    case "--start":
                    case "--starting-date":
                        options.FromUtc = ParseDate(RequireValue(args, ref i, arg), arg);
                        break;
                    case "--to":
                    case "--end":
                    case "--ending-date":
                        options.ToUtc = ParseDate(RequireValue(args, ref i, arg), arg);
                        break;
                    case "--density":
                        options.ApplyDensity(RequireValue(args, ref i, arg));
                        break;
                    case "--records":
                        options.Records = ParseLong(RequireValue(args, ref i, arg), arg, 1, Int32.MaxValue);
                        break;
                    case "--request-history-records":
                        options.RequestHistoryRecords = ParseInt(RequireValue(args, ref i, arg), arg, 0, Int32.MaxValue);
                        break;
                    case "--tenants":
                        options.Tenants = ParseInt(RequireValue(args, ref i, arg), arg, 1, 10000);
                        break;
                    case "--users-per-tenant":
                        options.UsersPerTenant = ParseInt(RequireValue(args, ref i, arg), arg, 1, 10000);
                        break;
                    case "--accounts-per-user":
                        options.AccountsPerUser = ParseInt(RequireValue(args, ref i, arg), arg, 1, 10000);
                        break;
                    case "--entries-per-account-day":
                        options.EntriesPerAccountDay = ParseDouble(RequireValue(args, ref i, arg), arg, 0.01d, 100000d);
                        break;
                    case "--request-history-per-account-day":
                        options.RequestHistoryPerAccountDay = ParseDouble(RequireValue(args, ref i, arg), arg, 0.01d, 100000d);
                        break;
                    case "--commit-ratio":
                        options.CommitRatio = ParseDouble(RequireValue(args, ref i, arg), arg, 0d, 1d);
                        break;
                    case "--request-history-ratio":
                        options.RequestHistoryRatio = ParseDouble(RequireValue(args, ref i, arg), arg, 0d, 100d);
                        break;
                    case "--metadata-richness":
                        options.MetadataRichness = ParseDouble(RequireValue(args, ref i, arg), arg, 0d, 1d);
                        break;
                    case "--business-hours-bias":
                        options.BusinessHoursBias = ParseDouble(RequireValue(args, ref i, arg), arg, 0d, 1d);
                        break;
                    case "--weekday-bias":
                        options.WeekdayBias = ParseDouble(RequireValue(args, ref i, arg), arg, 0d, 1d);
                        break;
                    case "--spike-ratio":
                        options.SpikeRatio = ParseDouble(RequireValue(args, ref i, arg), arg, 0d, 1d);
                        break;
                    case "--commit-batch-size":
                        options.CommitBatchSize = ParseInt(RequireValue(args, ref i, arg), arg, 1, 10000);
                        break;
                    case "--insert-batch-size":
                        options.InsertBatchSize = ParseInt(RequireValue(args, ref i, arg), arg, 1, 10000);
                        break;
                    case "--seed":
                        options.Seed = ParseInt(RequireValue(args, ref i, arg), arg, Int32.MinValue, Int32.MaxValue);
                        break;
                    case "--prefix":
                        options.Prefix = RequireValue(args, ref i, arg);
                        break;
                    case "--run-id":
                        options.RunId = RequireValue(args, ref i, arg);
                        break;
                    case "--no-request-history":
                        options.IncludeRequestHistory = false;
                        break;
                    default:
                        throw new ArgumentException("Unknown option '" + arg + "'.");
                }
            }

            options.FromUtc = DateTime.SpecifyKind(options.FromUtc.ToUniversalTime(), DateTimeKind.Utc);
            options.ToUtc = DateTime.SpecifyKind(options.ToUtc.ToUniversalTime(), DateTimeKind.Utc);
            if (options.ToUtc <= options.FromUtc)
            {
                throw new ArgumentException("--to must be later than --from.");
            }

            if (options.DatabaseType == DatabaseTypeEnum.Sqlite && String.IsNullOrEmpty(options.Filename))
            {
                throw new ArgumentException("--file is required when --db sqlite is used.");
            }

            return options;
        }

        private void ApplyDensity(string density)
        {
            string normalized = density.Trim().ToLowerInvariant();
            DensityName = normalized;
            switch (normalized)
            {
                case "tiny":
                    Tenants = 1;
                    UsersPerTenant = 2;
                    AccountsPerUser = 2;
                    EntriesPerAccountDay = 1.5d;
                    RequestHistoryPerAccountDay = EntriesPerAccountDay;
                    break;
                case "low":
                    Tenants = 2;
                    UsersPerTenant = 4;
                    AccountsPerUser = 2;
                    EntriesPerAccountDay = 2.5d;
                    RequestHistoryPerAccountDay = EntriesPerAccountDay;
                    break;
                case "medium":
                    Tenants = 3;
                    UsersPerTenant = 8;
                    AccountsPerUser = 3;
                    EntriesPerAccountDay = 3d;
                    RequestHistoryPerAccountDay = EntriesPerAccountDay;
                    break;
                case "high":
                    Tenants = 5;
                    UsersPerTenant = 12;
                    AccountsPerUser = 4;
                    EntriesPerAccountDay = 5d;
                    RequestHistoryPerAccountDay = EntriesPerAccountDay;
                    break;
                case "extreme":
                    Tenants = 8;
                    UsersPerTenant = 20;
                    AccountsPerUser = 5;
                    EntriesPerAccountDay = 8d;
                    RequestHistoryPerAccountDay = EntriesPerAccountDay;
                    break;
                default:
                    throw new ArgumentException("Unsupported density '" + density + "'. Use tiny, low, medium, high, or extreme.");
            }
        }

        private static DatabaseTypeEnum ParseDatabaseType(string value)
        {
            string normalized = value.Trim().ToLowerInvariant();
            if (normalized == "sqlite" || normalized == "sqlitedb") return DatabaseTypeEnum.Sqlite;
            if (normalized == "mysql") return DatabaseTypeEnum.Mysql;
            if (normalized == "postgres" || normalized == "postgresql") return DatabaseTypeEnum.Postgresql;
            if (normalized == "sqlserver" || normalized == "mssql") return DatabaseTypeEnum.SqlServer;
            throw new ArgumentException("Unsupported database type '" + value + "'.");
        }

        private static string RequireValue(string[] args, ref int index, string option)
        {
            if (index + 1 >= args.Length || args[index + 1].StartsWith("-", StringComparison.Ordinal))
            {
                throw new ArgumentException(option + " requires a value.");
            }

            index++;
            return args[index];
        }

        private static DateTime ParseDate(string value, string option)
        {
            if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTimeOffset dto))
            {
                throw new ArgumentException(option + " must be a valid date or timestamp.");
            }

            return dto.UtcDateTime;
        }

        private static int ParseInt(string value, string option, int minimum, int maximum)
        {
            if (!Int32.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result) || result < minimum || result > maximum)
            {
                throw new ArgumentException(option + " must be an integer from " + minimum.ToString(CultureInfo.InvariantCulture) + " to " + maximum.ToString(CultureInfo.InvariantCulture) + ".");
            }

            return result;
        }

        private static long ParseLong(string value, string option, long minimum, long maximum)
        {
            if (!Int64.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long result) || result < minimum || result > maximum)
            {
                throw new ArgumentException(option + " must be an integer from " + minimum.ToString(CultureInfo.InvariantCulture) + " to " + maximum.ToString(CultureInfo.InvariantCulture) + ".");
            }

            return result;
        }

        private static double ParseDouble(string value, string option, double minimum, double maximum)
        {
            if (!Double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double result) || result < minimum || result > maximum)
            {
                throw new ArgumentException(option + " must be a number from " + minimum.ToString(CultureInfo.InvariantCulture) + " to " + maximum.ToString(CultureInfo.InvariantCulture) + ".");
            }

            return result;
        }

        internal static readonly string HelpText =
@"Usage:
  dotnet run --project src\LoadGenerator\LoadGenerator.csproj -- [options]

Database:
  --db sqlite|mysql|postgresql|sqlserver
  --file <path>
  --host <host> --port <port> --database <name> --username <user> --password <password>
  --schema <name> --instance <name> --require-encryption --max-pool-size <count>

Generation:
  --from <timestamp> --to <timestamp>
  --density tiny|low|medium|high|extreme
  --records <count>
  --tenants <count>
  --users-per-tenant <count>
  --accounts-per-user <count>
  --entries-per-account-day <count>
  --commit-ratio <0-1>
  --request-history-per-account-day <count>
  --request-history-ratio <multiplier>
  --request-history-records <count>
  --metadata-richness <0-1>
  --business-hours-bias <0-1>
  --weekday-bias <0-1>
  --spike-ratio <0-1>
  --commit-batch-size <count>
  --insert-batch-size <count>
  --seed <int>
  --prefix <text>
  --run-id <text>
  --no-request-history";
    }
}

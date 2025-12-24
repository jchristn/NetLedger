#!/usr/bin/env python3
"""Test harness for the NetLedger SDK."""

import sys
import time
import uuid
from dataclasses import dataclass
from datetime import datetime
from typing import List, Optional, Callable

# Add parent directory to path for development
sys.path.insert(0, '..')

from netledger_sdk import (
    NetLedgerClient,
    EntryType,
    EnumerationOrder,
    EntryInput,
    AccountEnumerationQuery,
    EntryEnumerationQuery,
    ApiKeyEnumerationQuery
)


@dataclass
class TestResult:
    """Result of a single test."""
    name: str
    success: bool
    elapsed_ms: float
    error_message: Optional[str] = None


class TestHarness:
    """Test harness for the NetLedger SDK."""

    def __init__(self, endpoint: str, api_key: str):
        """Initialize the test harness."""
        self.client = NetLedgerClient(endpoint, api_key)
        self.results: List[TestResult] = []
        self.total_start_time: float = 0
        self.test_account_guid: str = ""
        self.test_entry_guids: List[str] = []

    def run(self) -> int:
        """Run all tests and return exit code."""
        print("NetLedger SDK Test Harness (Python)")
        print("====================================")
        print()
        print(f"Endpoint: {self.client.base_url}")
        print()

        self.total_start_time = time.time()

        try:
            self._run_service_tests()
            self._run_account_tests()
            self._run_entry_tests()
            self._run_enumeration_tests()
            self._run_balance_tests()
            self._run_api_key_tests()
            self._run_cleanup_tests()
        except Exception as e:
            print(f"Fatal error: {e}")
        finally:
            self.client.close()

        self._print_summary()

        failed_count = len([r for r in self.results if not r.success])
        return 1 if failed_count > 0 else 0

    def _run_service_tests(self) -> None:
        """Run service tests."""
        self._print_section_header("SERVICE TESTS")

        self._run_test("Health Check", lambda: (
            self._assert(self.client.service.health_check(), "Health check returned false")
        ))

        self._run_test("Get Service Info", lambda: (
            self._assert(self.client.service.get_info().name, "Service name is empty")
        ))

        print()

    def _run_account_tests(self) -> None:
        """Run account tests."""
        self._print_section_header("ACCOUNT TESTS")

        test_account_name = f"TestAccount_{uuid.uuid4().hex[:8]}"

        def create_account():
            account = self.client.account.create(test_account_name, "Test notes")
            self._assert(account.guid, "Account GUID is empty")
            self._assert(account.name == test_account_name, "Account name mismatch")
            self.test_account_guid = account.guid

        self._run_test("Create Account", create_account)

        def check_exists():
            self._assert(self.test_account_guid, "No account to check")
            exists = self.client.account.exists(self.test_account_guid)
            self._assert(exists, "Account should exist")

        self._run_test("Check Account Exists", check_exists)

        def get_account():
            self._assert(self.test_account_guid, "No account to get")
            account = self.client.account.get(self.test_account_guid)
            self._assert(account.guid == self.test_account_guid, "Account GUID mismatch")

        self._run_test("Get Account by GUID", get_account)

        def get_by_name():
            self._assert(self.test_account_guid, "No account to get")
            account = self.client.account.get_by_name(test_account_name)
            self._assert(account.guid == self.test_account_guid, "Account GUID mismatch")

        self._run_test("Get Account by Name", get_by_name)

        def enumerate_accounts():
            result = self.client.account.enumerate(AccountEnumerationQuery(max_results=10))
            self._assert(result.objects and len(result.objects) > 0, "No accounts returned")

        self._run_test("Enumerate Accounts", enumerate_accounts)

        def enumerate_with_search():
            result = self.client.account.enumerate(AccountEnumerationQuery(
                max_results=10,
                search_term=test_account_name
            ))
            self._assert(result.objects and len(result.objects) > 0, "Search should find the test account")

        self._run_test("Enumerate Accounts with Search", enumerate_with_search)

        print()

    def _run_entry_tests(self) -> None:
        """Run entry tests."""
        self._print_section_header("ENTRY TESTS")

        if not self.test_account_guid:
            print("  SKIPPED: No test account available")
            print()
            return

        def add_credit():
            entry_guid = self.client.entry.add_credit(self.test_account_guid, 100.00, "Test credit")
            self._assert(entry_guid, "Entry GUID is empty")
            self.test_entry_guids.append(entry_guid)

        self._run_test("Add Single Credit", add_credit)

        def add_debit():
            entry_guid = self.client.entry.add_debit(self.test_account_guid, 25.50, "Test debit")
            self._assert(entry_guid, "Entry GUID is empty")
            self.test_entry_guids.append(entry_guid)

        self._run_test("Add Single Debit", add_debit)

        def add_batch_credits():
            entry_guids = self.client.entry.add_credits(self.test_account_guid, [
                EntryInput(10.00, "Batch credit 1"),
                EntryInput(20.00, "Batch credit 2"),
                EntryInput(30.00, "Batch credit 3")
            ])
            self._assert(len(entry_guids) == 3, f"Expected 3 entries, got {len(entry_guids)}")
            for guid in entry_guids:
                self.test_entry_guids.append(guid)

        self._run_test("Add Multiple Credits (Batch)", add_batch_credits)

        def add_batch_debits():
            entry_guids = self.client.entry.add_debits(self.test_account_guid, [
                EntryInput(5.00, "Batch debit 1"),
                EntryInput(7.50, "Batch debit 2")
            ])
            self._assert(len(entry_guids) == 2, f"Expected 2 entries, got {len(entry_guids)}")
            for guid in entry_guids:
                self.test_entry_guids.append(guid)

        self._run_test("Add Multiple Debits (Batch)", add_batch_debits)

        def get_all_entries():
            entries = self.client.entry.get_all(self.test_account_guid)
            self._assert(len(entries) >= len(self.test_entry_guids),
                         f"Expected at least {len(self.test_entry_guids)} entries")

        self._run_test("Get All Entries", get_all_entries)

        def get_pending():
            entries = self.client.entry.get_pending(self.test_account_guid)
            self._assert(len(entries) > 0, "Should have pending entries")

        self._run_test("Get Pending Entries", get_pending)

        def get_pending_credits():
            entries = self.client.entry.get_pending_credits(self.test_account_guid)
            self._assert(len(entries) > 0, "Should have pending credits")

        self._run_test("Get Pending Credits", get_pending_credits)

        def get_pending_debits():
            entries = self.client.entry.get_pending_debits(self.test_account_guid)
            self._assert(len(entries) > 0, "Should have pending debits")

        self._run_test("Get Pending Debits", get_pending_debits)

        def enumerate_entries():
            result = self.client.entry.enumerate(self.test_account_guid, EntryEnumerationQuery(
                max_results=50,
                ordering=EnumerationOrder.CREATED_DESCENDING
            ))
            self._assert(result.objects and len(result.objects) > 0, "No entries returned")

        self._run_test("Enumerate Entries", enumerate_entries)

        def enumerate_with_filter():
            self.client.entry.enumerate(self.test_account_guid, EntryEnumerationQuery(
                max_results=50,
                amount_min=10.00,
                amount_max=50.00
            ))
            # Just verify no error

        self._run_test("Enumerate Entries with Amount Filter", enumerate_with_filter)

        entry_to_cancel: Optional[str] = None

        def add_for_cancel():
            nonlocal entry_to_cancel
            entry_guid = self.client.entry.add_credit(self.test_account_guid, 1.00, "Entry to cancel")
            entry_to_cancel = entry_guid

        self._run_test("Add Entry for Cancellation", add_for_cancel)

        def cancel_entry():
            self._assert(entry_to_cancel, "No entry to cancel")
            self.client.entry.cancel(self.test_account_guid, entry_to_cancel)

        self._run_test("Cancel Entry", cancel_entry)

        print()

    def _run_enumeration_tests(self) -> None:
        """Run comprehensive enumeration and pagination tests."""
        self._print_section_header("ENUMERATION AND PAGINATION TESTS")

        if not self.test_account_guid:
            print("  SKIPPED: No test account available")
            print()
            return

        # Create a dedicated account for enumeration tests with known data
        enum_test_account_guid: Optional[str] = None
        enum_entry_guids: List[str] = []

        def create_enum_test_account():
            nonlocal enum_test_account_guid
            account_name = f"EnumTestAccount_{uuid.uuid4().hex[:8]}"
            account = self.client.account.create(account_name, "Account for enumeration tests")
            self._assert(account.guid, "Enumeration test account GUID is empty")
            enum_test_account_guid = account.guid

        self._run_test("Create Enumeration Test Account", create_enum_test_account)

        # Create entries with varying amounts for ordering tests
        # We need enough entries to test pagination (more than one page)
        def create_enum_test_entries():
            nonlocal enum_entry_guids
            self._assert(enum_test_account_guid, "No enumeration test account")
            # Create 15 entries with specific amounts to test ordering
            amounts = [10.00, 50.00, 25.00, 75.00, 5.00, 100.00, 30.00,
                      15.00, 60.00, 45.00, 80.00, 20.00, 90.00, 35.00, 55.00]
            for i, amount in enumerate(amounts):
                entry_guid = self.client.entry.add_credit(
                    enum_test_account_guid,
                    amount,
                    f"Enum test entry {i+1}"
                )
                self._assert(entry_guid, f"Entry {i+1} GUID is empty")
                enum_entry_guids.append(entry_guid)
                # Small delay to ensure distinct creation timestamps
                time.sleep(0.05)

        self._run_test("Create 15 Test Entries for Pagination", create_enum_test_entries)

        # Test 1: Basic pagination - first page
        def test_pagination_first_page():
            self._assert(enum_test_account_guid, "No enumeration test account")
            result = self.client.entry.enumerate(enum_test_account_guid, EntryEnumerationQuery(
                max_results=5,
                ordering=EnumerationOrder.CREATED_ASCENDING
            ))
            self._assert(result.objects is not None, "Objects should not be None")
            self._assert(len(result.objects) == 5, f"Expected 5 entries, got {len(result.objects)}")
            self._assert(result.total_records == 15, f"Expected total_records=15, got {result.total_records}")
            self._assert(result.records_remaining == 10, f"Expected records_remaining=10, got {result.records_remaining}")
            self._assert(result.end_of_results == False, f"Expected end_of_results=False, got {result.end_of_results}")
            self._assert(result.continuation_token is not None, "Expected continuation_token to be present")
            self._assert(len(result.continuation_token) > 0, "Expected continuation_token to be non-empty")

        self._run_test("Pagination: First Page Metadata", test_pagination_first_page)

        # Test 2: Pagination - middle page using continuation token
        first_page_token: Optional[str] = None

        def test_pagination_middle_page():
            nonlocal first_page_token
            self._assert(enum_test_account_guid, "No enumeration test account")
            # Get first page to obtain continuation token
            first_result = self.client.entry.enumerate(enum_test_account_guid, EntryEnumerationQuery(
                max_results=5,
                ordering=EnumerationOrder.CREATED_ASCENDING
            ))
            first_page_token = first_result.continuation_token
            self._assert(first_page_token, "No continuation token from first page")

            # Get second page using continuation token
            second_result = self.client.entry.enumerate(enum_test_account_guid, EntryEnumerationQuery(
                max_results=5,
                ordering=EnumerationOrder.CREATED_ASCENDING,
                continuation_token=first_page_token
            ))
            self._assert(second_result.objects is not None, "Second page objects should not be None")
            self._assert(len(second_result.objects) == 5, f"Expected 5 entries on second page, got {len(second_result.objects)}")
            self._assert(second_result.total_records == 15, f"Expected total_records=15, got {second_result.total_records}")
            self._assert(second_result.records_remaining == 5, f"Expected records_remaining=5, got {second_result.records_remaining}")
            self._assert(second_result.end_of_results == False, f"Expected end_of_results=False, got {second_result.end_of_results}")
            self._assert(second_result.continuation_token is not None, "Expected continuation_token for third page")

        self._run_test("Pagination: Middle Page with Continuation Token", test_pagination_middle_page)

        # Test 3: Pagination - last page
        def test_pagination_last_page():
            self._assert(enum_test_account_guid, "No enumeration test account")
            # Get first two pages to reach the last page
            first_result = self.client.entry.enumerate(enum_test_account_guid, EntryEnumerationQuery(
                max_results=5,
                ordering=EnumerationOrder.CREATED_ASCENDING
            ))
            second_result = self.client.entry.enumerate(enum_test_account_guid, EntryEnumerationQuery(
                max_results=5,
                ordering=EnumerationOrder.CREATED_ASCENDING,
                continuation_token=first_result.continuation_token
            ))
            # Get third (last) page
            third_result = self.client.entry.enumerate(enum_test_account_guid, EntryEnumerationQuery(
                max_results=5,
                ordering=EnumerationOrder.CREATED_ASCENDING,
                continuation_token=second_result.continuation_token
            ))
            self._assert(third_result.objects is not None, "Third page objects should not be None")
            self._assert(len(third_result.objects) == 5, f"Expected 5 entries on last page, got {len(third_result.objects)}")
            self._assert(third_result.total_records == 15, f"Expected total_records=15, got {third_result.total_records}")
            self._assert(third_result.records_remaining == 0, f"Expected records_remaining=0, got {third_result.records_remaining}")
            self._assert(third_result.end_of_results == True, f"Expected end_of_results=True on last page, got {third_result.end_of_results}")
            # Continuation token may be None or empty on last page
            if third_result.continuation_token:
                # If present, fetching with it should return empty results
                pass

        self._run_test("Pagination: Last Page End of Results", test_pagination_last_page)

        # Test 4: Ordering - CREATED_ASCENDING
        def test_ordering_created_ascending():
            self._assert(enum_test_account_guid, "No enumeration test account")
            result = self.client.entry.enumerate(enum_test_account_guid, EntryEnumerationQuery(
                max_results=15,
                ordering=EnumerationOrder.CREATED_ASCENDING
            ))
            self._assert(result.objects is not None and len(result.objects) == 15, "Should return all 15 entries")
            # Verify entries are in ascending creation order (oldest first)
            for i in range(1, len(result.objects)):
                prev_created = result.objects[i-1].created_utc
                curr_created = result.objects[i].created_utc
                self._assert(prev_created is not None and curr_created is not None, "Created timestamps should be present")
                self._assert(prev_created <= curr_created,
                    f"CREATED_ASCENDING: Entry {i-1} ({prev_created}) should be <= Entry {i} ({curr_created})")

        self._run_test("Ordering: CREATED_ASCENDING", test_ordering_created_ascending)

        # Test 5: Ordering - CREATED_DESCENDING
        def test_ordering_created_descending():
            self._assert(enum_test_account_guid, "No enumeration test account")
            result = self.client.entry.enumerate(enum_test_account_guid, EntryEnumerationQuery(
                max_results=15,
                ordering=EnumerationOrder.CREATED_DESCENDING
            ))
            self._assert(result.objects is not None and len(result.objects) == 15, "Should return all 15 entries")
            # Verify entries are in descending creation order (newest first)
            for i in range(1, len(result.objects)):
                prev_created = result.objects[i-1].created_utc
                curr_created = result.objects[i].created_utc
                self._assert(prev_created is not None and curr_created is not None, "Created timestamps should be present")
                self._assert(prev_created >= curr_created,
                    f"CREATED_DESCENDING: Entry {i-1} ({prev_created}) should be >= Entry {i} ({curr_created})")

        self._run_test("Ordering: CREATED_DESCENDING", test_ordering_created_descending)

        # Test 6: Ordering - AMOUNT_ASCENDING
        def test_ordering_amount_ascending():
            self._assert(enum_test_account_guid, "No enumeration test account")
            result = self.client.entry.enumerate(enum_test_account_guid, EntryEnumerationQuery(
                max_results=15,
                ordering=EnumerationOrder.AMOUNT_ASCENDING
            ))
            self._assert(result.objects is not None and len(result.objects) == 15, "Should return all 15 entries")
            # Verify entries are in ascending amount order (smallest first)
            for i in range(1, len(result.objects)):
                prev_amount = result.objects[i-1].amount
                curr_amount = result.objects[i].amount
                self._assert(prev_amount <= curr_amount,
                    f"AMOUNT_ASCENDING: Entry {i-1} amount ({prev_amount}) should be <= Entry {i} amount ({curr_amount})")
            # Verify first entry has smallest amount (5.00) and last has largest (100.00)
            self._assert(result.objects[0].amount == 5.00, f"First entry should be 5.00, got {result.objects[0].amount}")
            self._assert(result.objects[-1].amount == 100.00, f"Last entry should be 100.00, got {result.objects[-1].amount}")

        self._run_test("Ordering: AMOUNT_ASCENDING", test_ordering_amount_ascending)

        # Test 7: Ordering - AMOUNT_DESCENDING
        def test_ordering_amount_descending():
            self._assert(enum_test_account_guid, "No enumeration test account")
            result = self.client.entry.enumerate(enum_test_account_guid, EntryEnumerationQuery(
                max_results=15,
                ordering=EnumerationOrder.AMOUNT_DESCENDING
            ))
            self._assert(result.objects is not None and len(result.objects) == 15, "Should return all 15 entries")
            # Verify entries are in descending amount order (largest first)
            for i in range(1, len(result.objects)):
                prev_amount = result.objects[i-1].amount
                curr_amount = result.objects[i].amount
                self._assert(prev_amount >= curr_amount,
                    f"AMOUNT_DESCENDING: Entry {i-1} amount ({prev_amount}) should be >= Entry {i} amount ({curr_amount})")
            # Verify first entry has largest amount (100.00) and last has smallest (5.00)
            self._assert(result.objects[0].amount == 100.00, f"First entry should be 100.00, got {result.objects[0].amount}")
            self._assert(result.objects[-1].amount == 5.00, f"Last entry should be 5.00, got {result.objects[-1].amount}")

        self._run_test("Ordering: AMOUNT_DESCENDING", test_ordering_amount_descending)

        # Test 8: Pagination with ordering preserved across pages
        def test_pagination_preserves_ordering():
            self._assert(enum_test_account_guid, "No enumeration test account")
            all_amounts: List[float] = []

            # Fetch all entries across multiple pages with AMOUNT_ASCENDING
            query = EntryEnumerationQuery(
                max_results=5,
                ordering=EnumerationOrder.AMOUNT_ASCENDING
            )
            result = self.client.entry.enumerate(enum_test_account_guid, query)
            while result.objects:
                for entry in result.objects:
                    all_amounts.append(entry.amount)
                if result.end_of_results or not result.continuation_token:
                    break
                query = EntryEnumerationQuery(
                    max_results=5,
                    ordering=EnumerationOrder.AMOUNT_ASCENDING,
                    continuation_token=result.continuation_token
                )
                result = self.client.entry.enumerate(enum_test_account_guid, query)

            self._assert(len(all_amounts) == 15, f"Expected 15 entries across all pages, got {len(all_amounts)}")
            # Verify ordering is preserved across pages
            for i in range(1, len(all_amounts)):
                self._assert(all_amounts[i-1] <= all_amounts[i],
                    f"Ordering not preserved across pages: {all_amounts[i-1]} should be <= {all_amounts[i]}")

        self._run_test("Pagination: Ordering Preserved Across Pages", test_pagination_preserves_ordering)

        # Test 9: Single result page (all results fit in one page)
        def test_single_page_results():
            self._assert(enum_test_account_guid, "No enumeration test account")
            result = self.client.entry.enumerate(enum_test_account_guid, EntryEnumerationQuery(
                max_results=100,  # More than total entries
                ordering=EnumerationOrder.CREATED_ASCENDING
            ))
            self._assert(result.objects is not None, "Objects should not be None")
            self._assert(len(result.objects) == 15, f"Expected 15 entries, got {len(result.objects)}")
            self._assert(result.total_records == 15, f"Expected total_records=15, got {result.total_records}")
            self._assert(result.records_remaining == 0, f"Expected records_remaining=0, got {result.records_remaining}")
            self._assert(result.end_of_results == True, f"Expected end_of_results=True, got {result.end_of_results}")

        self._run_test("Pagination: Single Page (All Results)", test_single_page_results)

        # Test 10: Empty results
        def test_empty_results():
            # Create account with no entries
            empty_account = self.client.account.create(
                f"EmptyAccount_{uuid.uuid4().hex[:8]}",
                "Account with no entries"
            )
            result = self.client.entry.enumerate(empty_account.guid, EntryEnumerationQuery(
                max_results=10,
                ordering=EnumerationOrder.CREATED_ASCENDING
            ))
            self._assert(result.total_records == 0, f"Expected total_records=0, got {result.total_records}")
            self._assert(result.records_remaining == 0, f"Expected records_remaining=0, got {result.records_remaining}")
            self._assert(result.end_of_results == True, f"Expected end_of_results=True for empty results")
            objects_count = len(result.objects) if result.objects else 0
            self._assert(objects_count == 0, f"Expected 0 objects, got {objects_count}")
            # Clean up
            self.client.account.delete(empty_account.guid)

        self._run_test("Pagination: Empty Results", test_empty_results)

        # Test 11: Account enumeration pagination
        def test_account_enumeration_pagination():
            # Create additional accounts for pagination testing
            created_account_guids: List[str] = []
            for i in range(5):
                acct = self.client.account.create(
                    f"PaginationTestAcct_{i}_{uuid.uuid4().hex[:4]}",
                    f"Pagination test account {i}"
                )
                created_account_guids.append(acct.guid)

            # Test first page
            result = self.client.account.enumerate(AccountEnumerationQuery(max_results=3))
            self._assert(result.objects is not None, "Account objects should not be None")
            self._assert(len(result.objects) <= 3, f"Expected at most 3 accounts, got {len(result.objects)}")
            self._assert(result.total_records > 0, f"Expected total_records > 0, got {result.total_records}")

            # If there are more results, verify pagination fields
            if not result.end_of_results:
                self._assert(result.records_remaining > 0, "records_remaining should be > 0 when not end_of_results")

            # Clean up created accounts
            for guid in created_account_guids:
                self.client.account.delete(guid)

        self._run_test("Account Enumeration: Pagination Metadata", test_account_enumeration_pagination)

        # Test 12: API Key enumeration pagination
        def test_api_key_enumeration_pagination():
            # Create additional API keys for pagination testing
            created_key_guids: List[str] = []
            for i in range(3):
                key = self.client.api_key.create(f"PaginationTestKey_{i}", is_admin=False)
                created_key_guids.append(key.guid)

            # Test enumeration
            result = self.client.api_key.enumerate(ApiKeyEnumerationQuery(max_results=2))
            self._assert(result.objects is not None, "API key objects should not be None")
            self._assert(len(result.objects) <= 2, f"Expected at most 2 keys, got {len(result.objects)}")
            self._assert(result.total_records > 0, f"Expected total_records > 0, got {result.total_records}")

            # Clean up created keys
            for guid in created_key_guids:
                self.client.api_key.revoke(guid)

        self._run_test("API Key Enumeration: Pagination Metadata", test_api_key_enumeration_pagination)

        # Test 13: Amount range filtering with pagination
        def test_amount_filter_with_pagination():
            self._assert(enum_test_account_guid, "No enumeration test account")
            # Filter for entries between 20 and 60 (should get: 20, 25, 30, 35, 45, 50, 55, 60 = 8 entries)
            result = self.client.entry.enumerate(enum_test_account_guid, EntryEnumerationQuery(
                max_results=3,
                amount_min=20.00,
                amount_max=60.00,
                ordering=EnumerationOrder.AMOUNT_ASCENDING
            ))
            self._assert(result.objects is not None, "Objects should not be None")
            self._assert(len(result.objects) == 3, f"Expected 3 entries on first page, got {len(result.objects)}")
            # Verify amounts are within range
            for entry in result.objects:
                self._assert(20.00 <= entry.amount <= 60.00,
                    f"Entry amount {entry.amount} should be between 20 and 60")
            # Verify total records reflects filtered count
            self._assert(result.total_records == 8, f"Expected 8 filtered entries, got {result.total_records}")
            self._assert(result.end_of_results == False, "Should have more pages")
            self._assert(result.records_remaining == 5, f"Expected 5 remaining, got {result.records_remaining}")

        self._run_test("Pagination: Amount Filter with Metadata", test_amount_filter_with_pagination)

        # Clean up enumeration test account
        def cleanup_enum_test_account():
            if enum_test_account_guid:
                self.client.account.delete(enum_test_account_guid)

        self._run_test("Cleanup Enumeration Test Account", cleanup_enum_test_account)

        print()

    def _run_balance_tests(self) -> None:
        """Run balance tests."""
        self._print_section_header("BALANCE TESTS")

        if not self.test_account_guid:
            print("  SKIPPED: No test account available")
            print()
            return

        def get_balance():
            self.client.balance.get(self.test_account_guid)

        self._run_test("Get Balance", get_balance)

        def get_all_balances():
            balances = self.client.balance.get_all()
            self._assert(len(balances) > 0, "Should have at least one balance")

        self._run_test("Get All Balances", get_all_balances)

        def commit_all():
            self.client.balance.commit(self.test_account_guid)

        self._run_test("Commit All Pending Entries", commit_all)

        def get_balance_after():
            self.client.balance.get(self.test_account_guid)

        self._run_test("Get Balance After Commit", get_balance_after)

        def verify_chain():
            valid = self.client.balance.verify(self.test_account_guid)
            self._assert(valid, "Balance chain should be valid")

        self._run_test("Verify Balance Chain", verify_chain)

        def add_for_selective():
            self.client.entry.add_credit(self.test_account_guid, 50.00, "Selective commit test")
            self.client.entry.add_credit(self.test_account_guid, 75.00, "Selective commit test 2")

        self._run_test("Add Entries for Selective Commit", add_for_selective)

        def commit_specific():
            pending = self.client.entry.get_pending(self.test_account_guid)
            if len(pending) > 0:
                self.client.balance.commit(self.test_account_guid, [pending[0].guid])

        self._run_test("Commit Specific Entries", commit_specific)

        def get_historical():
            self.client.balance.get_as_of(self.test_account_guid, datetime.utcnow())

        self._run_test("Get Historical Balance", get_historical)

        print()

    def _run_api_key_tests(self) -> None:
        """Run API key tests."""
        self._print_section_header("API KEY TESTS")

        created_key_guid: Optional[str] = None

        def create_key():
            nonlocal created_key_guid
            key = self.client.api_key.create("Test SDK Key", is_admin=False)
            self._assert(key.guid, "API key GUID is empty")
            self._assert(key.api_key, "API key value should be returned on creation")
            created_key_guid = key.guid

        self._run_test("Create API Key", create_key)

        def enumerate_keys():
            result = self.client.api_key.enumerate(ApiKeyEnumerationQuery(max_results=10))
            self._assert(result.objects and len(result.objects) > 0, "Should have at least one API key")

        self._run_test("Enumerate API Keys", enumerate_keys)

        def revoke_key():
            self._assert(created_key_guid, "No API key to revoke")
            self.client.api_key.revoke(created_key_guid)

        self._run_test("Revoke API Key", revoke_key)

        print()

    def _run_cleanup_tests(self) -> None:
        """Run cleanup tests to delete test data."""
        self._print_section_header("CLEANUP")

        def delete_test_account():
            if self.test_account_guid:
                self.client.account.delete(self.test_account_guid)
                exists = self.client.account.exists(self.test_account_guid)
                self._assert(not exists, "Account should have been deleted")

        if self.test_account_guid:
            self._run_test("Delete Test Account", delete_test_account)

        print()

    def _print_section_header(self, title: str) -> None:
        """Print a section header."""
        print(f"[{title}]")

    def _run_test(self, name: str, test_func: Callable[[], None]) -> None:
        """Run a single test."""
        start_time = time.time()
        success = False
        error_message: Optional[str] = None

        try:
            test_func()
            success = True
        except Exception as e:
            error_message = str(e)

        elapsed_ms = (time.time() - start_time) * 1000

        self.results.append(TestResult(
            name=name,
            success=success,
            elapsed_ms=elapsed_ms,
            error_message=error_message
        ))

        status = "PASS" if success else "FAIL"
        color = "\033[32m" if success else "\033[31m"
        reset = "\033[0m"

        print(f"  [{color}{status}{reset}] {name} ({elapsed_ms:.0f}ms)")
        if not success and error_message:
            print(f"         Error: {error_message}")

    def _assert(self, condition, message: str) -> None:
        """Assert a condition is true."""
        if not condition:
            raise AssertionError(message)

    def _print_summary(self) -> None:
        """Print the test summary."""
        total_time = (time.time() - self.total_start_time) * 1000

        print("========================================")
        print("TEST SUMMARY")
        print("========================================")
        print()

        pass_count = len([r for r in self.results if r.success])
        fail_count = len([r for r in self.results if not r.success])

        overall_status = "PASS" if fail_count == 0 else "FAIL"
        color = "\033[32m" if fail_count == 0 else "\033[31m"
        reset = "\033[0m"

        print(f"Total Tests: {len(self.results)}")
        print(f"Passed:      {pass_count}")
        print(f"Failed:      {fail_count}")
        print()
        print(f"Overall:     [{color}{overall_status}{reset}]")
        print(f"Total Time:  {total_time:.0f}ms ({total_time / 1000:.2f}s)")
        print()

        if fail_count > 0:
            print("Failed Tests:")
            for result in self.results:
                if not result.success:
                    print(f"  - {result.name}: {result.error_message}")
            print()


def main() -> None:
    """Main entry point."""
    if len(sys.argv) < 3:
        print("Usage: python test_harness.py <endpoint> <api-key>")
        print()
        print("Example: python test_harness.py http://localhost:8080 your-api-key-here")
        sys.exit(1)

    endpoint = sys.argv[1]
    api_key = sys.argv[2]

    print(f"API Key: {api_key[:min(8, len(api_key))]}...")

    harness = TestHarness(endpoint, api_key)
    exit_code = harness.run()
    sys.exit(exit_code)


if __name__ == "__main__":
    main()

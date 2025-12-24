"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.EnumerationOrder = exports.EntryType = void 0;
/**
 * Entry type enumeration.
 */
var EntryType;
(function (EntryType) {
    /** Credit entry (increases balance). */
    EntryType[EntryType["Credit"] = 0] = "Credit";
    /** Debit entry (decreases balance). */
    EntryType[EntryType["Debit"] = 1] = "Debit";
    /** Balance snapshot entry. */
    EntryType[EntryType["Balance"] = 2] = "Balance";
})(EntryType || (exports.EntryType = EntryType = {}));
/**
 * Enumeration order options.
 */
var EnumerationOrder;
(function (EnumerationOrder) {
    /** Order by creation date, ascending (oldest first). */
    EnumerationOrder[EnumerationOrder["CreatedAscending"] = 0] = "CreatedAscending";
    /** Order by creation date, descending (newest first). */
    EnumerationOrder[EnumerationOrder["CreatedDescending"] = 1] = "CreatedDescending";
    /** Order by amount, ascending (smallest first). */
    EnumerationOrder[EnumerationOrder["AmountAscending"] = 2] = "AmountAscending";
    /** Order by amount, descending (largest first). */
    EnumerationOrder[EnumerationOrder["AmountDescending"] = 3] = "AmountDescending";
})(EnumerationOrder || (exports.EnumerationOrder = EnumerationOrder = {}));
//# sourceMappingURL=index.js.map
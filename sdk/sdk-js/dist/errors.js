"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.NetLedgerValidationError = exports.NetLedgerApiError = exports.NetLedgerConnectionError = exports.NetLedgerError = void 0;
/**
 * Base exception for all NetLedger SDK errors.
 */
class NetLedgerError extends Error {
    constructor(message) {
        super(message);
        this.name = 'NetLedgerError';
        Object.setPrototypeOf(this, NetLedgerError.prototype);
    }
}
exports.NetLedgerError = NetLedgerError;
/**
 * Error thrown when unable to connect to the server.
 */
class NetLedgerConnectionError extends NetLedgerError {
    constructor(message, cause) {
        super(message);
        this.name = 'NetLedgerConnectionError';
        this.cause = cause;
        Object.setPrototypeOf(this, NetLedgerConnectionError.prototype);
    }
}
exports.NetLedgerConnectionError = NetLedgerConnectionError;
/**
 * Error thrown when the API returns an error response.
 */
class NetLedgerApiError extends NetLedgerError {
    constructor(statusCode, message, details) {
        super(message);
        this.name = 'NetLedgerApiError';
        this.statusCode = statusCode;
        this.details = details;
        Object.setPrototypeOf(this, NetLedgerApiError.prototype);
    }
}
exports.NetLedgerApiError = NetLedgerApiError;
/**
 * Error thrown when input validation fails.
 */
class NetLedgerValidationError extends NetLedgerError {
    constructor(message, parameterName) {
        super(message);
        this.name = 'NetLedgerValidationError';
        this.parameterName = parameterName;
        Object.setPrototypeOf(this, NetLedgerValidationError.prototype);
    }
}
exports.NetLedgerValidationError = NetLedgerValidationError;
//# sourceMappingURL=errors.js.map
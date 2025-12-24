/**
 * Base exception for all NetLedger SDK errors.
 */
export declare class NetLedgerError extends Error {
    constructor(message: string);
}
/**
 * Error thrown when unable to connect to the server.
 */
export declare class NetLedgerConnectionError extends NetLedgerError {
    /** The underlying error, if any. */
    readonly cause?: Error;
    constructor(message: string, cause?: Error);
}
/**
 * Error thrown when the API returns an error response.
 */
export declare class NetLedgerApiError extends NetLedgerError {
    /** HTTP status code. */
    readonly statusCode: number;
    /** Additional error details. */
    readonly details?: string;
    constructor(statusCode: number, message: string, details?: string);
}
/**
 * Error thrown when input validation fails.
 */
export declare class NetLedgerValidationError extends NetLedgerError {
    /** The name of the invalid parameter. */
    readonly parameterName?: string;
    constructor(message: string, parameterName?: string);
}
//# sourceMappingURL=errors.d.ts.map
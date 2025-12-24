/**
 * Base exception for all NetLedger SDK errors.
 */
export class NetLedgerError extends Error {
    constructor(message: string) {
        super(message);
        this.name = 'NetLedgerError';
        Object.setPrototypeOf(this, NetLedgerError.prototype);
    }
}

/**
 * Error thrown when unable to connect to the server.
 */
export class NetLedgerConnectionError extends NetLedgerError {
    /** The underlying error, if any. */
    public readonly cause?: Error;

    constructor(message: string, cause?: Error) {
        super(message);
        this.name = 'NetLedgerConnectionError';
        this.cause = cause;
        Object.setPrototypeOf(this, NetLedgerConnectionError.prototype);
    }
}

/**
 * Error thrown when the API returns an error response.
 */
export class NetLedgerApiError extends NetLedgerError {
    /** HTTP status code. */
    public readonly statusCode: number;
    /** Additional error details. */
    public readonly details?: string;

    constructor(statusCode: number, message: string, details?: string) {
        super(message);
        this.name = 'NetLedgerApiError';
        this.statusCode = statusCode;
        this.details = details;
        Object.setPrototypeOf(this, NetLedgerApiError.prototype);
    }
}

/**
 * Error thrown when input validation fails.
 */
export class NetLedgerValidationError extends NetLedgerError {
    /** The name of the invalid parameter. */
    public readonly parameterName?: string;

    constructor(message: string, parameterName?: string) {
        super(message);
        this.name = 'NetLedgerValidationError';
        this.parameterName = parameterName;
        Object.setPrototypeOf(this, NetLedgerValidationError.prototype);
    }
}

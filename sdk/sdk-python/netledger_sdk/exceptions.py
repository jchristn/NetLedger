"""Exceptions for the NetLedger SDK."""

from typing import Optional


class NetLedgerError(Exception):
    """Base exception for all NetLedger SDK errors."""
    pass


class NetLedgerConnectionError(NetLedgerError):
    """Exception thrown when unable to connect to the server."""

    def __init__(self, message: str, cause: Optional[Exception] = None):
        super().__init__(message)
        self.cause = cause


class NetLedgerApiError(NetLedgerError):
    """Exception thrown when the API returns an error response."""

    def __init__(self, status_code: int, message: str, details: Optional[str] = None):
        super().__init__(message)
        self.status_code = status_code
        self.details = details


class NetLedgerValidationError(NetLedgerError):
    """Exception thrown when input validation fails."""

    def __init__(self, message: str, parameter_name: Optional[str] = None):
        super().__init__(message)
        self.parameter_name = parameter_name

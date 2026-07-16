"""Request history operations for the NetLedger API."""

from typing import Optional
from urllib.parse import urlencode

from ..http_client import HttpClient
from ..models import (
    EnumerationResult,
    RequestHistoryDeleteResult,
    RequestHistoryEntry,
    RequestHistoryQuery,
    RequestHistorySummary
)


class RequestHistoryMethods:
    """Request history operations."""

    def __init__(self, client: HttpClient):
        """Initialize request history methods."""
        self._client = client

    def enumerate(self, query: Optional[RequestHistoryQuery] = None) -> EnumerationResult:
        """Enumerate request history entries."""
        response = self._client.get('/v1.0/api/request-history' + self._build_query_string(query, False))
        return EnumerationResult.from_dict(response.data or {}, RequestHistoryEntry.from_dict)

    def summarize(self, query: Optional[RequestHistoryQuery] = None) -> RequestHistorySummary:
        """Summarize request history entries."""
        response = self._client.get('/v1.0/api/request-history/summary' + self._build_query_string(query, True))
        if not response.data:
            raise ValueError('No data returned from server')
        return RequestHistorySummary.from_dict(response.data)

    def read(self, request_id: str) -> RequestHistoryEntry:
        """Read one request history entry."""
        response = self._client.get(f'/v1.0/api/request-history/{request_id}')
        if not response.data:
            raise ValueError('No data returned from server')
        return RequestHistoryEntry.from_dict(response.data)

    def delete(self, request_id: str) -> RequestHistoryDeleteResult:
        """Delete one request history entry."""
        response = self._client.delete_with_response(f'/v1.0/api/request-history/{request_id}')
        return RequestHistoryDeleteResult.from_dict(response.data or {})

    def delete_many(self, query: Optional[RequestHistoryQuery] = None) -> RequestHistoryDeleteResult:
        """Delete matching request history entries."""
        response = self._client.delete_with_response('/v1.0/api/request-history' + self._build_query_string(query, False))
        return RequestHistoryDeleteResult.from_dict(response.data or {})

    def _build_query_string(self, query: Optional[RequestHistoryQuery], include_bucket_minutes: bool) -> str:
        if query is None:
            query = RequestHistoryQuery()

        params = {
            'maxResults': query.max_results,
            'skip': query.skip
        }
        if query.tenant_id:
            params['tenantId'] = query.tenant_id
        if query.principal_id:
            params['principalId'] = query.principal_id
        if query.method:
            params['method'] = query.method
        if query.status_code is not None:
            params['statusCode'] = query.status_code
        if query.path_contains:
            params['pathContains'] = query.path_contains
        if query.from_utc:
            params['fromUtc'] = query.from_utc
        if query.to_utc:
            params['toUtc'] = query.to_utc
        if include_bucket_minutes:
            params['bucketMinutes'] = query.bucket_minutes

        query_string = urlencode(params)
        return f'?{query_string}' if query_string else ''

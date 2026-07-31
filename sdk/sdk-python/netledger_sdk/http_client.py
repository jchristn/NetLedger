"""HTTP client for making API requests."""

import requests
from requests.adapters import HTTPAdapter
from typing import Any, Dict, Optional, TypeVar, Generic
from .exceptions import NetLedgerConnectionError, NetLedgerApiError


T = TypeVar('T')


class ApiResponse(Generic[T]):
    """Represents an API response."""

    def __init__(self, data: Optional[T], status_code: int, request_id: Optional[str] = None):
        self.data = data
        self.status_code = status_code
        self.request_id = request_id


class HttpClient:
    """HTTP client for making API requests."""

    def __init__(
        self,
        base_url: str,
        api_key: str,
        timeout_seconds: float = 30.0,
        tenant_id: Optional[str] = None,
        pool_connections: int = 10,
        pool_maxsize: int = 20
    ):
        """
        Initialize the HTTP client.

        Args:
            base_url: The base URL of the NetLedger server.
            api_key: The API key for authentication.
            timeout_seconds: Request timeout in seconds.
        """
        self.base_url = base_url.rstrip('/')
        self.api_key = api_key
        self.tenant_id = tenant_id
        self.timeout = timeout_seconds
        self.session = requests.Session()
        adapter = HTTPAdapter(pool_connections=pool_connections, pool_maxsize=pool_maxsize)
        self.session.mount('http://', adapter)
        self.session.mount('https://', adapter)
        headers = {
            'Authorization': f'Bearer {api_key}',
            'Accept': 'application/json',
            'Content-Type': 'application/json'
        }
        if tenant_id:
            headers['x-tenant-id'] = tenant_id
        self.session.headers.update(headers)

    def get(self, path: str) -> ApiResponse:
        """Make a GET request."""
        return self._request('GET', path)

    def put(self, path: str, body: Optional[Any] = None) -> ApiResponse:
        """Make a PUT request."""
        return self._request('PUT', path, body)

    def put_raw(
        self,
        path: str,
        body: Any,
        content_type: str = 'application/octet-stream',
        headers: Optional[Dict[str, str]] = None
    ) -> ApiResponse:
        """Make a PUT request with raw content."""
        return self._raw_request('PUT', path, body, content_type, headers)

    def post(self, path: str, body: Optional[Any] = None) -> ApiResponse:
        """Make a POST request."""
        return self._request('POST', path, body)

    def delete(self, path: str) -> None:
        """Make a DELETE request."""
        self._request('DELETE', path)

    def delete_with_response(self, path: str) -> ApiResponse:
        """Make a DELETE request and return a response body."""
        return self._request('DELETE', path)

    def head(self, path: str) -> bool:
        """Make a HEAD request to check if a resource exists."""
        try:
            url = f'{self.base_url}{path}'
            response = self.session.head(url, timeout=self.timeout)
            return 200 <= response.status_code < 300
        except requests.exceptions.Timeout:
            raise NetLedgerConnectionError('Request timed out')
        except requests.exceptions.RequestException as e:
            raise NetLedgerConnectionError('Failed to connect to the server', e)

    def _request(self, method: str, path: str, body: Optional[Any] = None) -> ApiResponse:
        """Make an HTTP request."""
        try:
            url = f'{self.base_url}{path}'

            kwargs: Dict[str, Any] = {'timeout': self.timeout}
            if body is not None:
                kwargs['json'] = body

            response = self.session.request(method, url, **kwargs)

            if not (200 <= response.status_code < 300):
                error_message = response.reason or 'Unknown error'
                error_details = None

                try:
                    error_data = response.json()
                    error_message = error_data.get('message', error_message)
                    error_details = error_data.get('description')
                except (ValueError, KeyError):
                    pass

                raise NetLedgerApiError(response.status_code, error_message, error_details)

            # Get request identifier from header
            request_id = response.headers.get('x-request-id')

            if not response.text or response.text.strip() == '':
                return ApiResponse(None, response.status_code, request_id)

            try:
                data = response.json()
                return ApiResponse(
                    data=data,
                    status_code=response.status_code,
                    request_id=request_id
                )
            except ValueError:
                raise NetLedgerApiError(response.status_code, 'Failed to parse response')

        except requests.exceptions.Timeout:
            raise NetLedgerConnectionError('Request timed out')
        except requests.exceptions.RequestException as e:
            raise NetLedgerConnectionError('Failed to connect to the server', e)

    def _raw_request(
        self,
        method: str,
        path: str,
        body: Any,
        content_type: str,
        headers: Optional[Dict[str, str]] = None
    ) -> ApiResponse:
        """Make an HTTP request with raw content."""
        try:
            url = f'{self.base_url}{path}'
            request_headers = {'Content-Type': content_type or 'application/octet-stream'}
            if headers:
                request_headers.update(headers)

            response = self.session.request(
                method,
                url,
                data=body,
                headers=request_headers,
                timeout=self.timeout
            )

            if not (200 <= response.status_code < 300):
                error_message = response.reason or 'Unknown error'
                error_details = None

                try:
                    error_data = response.json()
                    error_message = error_data.get('message', error_message)
                    error_details = error_data.get('description')
                except (ValueError, KeyError):
                    pass

                raise NetLedgerApiError(response.status_code, error_message, error_details)

            request_id = response.headers.get('x-request-id')
            if not response.text or response.text.strip() == '':
                return ApiResponse(None, response.status_code, request_id)

            try:
                data = response.json()
                return ApiResponse(
                    data=data,
                    status_code=response.status_code,
                    request_id=request_id
                )
            except ValueError:
                raise NetLedgerApiError(response.status_code, 'Failed to parse response')

        except requests.exceptions.Timeout:
            raise NetLedgerConnectionError('Request timed out')
        except requests.exceptions.RequestException as e:
            raise NetLedgerConnectionError('Failed to connect to the server', e)

    def close(self) -> None:
        """Close the session."""
        self.session.close()

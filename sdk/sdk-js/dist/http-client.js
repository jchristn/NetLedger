"use strict";
var __createBinding = (this && this.__createBinding) || (Object.create ? (function(o, m, k, k2) {
    if (k2 === undefined) k2 = k;
    var desc = Object.getOwnPropertyDescriptor(m, k);
    if (!desc || ("get" in desc ? !m.__esModule : desc.writable || desc.configurable)) {
      desc = { enumerable: true, get: function() { return m[k]; } };
    }
    Object.defineProperty(o, k2, desc);
}) : (function(o, m, k, k2) {
    if (k2 === undefined) k2 = k;
    o[k2] = m[k];
}));
var __setModuleDefault = (this && this.__setModuleDefault) || (Object.create ? (function(o, v) {
    Object.defineProperty(o, "default", { enumerable: true, value: v });
}) : function(o, v) {
    o["default"] = v;
});
var __importStar = (this && this.__importStar) || (function () {
    var ownKeys = function(o) {
        ownKeys = Object.getOwnPropertyNames || function (o) {
            var ar = [];
            for (var k in o) if (Object.prototype.hasOwnProperty.call(o, k)) ar[ar.length] = k;
            return ar;
        };
        return ownKeys(o);
    };
    return function (mod) {
        if (mod && mod.__esModule) return mod;
        var result = {};
        if (mod != null) for (var k = ownKeys(mod), i = 0; i < k.length; i++) if (k[i] !== "default") __createBinding(result, mod, k[i]);
        __setModuleDefault(result, mod);
        return result;
    };
})();
Object.defineProperty(exports, "__esModule", { value: true });
exports.HttpClient = void 0;
const http = __importStar(require("http"));
const https = __importStar(require("https"));
const url_1 = require("url");
const errors_1 = require("./errors");
/**
 * HTTP client for making API requests.
 */
class HttpClient {
    constructor(baseUrl, apiKey, timeoutMs = 30000) {
        this.baseUrl = baseUrl.replace(/\/$/, '');
        this.apiKey = apiKey;
        this.timeoutMs = timeoutMs;
    }
    /**
     * Make a GET request.
     */
    async get(path) {
        return this.request('GET', path);
    }
    /**
     * Make a PUT request.
     */
    async put(path, body) {
        return this.request('PUT', path, body);
    }
    /**
     * Make a POST request.
     */
    async post(path, body) {
        return this.request('POST', path, body);
    }
    /**
     * Make a DELETE request.
     */
    async delete(path) {
        await this.request('DELETE', path);
    }
    /**
     * Make a HEAD request.
     */
    async head(path) {
        return new Promise((resolve, reject) => {
            const url = new url_1.URL(path, this.baseUrl);
            const isHttps = url.protocol === 'https:';
            const lib = isHttps ? https : http;
            const options = {
                method: 'HEAD',
                hostname: url.hostname,
                port: url.port || (isHttps ? 443 : 80),
                path: url.pathname + url.search,
                timeout: this.timeoutMs,
                headers: {
                    'Authorization': `Bearer ${this.apiKey}`,
                    'Accept': 'application/json'
                }
            };
            const req = lib.request(options, (res) => {
                resolve(res.statusCode !== undefined && res.statusCode >= 200 && res.statusCode < 300);
            });
            req.on('error', (err) => {
                reject(new errors_1.NetLedgerConnectionError('Failed to connect to the server', err));
            });
            req.on('timeout', () => {
                req.destroy();
                reject(new errors_1.NetLedgerConnectionError('Request timed out'));
            });
            req.end();
        });
    }
    /**
     * Make an HTTP request.
     */
    request(method, path, body) {
        return new Promise((resolve, reject) => {
            const url = new url_1.URL(path, this.baseUrl);
            const isHttps = url.protocol === 'https:';
            const lib = isHttps ? https : http;
            const bodyData = body !== null && body !== undefined ? JSON.stringify(body) : undefined;
            const options = {
                method,
                hostname: url.hostname,
                port: url.port || (isHttps ? 443 : 80),
                path: url.pathname + url.search,
                timeout: this.timeoutMs,
                headers: {
                    'Authorization': `Bearer ${this.apiKey}`,
                    'Accept': 'application/json',
                    ...(bodyData ? { 'Content-Type': 'application/json', 'Content-Length': Buffer.byteLength(bodyData) } : {})
                }
            };
            const req = lib.request(options, (res) => {
                let data = '';
                res.on('data', (chunk) => {
                    data += chunk;
                });
                res.on('end', () => {
                    const statusCode = res.statusCode || 0;
                    if (statusCode < 200 || statusCode >= 300) {
                        let errorMessage = res.statusMessage || 'Unknown error';
                        let errorDetails;
                        if (data) {
                            try {
                                const errorResponse = JSON.parse(data);
                                errorMessage = errorResponse.Message || errorMessage;
                                errorDetails = errorResponse.Description;
                            }
                            catch {
                                // Failed to parse error response
                            }
                        }
                        reject(new errors_1.NetLedgerApiError(statusCode, errorMessage, errorDetails));
                        return;
                    }
                    if (!data || data.trim() === '') {
                        resolve({ StatusCode: statusCode });
                        return;
                    }
                    try {
                        const parsedData = JSON.parse(data);
                        // Server sends data directly, wrap it in ApiResponse format
                        const response = {
                            StatusCode: statusCode,
                            Data: parsedData
                        };
                        resolve(response);
                    }
                    catch (err) {
                        reject(new errors_1.NetLedgerApiError(statusCode, 'Failed to parse response'));
                    }
                });
            });
            req.on('error', (err) => {
                reject(new errors_1.NetLedgerConnectionError('Failed to connect to the server', err));
            });
            req.on('timeout', () => {
                req.destroy();
                reject(new errors_1.NetLedgerConnectionError('Request timed out'));
            });
            if (bodyData) {
                req.write(bodyData);
            }
            req.end();
        });
    }
}
exports.HttpClient = HttpClient;
//# sourceMappingURL=http-client.js.map
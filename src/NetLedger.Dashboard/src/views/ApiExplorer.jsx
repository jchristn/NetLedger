import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { useApp } from '../context/useApp'
import CopyButton from '../components/CopyButton'
import Modal, { ConfirmModal } from '../components/Modal'
import { buildCodeSnippets, buildRequestPath, flattenOpenApiSpec, getParameterDefault, getRequestBodyTemplate } from '../utils/openApi'
import { formatDate } from '../api/api'
import './ApiExplorer.css'

const STORAGE_KEY = 'netledger_api_explorer_state'
const HISTORY_KEY = 'netledger_api_explorer_history'
const MAX_HISTORY_ITEMS = 12

function loadJsonStorage(key, fallback) {
  try {
    const saved = localStorage.getItem(key)
    return saved ? JSON.parse(saved) : fallback
  } catch {
    return fallback
  }
}

function buildDefaultParameterValues(spec, operation) {
  return operation.parameters.reduce((result, parameter) => {
    result[parameter.name] = getParameterDefault(spec, parameter)
    return result
  }, {})
}

function prettyPrint(value) {
  if (value === null || value === undefined || value === '') return '(empty)'
  if (typeof value === 'string') {
    try {
      return JSON.stringify(JSON.parse(value), null, 2)
    } catch {
      return value
    }
  }
  return JSON.stringify(value, null, 2)
}

function formatDuration(value) {
  if (!value && value !== 0) return '-'
  return `${value.toFixed(1)} ms`
}

function formatBytes(value) {
  if (!value) return '0 B'
  if (value < 1024) return `${value} B`
  if (value < 1024 * 1024) return `${(value / 1024).toFixed(1)} KB`
  return `${(value / (1024 * 1024)).toFixed(1)} MB`
}

export default function ApiExplorer() {
  const { api, serverUrl, setError } = useApp()
  const savedState = useMemo(() => loadJsonStorage(STORAGE_KEY, { selectedOperationKey: '' }), [])
  const [spec, setSpec] = useState(null)
  const [loading, setLoading] = useState(true)
  const [refreshing, setRefreshing] = useState(false)
  const [sending, setSending] = useState(false)
  const [selectedOperationKey, setSelectedOperationKey] = useState(savedState.selectedOperationKey || '')
  const [parameterValues, setParameterValues] = useState({})
  const [extraQueryText, setExtraQueryText] = useState('')
  const [headerText, setHeaderText] = useState('')
  const [requestBody, setRequestBody] = useState('')
  const [responseTab, setResponseTab] = useState('body')
  const [codeLanguage, setCodeLanguage] = useState('curl')
  const [operationTag, setOperationTag] = useState('all')
  const [response, setResponse] = useState(null)
  const [sendError, setSendError] = useState('')
  const [history, setHistory] = useState(() => loadJsonStorage(HISTORY_KEY, []))
  const [confirmSend, setConfirmSend] = useState(false)
  const [historyModalOpen, setHistoryModalOpen] = useState(false)
  const abortControllerRef = useRef(null)
  const restoringRef = useRef(false)

  const operations = useMemo(() => flattenOpenApiSpec(spec || {}), [spec])
  const operationTags = useMemo(() => ['all', ...new Set(operations.map((operation) => operation.tag).filter(Boolean))], [operations])
  const visibleOperations = useMemo(() => {
    if (operationTag === 'all') return operations
    return operations.filter((operation) => operation.tag === operationTag)
  }, [operationTag, operations])
  const groupedOperations = useMemo(() => {
    return visibleOperations.reduce((result, operation) => {
      if (!result[operation.tag]) result[operation.tag] = []
      result[operation.tag].push(operation)
      return result
    }, {})
  }, [visibleOperations])
  const selectedOperation = useMemo(
    () => operations.find((operation) => operation.key === selectedOperationKey) || null,
    [operations, selectedOperationKey]
  )
  const pathParameters = selectedOperation?.parameters.filter((parameter) => parameter.in === 'path') || []
  const queryParameters = selectedOperation?.parameters.filter((parameter) => parameter.in === 'query') || []

  const parsedExtraQuery = useMemo(() => parseKeyValueText(extraQueryText), [extraQueryText])
  const parsedHeaders = useMemo(() => parseKeyValueText(headerText), [headerText])
  const requestPath = selectedOperation ? buildRequestPath(selectedOperation, parameterValues, parsedExtraQuery) : ''
  const codeSnippets = useMemo(() => {
    if (!selectedOperation || !requestPath) return { curl: '', javascript: '', csharp: '' }

    const headers = { ...parsedHeaders }
    const body = requestBody.trim() && selectedOperation.method !== 'GET' && selectedOperation.method !== 'HEAD' ? requestBody : ''
    if (body && !headers['Content-Type'] && !headers['content-type']) headers['Content-Type'] = 'application/json'
    return buildCodeSnippets(serverUrl, selectedOperation.method, requestPath, headers, body)
  }, [parsedHeaders, requestBody, requestPath, selectedOperation, serverUrl])

  const loadSpec = useCallback(async (showRefreshing) => {
    try {
      if (showRefreshing) setRefreshing(true)
      else setLoading(true)
      setSpec(await api.getOpenApiSpec())
    } catch (err) {
      setError(`Failed to load OpenAPI document: ${err.message}`)
    } finally {
      setLoading(false)
      setRefreshing(false)
    }
  }, [api, setError])

  useEffect(() => {
    localStorage.setItem(STORAGE_KEY, JSON.stringify({ selectedOperationKey }))
  }, [selectedOperationKey])

  useEffect(() => {
    localStorage.setItem(HISTORY_KEY, JSON.stringify(history.slice(0, MAX_HISTORY_ITEMS)))
  }, [history])

  useEffect(() => {
    loadSpec(false)
  }, [loadSpec])

  useEffect(() => {
    if (!operations.length) return
    if (!selectedOperationKey || !operations.some((operation) => operation.key === selectedOperationKey)) {
      setSelectedOperationKey(operations[0].key)
    }
  }, [operations, selectedOperationKey])

  useEffect(() => {
    if (visibleOperations.length > 0 && !visibleOperations.some((operation) => operation.key === selectedOperationKey)) {
      setSelectedOperationKey(visibleOperations[0].key)
    }
  }, [selectedOperationKey, visibleOperations])

  useEffect(() => {
    if (!selectedOperation || !spec) return
    if (restoringRef.current) {
      restoringRef.current = false
      return
    }

    setParameterValues(buildDefaultParameterValues(spec, selectedOperation))
    setExtraQueryText('')
    setHeaderText('')
    setRequestBody(getRequestBodyTemplate(spec, selectedOperation))
    setSendError('')
  }, [selectedOperation, spec])

  function resetRequest() {
    if (!selectedOperation || !spec) return
    setParameterValues(buildDefaultParameterValues(spec, selectedOperation))
    setExtraQueryText('')
    setHeaderText('')
    setRequestBody(getRequestBodyTemplate(spec, selectedOperation))
    setResponse(null)
    setSendError('')
  }

  function updateParameter(name, value) {
    setParameterValues((current) => ({ ...current, [name]: value }))
  }

  function validateRequest() {
    if (!selectedOperation) return 'Select an operation.'
    const missingParameter = pathParameters.find((parameter) => !parameterValues[parameter.name])
    if (missingParameter) return `${missingParameter.name} is required.`
    if (requestBody.trim() && selectedOperation.method !== 'GET' && selectedOperation.method !== 'HEAD') {
      try {
        JSON.parse(requestBody)
      } catch {
        return 'Request body must be valid JSON.'
      }
    }
    return ''
  }

  async function sendRequest() {
    const validationError = validateRequest()
    if (validationError) {
      setSendError(validationError)
      return
    }

    if (selectedOperation.method === 'DELETE' && !confirmSend) {
      setConfirmSend(true)
      return
    }

    setConfirmSend(false)
    setSending(true)
    setSendError('')
    setResponse(null)
    abortControllerRef.current = new AbortController()

    try {
      const body = requestBody.trim() && selectedOperation.method !== 'GET' && selectedOperation.method !== 'HEAD' ? requestBody : null
      const result = await api.requestRaw(selectedOperation.method, requestPath, {
        body,
        headers: parsedHeaders,
        signal: abortControllerRef.current.signal
      })
      setResponse(result)
      if (!result.ok) setSendError(`HTTP ${result.status}: ${result.statusText}`)
      setHistory((current) => [
        {
          id: `${Date.now()}-${Math.random().toString(16).slice(2)}`,
          timestampUtc: new Date().toISOString(),
          operationKey: selectedOperation.key,
          method: selectedOperation.method,
          path: requestPath,
          status: result.status,
          durationMs: result.durationMs,
          parameterValues,
          extraQueryText,
          headerText,
          requestBody
        },
        ...current.filter((item) => item.operationKey !== selectedOperation.key || item.path !== requestPath)
      ].slice(0, MAX_HISTORY_ITEMS))
    } catch (err) {
      if (err.name !== 'AbortError') {
        setSendError(err.message || 'Request failed')
      }
    } finally {
      setSending(false)
      abortControllerRef.current = null
    }
  }

  function restoreHistoryItem(item) {
    const operationExists = operations.some((operation) => operation.key === item.operationKey)
    if (operationExists) {
      restoringRef.current = true
      setSelectedOperationKey(item.operationKey)
    }
    setParameterValues(item.parameterValues || {})
    setExtraQueryText(item.extraQueryText || '')
    setHeaderText(item.headerText || '')
    setRequestBody(item.requestBody || '')
    setHistoryModalOpen(false)
  }

  const responseBodyText = response ? (response.text || prettyPrint(response.json)) : '(no response yet)'

  return (
    <div className="api-explorer-page">
      <div className="page-header">
        <div className="page-header-left">
          <h2 className="page-title">API Explorer</h2>
          <p className="page-description">Explore and execute NetLedger REST operations with your current session.</p>
        </div>
        <div className="page-header-actions">
          <button className="btn btn-secondary" onClick={() => setHistoryModalOpen(true)}>
            History
          </button>
          <button className="btn btn-secondary" onClick={() => loadSpec(true)} disabled={refreshing}>
            {refreshing ? 'Refreshing...' : 'Refresh Spec'}
          </button>
        </div>
      </div>

      <div className="api-explorer-grid">
        <section className="api-explorer-panel">
          <div className="section-header">
            <div>
              <h3>Operations</h3>
              <p>Filter by OpenAPI tag and select the route to execute.</p>
            </div>
          </div>
          {loading ? (
            <div className="page-loading"><span className="spinner"></span><span>Loading...</span></div>
          ) : (
            <div className="operation-list">
              <label className="operation-filter">
                <span>Endpoint Group</span>
                <select value={operationTag} onChange={(event) => setOperationTag(event.target.value)}>
                  {operationTags.map((tag) => (
                    <option key={tag} value={tag}>{tag === 'all' ? 'All endpoints' : tag}</option>
                  ))}
                </select>
              </label>
              {Object.entries(groupedOperations).map(([tag, tagOperations]) => (
                <div key={tag} className="operation-group">
                  <div className="operation-group-title">{tag}</div>
                  {tagOperations.map((operation) => (
                    <button
                      key={operation.key}
                      className={`operation-item ${operation.key === selectedOperationKey ? 'active' : ''}`}
                      onClick={() => setSelectedOperationKey(operation.key)}
                    >
                      <span className={`method-pill method-${operation.method.toLowerCase()}`}>{operation.method}</span>
                      <span className="operation-summary">{operation.summary}</span>
                      <span className="operation-path">{operation.path}</span>
                    </button>
                  ))}
                </div>
              ))}
            </div>
          )}
        </section>

        <section className="api-explorer-panel api-explorer-workspace">
          <div className="section-header">
            <h3>{selectedOperation ? selectedOperation.summary : 'Request'}</h3>
            <div className="request-path-copy">
              <code>{requestPath || '-'}</code>
              <CopyButton text={requestPath} title="Copy path" />
            </div>
          </div>

          <div className="api-explorer-work-grid">
            <div className="api-request-builder">
              <div className="request-toolbar">
                <button className="btn btn-primary" onClick={sendRequest} disabled={!selectedOperation || sending}>
                  {sending ? 'Sending...' : 'Send'}
                </button>
                {sending && (
                  <button className="btn btn-secondary" onClick={() => abortControllerRef.current?.abort()}>
                    Cancel
                  </button>
                )}
                <button className="btn btn-secondary" onClick={resetRequest} disabled={!selectedOperation}>
                  Reset
                </button>
              </div>

              {sendError && <div className="inline-error">{sendError}</div>}

              <div className="route-preview">
                <span className={`method-pill method-${String(selectedOperation?.method || '').toLowerCase()}`}>{selectedOperation?.method || '-'}</span>
                <code>{selectedOperation?.path || '-'}</code>
                <span>{selectedOperation?.summary || 'Select an operation'}</span>
              </div>

              <div className="request-editor-grid">
                <ParameterSection title="Path Parameters" parameters={pathParameters} values={parameterValues} onChange={updateParameter} />
                <ParameterSection title="Query Parameters" parameters={queryParameters} values={parameterValues} onChange={updateParameter} />
              </div>

              <div className="form-grid two-columns">
                <label className="form-group">
                  <span>Extra Query</span>
                  <textarea value={extraQueryText} onChange={(event) => setExtraQueryText(event.target.value)} placeholder={'key=value\ninclude=details'} />
                </label>
                <label className="form-group">
                  <span>Headers</span>
                  <textarea value={headerText} onChange={(event) => setHeaderText(event.target.value)} placeholder={'x-correlation-id=demo'} />
                </label>
              </div>

              {selectedOperation && selectedOperation.method !== 'GET' && selectedOperation.method !== 'HEAD' && (
                <label className="form-group request-body-group">
                  <span>JSON Body</span>
                  <textarea className="mono-textarea" value={requestBody} onChange={(event) => setRequestBody(event.target.value)} />
                </label>
              )}
            </div>

            <div className="api-response">
              <div className="response-header">
                <div>
                  <h3>Response</h3>
                  <div className="response-status">
                    <span className={response?.ok ? 'status-ok' : response ? 'status-error' : ''}>{response ? response.status : '-'}</span>
                    <span>{response ? formatDuration(response.durationMs) : '-'}</span>
                    <span>{response ? formatBytes(response.text?.length || 0) : '-'}</span>
                  </div>
                </div>
                <div className="tab-buttons">
                  {['body', 'headers', 'code'].map((tab) => (
                    <button key={tab} className={responseTab === tab ? 'active' : ''} onClick={() => setResponseTab(tab)}>
                      {tab}
                    </button>
                  ))}
                </div>
              </div>
              {responseTab === 'body' && <CodeBlock text={prettyPrint(responseBodyText)} title="Copy response" />}
              {responseTab === 'headers' && <CodeBlock text={response ? prettyPrint(response.headers) : '(no response yet)'} title="Copy headers" />}
              {responseTab === 'code' && (
                <div>
                  <div className="tab-buttons code-tabs">
                    {['curl', 'javascript', 'csharp'].map((language) => (
                      <button key={language} className={codeLanguage === language ? 'active' : ''} onClick={() => setCodeLanguage(language)}>
                        {language}
                      </button>
                    ))}
                  </div>
                  <CodeBlock text={codeSnippets[codeLanguage]} title="Copy snippet" />
                </div>
              )}
            </div>
          </div>
        </section>
      </div>

      <Modal isOpen={historyModalOpen} onClose={() => setHistoryModalOpen(false)} title="Explorer History" size="large">
        <div className="history-list">
          {history.length === 0 ? (
            <div className="empty-state"><span className="empty-state-title">No explorer requests yet</span></div>
          ) : history.map((item) => (
            <button key={item.id} className="history-item" onClick={() => restoreHistoryItem(item)}>
              <span className={`method-pill method-${item.method.toLowerCase()}`}>{item.method}</span>
              <span className="history-path">{item.path}</span>
              <span className={item.status >= 200 && item.status < 400 ? 'status-ok' : 'status-error'}>{item.status}</span>
              <span className="history-timestamp">{formatDate(item.timestampUtc)}</span>
            </button>
          ))}
        </div>
      </Modal>

      <ConfirmModal
        isOpen={confirmSend}
        onClose={() => setConfirmSend(false)}
        onConfirm={sendRequest}
        title="Send DELETE Request"
        message="This operation may delete data. Confirm that you want to send it."
        confirmText="Send DELETE"
      />
    </div>
  )
}

function ParameterSection({ title, parameters, values, onChange }) {
  return (
    <div className="parameter-section">
      <h4>{title}</h4>
      {parameters.length === 0 ? (
        <div className="parameter-empty">None</div>
      ) : parameters.map((parameter) => (
        <label className="form-group parameter-field" key={`${parameter.in}-${parameter.name}`}>
          <span>{parameter.name}{parameter.required ? ' *' : ''}</span>
          <input value={values[parameter.name] || ''} onChange={(event) => onChange(parameter.name, event.target.value)} />
        </label>
      ))}
    </div>
  )
}

function CodeBlock({ text, title }) {
  return (
    <div className="code-block">
      <div className="code-block-toolbar">
        <span>Content</span>
        <CopyButton text={text} title={title} />
      </div>
      <pre>{text}</pre>
    </div>
  )
}

function parseKeyValueText(text) {
  return text
    .split('\n')
    .map((line) => line.trim())
    .filter(Boolean)
    .reduce((result, line) => {
      const separator = line.indexOf('=')
      if (separator <= 0) return result
      result[line.slice(0, separator).trim()] = line.slice(separator + 1).trim()
      return result
    }, {})
}

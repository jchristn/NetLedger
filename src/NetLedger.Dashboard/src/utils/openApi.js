const HTTP_METHODS = ['get', 'post', 'put', 'delete', 'patch', 'head']

function mergeParameters(pathParameters = [], operationParameters = []) {
  const merged = new Map()

  ;[...pathParameters, ...operationParameters].forEach((parameter) => {
    if (!parameter?.name || !parameter?.in) return
    merged.set(`${parameter.in}:${parameter.name}`, parameter)
  })

  return [...merged.values()]
}

function resolveRef(spec, schema, seen = new Set()) {
  if (!schema) return null
  if (!schema.$ref) return schema
  if (seen.has(schema.$ref)) return null

  const schemaName = schema.$ref.split('/').pop()
  const resolved = spec.components?.schemas?.[schemaName]
  if (!resolved) return null

  return resolveRef(spec, resolved, new Set([...seen, schema.$ref]))
}

function mergeAllOf(spec, schema) {
  if (!schema?.allOf) return schema

  return schema.allOf.reduce((accumulator, current) => {
    const resolved = resolveSchema(spec, current)
    return {
      ...accumulator,
      ...resolved,
      properties: {
        ...(accumulator?.properties || {}),
        ...(resolved?.properties || {})
      },
      required: [...new Set([...(accumulator?.required || []), ...(resolved?.required || [])])]
    }
  }, { type: 'object', properties: {}, required: [] })
}

export function resolveSchema(spec, schema) {
  const resolved = resolveRef(spec, schema)
  if (!resolved) return null
  return mergeAllOf(spec, resolved)
}

export function flattenOpenApiSpec(spec) {
  const tagOrder = new Map((spec.tags || []).map((tag, index) => [tag.name, index]))
  const operations = []

  Object.entries(spec.paths || {}).forEach(([path, pathItem]) => {
    HTTP_METHODS.forEach((method) => {
      const operation = pathItem?.[method]
      if (!operation) return

      const parameters = mergeParameters(pathItem.parameters, operation.parameters)
      const tags = operation.tags?.length ? operation.tags : ['Ungrouped']
      operations.push({
        key: `${method.toUpperCase()} ${path}`,
        method: method.toUpperCase(),
        path,
        summary: operation.summary || `${method.toUpperCase()} ${path}`,
        description: operation.description || '',
        operationId: operation.operationId || `${method}_${path}`,
        tag: tags[0],
        tags,
        parameters,
        requestBody: operation.requestBody || null
      })
    })
  })

  return operations.sort((left, right) => {
    const tagSort = (tagOrder.get(left.tag) ?? 999) - (tagOrder.get(right.tag) ?? 999)
    if (tagSort !== 0) return tagSort
    if (left.path !== right.path) return left.path.localeCompare(right.path)
    return left.method.localeCompare(right.method)
  })
}

export function getRequestBodyTemplate(spec, operation) {
  const content = operation?.requestBody?.content || {}
  const jsonSchema = content['application/json']?.schema
  if (!jsonSchema) return ''

  const example = buildExampleFromSchema(spec, jsonSchema)
  if (example === undefined) return ''
  return JSON.stringify(example, null, 2)
}

export function getParameterDefault(spec, parameter) {
  if (!parameter) return ''
  if (parameter.example !== undefined) return formatDefaultValue(parameter.example)

  const schema = resolveSchema(spec, parameter.schema)
  if (!schema) return ''
  if (schema.example !== undefined) return formatDefaultValue(schema.example)
  if (schema.default !== undefined) return formatDefaultValue(schema.default)
  if (Array.isArray(schema.enum) && schema.enum.length > 0) return formatDefaultValue(schema.enum[0])
  return ''
}

function formatDefaultValue(value) {
  if (typeof value === 'object' && value !== null) {
    return JSON.stringify(value)
  }
  return value
}

function buildExampleFromSchema(spec, schema, depth = 0) {
  if (!schema || depth > 5) return undefined

  const resolved = resolveSchema(spec, schema)
  if (!resolved) return undefined

  if (resolved.example !== undefined) return resolved.example
  if (resolved.default !== undefined) return resolved.default
  if (Array.isArray(resolved.enum) && resolved.enum.length > 0) return resolved.enum[0]

  switch (resolved.type) {
    case 'object':
      return buildObjectExample(spec, resolved, depth)
    case 'array':
      return [buildExampleFromSchema(spec, resolved.items, depth + 1)].filter((item) => item !== undefined)
    case 'integer':
    case 'number':
      return 0
    case 'boolean':
      return false
    case 'string':
      return buildStringExample(resolved.format)
    default:
      return {}
  }
}

function buildObjectExample(spec, schema, depth) {
  const properties = schema.properties || {}
  const keys = Object.keys(properties)
  if (keys.length === 0) return {}

  return keys.reduce((result, key) => {
    const value = buildExampleFromSchema(spec, properties[key], depth + 1)
    result[key] = value === undefined ? '' : value
    return result
  }, {})
}

function buildStringExample(format) {
  switch (format) {
    case 'date-time':
      return new Date().toISOString()
    case 'date':
      return new Date().toISOString().slice(0, 10)
    case 'uuid':
      return '00000000-0000-0000-0000-000000000000'
    default:
      return ''
  }
}

export function buildRequestPath(operation, parameterValues = {}, extraQueryValues = {}) {
  const query = new URLSearchParams()
  const path = operation.path.replace(/\{([^}]+)\}/g, (_, name) => {
    const value = parameterValues[name]
    return encodeURIComponent(value ?? `{${name}}`)
  })

  operation.parameters
    .filter((parameter) => parameter.in === 'query')
    .forEach((parameter) => {
      const value = parameterValues[parameter.name]
      if (value === null || value === undefined || value === '') return
      query.set(parameter.name, String(value))
    })

  Object.entries(extraQueryValues).forEach(([key, value]) => {
    if (!key || value === null || value === undefined || value === '') return
    query.set(key, String(value))
  })

  const queryString = query.toString()
  return queryString ? `${path}?${queryString}` : path
}

export function buildCodeSnippets(baseUrl, method, relativePath, headers, body) {
  const normalizedBase = baseUrl.replace(/\/$/, '')
  const fullUrl = `${normalizedBase}${relativePath}`
  const headerEntries = Object.entries(headers || {})
  const bodyText = body?.trim() ? body : ''

  const curlHeaders = headerEntries
    .map(([key, value]) => `  -H "${escapeDoubleQuotes(key)}: ${escapeDoubleQuotes(value)}"`)
    .join(' \\\n')
  const curlBody = bodyText ? `${curlHeaders ? ' \\\n' : ''}  --data-raw '${escapeSingleQuotes(bodyText)}'` : ''
  const curl = [`curl -X ${method} "${fullUrl}"`, curlHeaders, curlBody].filter(Boolean).join(' \\\n')

  const jsHeaders = headerEntries.length
    ? `,\n    headers: ${JSON.stringify(headers, null, 4).replace(/^/gm, '    ')}`
    : ''
  const jsBody = bodyText ? `,\n    body: ${JSON.stringify(bodyText)}` : ''
  const javascript = `const response = await fetch(${JSON.stringify(fullUrl)}, {\n    method: ${JSON.stringify(method)}${jsHeaders}${jsBody}\n})\n\nconst text = await response.text()\nconsole.log(response.status, text)`

  const csharpHeaders = headerEntries
    .map(([key, value]) => `request.Headers.TryAddWithoutValidation(${JSON.stringify(key)}, ${JSON.stringify(value)});`)
    .join('\n')
  const csharpBody = bodyText
    ? `request.Content = new StringContent(${JSON.stringify(bodyText)}, Encoding.UTF8, "application/json");`
    : ''
  const csharp = `using System.Net.Http;\nusing System.Text;\n\nusing HttpClient client = new();\nusing HttpRequestMessage request = new(HttpMethod.${method[0]}${method.slice(1).toLowerCase()}, ${JSON.stringify(fullUrl)});\n${csharpHeaders}${csharpHeaders ? '\n' : ''}${csharpBody}\nusing HttpResponseMessage response = await client.SendAsync(request);\nstring text = await response.Content.ReadAsStringAsync();\nConsole.WriteLine($\"{(int)response.StatusCode} {text}\");`

  return { curl, javascript, csharp }
}

function escapeDoubleQuotes(value) {
  return String(value).replace(/"/g, '\\"')
}

function escapeSingleQuotes(value) {
  return String(value).replace(/'/g, "'\\''")
}

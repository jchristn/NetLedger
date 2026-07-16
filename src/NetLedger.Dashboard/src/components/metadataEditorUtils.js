export const isLabelsField = (key) => String(key || '').toLowerCase() === 'labels'
export const isTagsField = (key) => String(key || '').toLowerCase() === 'tags'

export const normalizeLabelRows = (value) => {
  if (Array.isArray(value)) {
    return value.length > 0 ? value.map(item => String(item ?? '')) : ['']
  }

  if (typeof value === 'string') {
    const labels = value.split(',').map(item => item.trim())
    return labels.length > 0 ? labels : ['']
  }

  return ['']
}

export const labelsToPayload = (value) => {
  return normalizeLabelRows(value)
    .map(item => item.trim())
    .filter(Boolean)
}

export const normalizeTagRows = (value) => {
  if (Array.isArray(value)) {
    const rows = value.map(item => ({
      key: String(item?.key ?? item?.Key ?? ''),
      value: String(item?.value ?? item?.Value ?? '')
    }))
    return rows.length > 0 ? rows : [{ key: '', value: '' }]
  }

  if (value && typeof value === 'object') {
    const rows = Object.entries(value).map(([key, tagValue]) => ({
      key,
      value: String(tagValue ?? '')
    }))
    return rows.length > 0 ? rows : [{ key: '', value: '' }]
  }

  if (typeof value === 'string') {
    const rows = value.split(',').map(pair => {
      const [key, ...rest] = pair.split('=')
      return {
        key: String(key ?? '').trim(),
        value: rest.join('=').trim()
      }
    }).filter(row => row.key || row.value)
    return rows.length > 0 ? rows : [{ key: '', value: '' }]
  }

  return [{ key: '', value: '' }]
}

export const tagsToPayload = (value) => {
  return normalizeTagRows(value).reduce((tags, row) => {
    const key = row.key.trim()
    if (key) {
      tags[key] = row.value.trim()
    }
    return tags
  }, {})
}

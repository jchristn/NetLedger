import React from 'react'
import CopyButton from './CopyButton'
import './ExternalServicesCard.css'

// Defaults match the host-published ports in docker/compose.yaml. Operators can override any URL or credential at
// container start (see docker-entrypoint.sh); setting a URL to an empty string hides that service.
const DEFAULT_SERVICES = [
  {
    key: 'grafana',
    name: 'Grafana',
    description: 'Dashboards for HTTP, ledger, database, archival pipeline, integrations, auth, and runtime (NetLedger folder).',
    url: 'http://localhost:3002',
    username: 'admin',
    password: 'admin'
  },
  {
    key: 'prometheus',
    name: 'Prometheus',
    description: 'Metrics store scraped from each NetLedger service. Query netledger_* and http_server_* series.',
    url: 'http://localhost:9090'
  },
  {
    key: 'tempo',
    name: 'Tempo',
    description: 'Trace store (API). Explore traces from Grafana > Explore > Tempo.',
    url: 'http://localhost:3200'
  },
  {
    key: 'loki',
    name: 'Loki',
    description: 'Log store (API) for background work such as automatic archival. Explore from Grafana > Explore > Loki.',
    url: 'http://localhost:3100'
  },
  {
    key: 'less3',
    name: 'Less3 UI',
    description: 'S3-compatible object storage console used by the Archive Server. Sign in with the admin API key; the Archive Server uses the S3 access and secret keys.',
    url: 'http://localhost:3001',
    username: 'default',
    password: 'default',
    apiKey: 'less3admin',
    usernameLabel: 'Access key',
    passwordLabel: 'Secret key',
    apiKeyLabel: 'Admin API key'
  }
]

function resolveServices() {
  const configured = window.NETLEDGER_CONFIG?.externalServices || {}
  return DEFAULT_SERVICES
    .map((service) => {
      const override = configured[service.key] || {}
      return {
        ...service,
        url: typeof override.url === 'string' ? override.url.trim() : service.url,
        username: typeof override.username === 'string' ? override.username : service.username,
        password: typeof override.password === 'string' ? override.password : service.password,
        apiKey: typeof override.apiKey === 'string' ? override.apiKey : service.apiKey
      }
    })
    .filter((service) => service.url)
}

function CredentialToken({ label, value }) {
  if (!value) return null
  return (
    <span className="external-service-credential">
      <span className="external-service-credential-label">{label}</span>
      <code className="external-service-token">{value}</code>
      <CopyButton text={value} title={`Copy ${label.toLowerCase()}`} size={12} />
    </span>
  )
}

export default function ExternalServicesCard() {
  const services = resolveServices()

  return (
    <div className="external-services card">
      <div className="card-header">
        <h3>External Services</h3>
        <span className="external-services-hint">Observability and storage tools bundled with this deployment</span>
      </div>
      <div className="card-body">
        {services.length === 0 ? (
          <p className="external-services-empty">No external services are configured for this deployment.</p>
        ) : (
          <ul className="external-services-list">
            {services.map((service) => (
              <li key={service.key} className="external-service">
                <div className="external-service-main">
                  <a className="external-service-name" href={service.url} target="_blank" rel="noopener noreferrer">
                    {service.name}
                  </a>
                  <span className="external-service-description">{service.description}</span>
                </div>
                <div className="external-service-details">
                  <span className="external-service-url">
                    <code className="external-service-token">{service.url}</code>
                    <CopyButton text={service.url} title="Copy URL" size={12} />
                  </span>
                  <CredentialToken label={service.usernameLabel || 'User'} value={service.username} />
                  <CredentialToken label={service.passwordLabel || 'Password'} value={service.password} />
                  <CredentialToken label={service.apiKeyLabel || 'API key'} value={service.apiKey} />
                </div>
              </li>
            ))}
          </ul>
        )}
        <p className="external-services-note">
          Credentials shown are local development defaults. Change them before exposing any of these services beyond this machine.
        </p>
      </div>
    </div>
  )
}

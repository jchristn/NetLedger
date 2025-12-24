# NetLedger Dashboard Dockerfile
# Multi-stage build for React/Vite application

# Stage 1: Build
FROM node:20-alpine AS build
WORKDIR /app

# Copy package files
COPY src/NetLedger.Dashboard/package*.json ./

# Install dependencies
RUN npm ci

# Copy source code
COPY src/NetLedger.Dashboard/ ./

# Build the application
RUN npm run build

# Stage 2: Runtime with nginx
FROM nginx:alpine AS runtime

# Install networking tools and utilities
# Note: Alpine uses apk instead of apt-get
RUN apk update && apk add --no-cache \
    busybox-extras \
    bind-tools \
    curl \
    wget \
    vim \
    net-tools \
    iproute2 \
    iputils

# Remove default nginx static content
RUN rm -rf /usr/share/nginx/html/*

# Copy built application from build stage
COPY --from=build /app/dist /usr/share/nginx/html

# Copy custom nginx configuration
COPY src/NetLedger.Dashboard/nginx.conf /etc/nginx/conf.d/default.conf

# Expose port 80
EXPOSE 80

# Start nginx
CMD ["nginx", "-g", "daemon off;"]

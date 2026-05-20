# syntax=docker/dockerfile:1.7

# Build context esperado: WebApp/ (la raiz de este proyecto)
# Uso: docker build -t canelary-webapp WebApp/
# o via docker compose desde la raiz del repo.

FROM node:22-alpine AS build
WORKDIR /app

# pnpm via corepack — coincide con pnpm-lock.yaml del repo
RUN corepack enable && corepack prepare pnpm@latest --activate

# Cache de dependencias: copiar manifests antes que el codigo
COPY package.json pnpm-lock.yaml ./
RUN pnpm install --frozen-lockfile

# Build de produccion (tsc -b && vite build)
COPY . .
RUN pnpm build


FROM nginx:alpine AS runtime

# Artefactos estaticos
COPY --from=build /app/dist /usr/share/nginx/html

# Conf que sirve la SPA + reverse proxy al Server
COPY nginx.conf /etc/nginx/conf.d/default.conf

# /healthz lo expone el propio nginx.conf
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD wget -qO- http://localhost/healthz >/dev/null 2>&1 || exit 1

EXPOSE 80

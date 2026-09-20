# syntax=docker/dockerfile:1
FROM node:20-alpine AS deps
WORKDIR /app
COPY package.json .npmrc ./
RUN npm install

# ---- dev ----
FROM node:20-alpine AS dev
WORKDIR /app
COPY --from=deps /app/node_modules ./node_modules
COPY . .
EXPOSE 5173
CMD ["npm", "run", "dev", "--", "--host", "0.0.0.0"]

# ---- build ----
FROM node:20-alpine AS build
WORKDIR /app
COPY --from=deps /app/node_modules ./node_modules
COPY . .
# VITE_* инлайнятся в бандл на этапе сборки. Для production-входа через Keycloak
# передайте build-args (см. README.md): VITE_AUTH_DEV_MODE=false и
# VITE_KEYCLOAK_URL/REALM/CLIENT_ID. По умолчанию — демо-режим.
ARG VITE_API_BASE_URL
ARG VITE_AUTH_DEV_MODE
ARG VITE_KEYCLOAK_URL
ARG VITE_KEYCLOAK_REALM
ARG VITE_KEYCLOAK_CLIENT_ID
ENV VITE_API_BASE_URL=${VITE_API_BASE_URL} \
    VITE_AUTH_DEV_MODE=${VITE_AUTH_DEV_MODE} \
    VITE_KEYCLOAK_URL=${VITE_KEYCLOAK_URL} \
    VITE_KEYCLOAK_REALM=${VITE_KEYCLOAK_REALM} \
    VITE_KEYCLOAK_CLIENT_ID=${VITE_KEYCLOAK_CLIENT_ID}
RUN npm run build

# ---- prod (nginx со статикой) ----
FROM nginx:1.27-alpine AS prod
COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80

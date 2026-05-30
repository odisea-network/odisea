# syntax=docker/dockerfile:1.6
# Multi-stage build for the Angular portal: build with Node, serve with nginx.

FROM node:22-alpine AS build
WORKDIR /app
COPY package.json package-lock.json* ./
RUN npm install --no-audit --no-fund
COPY . ./
RUN npx ng build --configuration=production

FROM nginx:alpine AS runtime
COPY --from=build /app/dist/portal/browser /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80

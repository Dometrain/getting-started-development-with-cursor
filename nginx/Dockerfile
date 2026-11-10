FROM node:24-alpine AS web
WORKDIR /src
COPY web/package.json web/package-lock.json ./
RUN npm ci
COPY web/ ./
RUN npm run build

FROM nginx:1.28-alpine
COPY nginx/nginx.conf /etc/nginx/nginx.conf
COPY nginx/livepoll-proxy.conf /etc/nginx/livepoll-proxy.conf
COPY --from=web /src/dist /usr/share/nginx/html
EXPOSE 80

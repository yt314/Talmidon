# בונה את ה-Angular ל-static assets, ומגיש אותם עם Caddy — שגם מטפל אוטומטית ב-HTTPS
# (Let's Encrypt) וגם מנתב /api/* ל-container של ה-API (ראו Caddyfile ו-docker-compose.prod.yml).

FROM node:22-alpine AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
RUN npm run build

FROM caddy:2-alpine
COPY --from=build /app/dist/talmidon/browser /srv
COPY Caddyfile /etc/caddy/Caddyfile

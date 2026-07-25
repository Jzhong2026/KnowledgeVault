FROM node:22.22-alpine AS build
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . ./
RUN npm run build -- --configuration docker --output-mode=static

FROM node:22.22-alpine
WORKDIR /app
COPY --from=build /app/dist/knowledge-vault-web/browser ./browser
COPY --from=build /app/server-prod.js ./
ENV PORT=4000
ENV API_HOST=backend
ENV API_PORT=5030
EXPOSE 4000
CMD ["node", "server-prod.js"]

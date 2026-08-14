FROM node:20-alpine AS build
WORKDIR /app
COPY src/Web/package*.json ./
RUN npm install
COPY src/Web/ .
RUN rm -f .env.production && npm run build

FROM nginx:alpine AS final
COPY --from=build /app/dist /usr/share/nginx/html
COPY docker/web/nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]

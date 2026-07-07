# Build the Angular frontend image.
# Run from repo root: docker build -f DASHBOARD.VIEW/Dockerfile -t dashboard-web .

FROM node:22-alpine AS build
WORKDIR /src
COPY DASHBOARD.VIEW/package*.json ./
RUN npm ci
COPY DASHBOARD.VIEW/ ./
RUN npm run build -- --configuration docker

FROM nginx:alpine AS runtime
COPY --from=build /src/dist/task-dashboard/browser /usr/share/nginx/html
COPY DASHBOARD.VIEW/nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80

---
description: Run the Azure deployment workflow (pre-deployment checklist, pipeline/Dockerfile generation, rollback procedure) for AKS/App Service.
argument-hint: [environment: dev|staging|prod]
---

# Workflow: Deploy to Azure

Deployment workflow for .NET + Angular apps on Azure (AKS / App Service).

## Usage
```
/deploy-to-azure [environment: dev|staging|prod]
```

---

## Pre-Deployment Checklist

### Code Quality
- [ ] All tests passing (`dotnet test` + `ng test`)
- [ ] No compiler warnings (`dotnet build -warnaserror`)
- [ ] TypeScript strict mode passing (`ng build --configuration production`)
- [ ] Code review approved
- [ ] Security review done for new endpoints

### Database
- [ ] Migration tested on copy of production DB
- [ ] Migration script generated and reviewed
- [ ] Rollback script ready (`dotnet ef migrations script Previous Current --output rollback.sql`)
- [ ] Zero-downtime migration confirmed (nullable columns, online indexes)

### Configuration
- [ ] New app settings added to Azure Key Vault
- [ ] Connection strings verified in target environment
- [ ] Feature flags configured
- [ ] Application Insights connection string updated

---

## Deployment Prompt Templates

### Generate Deployment Pipeline
```
Generate Azure DevOps pipeline for:

**App Type**: [.NET API | Angular SPA | Both]
**Target**: [AKS | Azure App Service | Static Web Apps]
**Environments**: [dev → staging → prod]

**Requirements**:
- Build + test on every PR
- Deploy to dev automatically on merge to develop
- Deploy to staging manually (with approval)
- Deploy to prod manually (with two approvals)
- Run EF Core migrations as part of deployment
- Zero-downtime deployment (blue-green or rolling)

Provide:
1. azure-pipelines.yml with stages
2. Dockerfile for .NET API
3. Dockerfile for Angular (nginx-based)
4. Kubernetes deployment YAML (if AKS)
5. Health check endpoint setup
```

### Review Deployment Config
```
Review this deployment configuration for issues:

**Pipeline file**:
[Paste azure-pipelines.yml or github-actions workflow]

**Dockerfile** (if applicable):
[Paste]

**K8s manifests** (if applicable):
[Paste]

Check for:
1. Secrets hardcoded (should use Key Vault / pipeline secrets)
2. Missing health checks on container
3. Missing resource limits on K8s pods
4. Missing liveness/readiness probes
5. Migration running before app deployment? (correct order)
6. Missing rollback strategy
7. Multi-stage Docker build? (reduces image size)
8. .NET app runs as non-root user?
9. Angular build uses production configuration?
```

---

## Docker Templates

### .NET API Dockerfile
```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["src/WebApi/WebApi.csproj", "src/WebApi/"]
COPY ["src/Application/Application.csproj", "src/Application/"]
COPY ["src/Domain/Domain.csproj", "src/Domain/"]
COPY ["src/Infrastructure/Infrastructure.csproj", "src/Infrastructure/"]
RUN dotnet restore "src/WebApi/WebApi.csproj"
COPY . .
RUN dotnet publish "src/WebApi/WebApi.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
# Non-root user
RUN adduser --disabled-password --gecos "" appuser && chown -R appuser /app
USER appuser
COPY --from=build /app/publish .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "WebApi.dll"]
```

### Angular Dockerfile (Nginx)
```dockerfile
FROM node:22-alpine AS build
WORKDIR /app
COPY package*.json .
RUN npm ci
COPY . .
RUN npm run build -- --configuration production

FROM nginx:alpine
COPY --from=build /app/dist/[app-name]/browser /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
```

---

## Rollback Procedure
```
If deployment fails:

1. Revert K8s deployment:
   kubectl rollout undo deployment/[app-name] -n [namespace]

2. Check rollout status:
   kubectl rollout status deployment/[app-name] -n [namespace]

3. If DB migration was applied and needs rollback:
   dotnet ef database update [PreviousMigrationName]
   -- OR use the rollback SQL script

4. Verify health:
   curl https://[app-url]/health

5. Check Application Insights for errors
```

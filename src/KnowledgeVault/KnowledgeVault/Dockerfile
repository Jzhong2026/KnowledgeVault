FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution-level files
COPY KnowledgeVault.slnx ./
COPY KnowledgeVault/KnowledgeVault.csproj KnowledgeVault/
COPY KnowledgeVault.Contracts/KnowledgeVault.Contracts.csproj KnowledgeVault.Contracts/
COPY KnowledgeVault.DataAccess/KnowledgeVault.DataAccess.csproj KnowledgeVault.DataAccess/
COPY KnowledgeVault.Domain/KnowledgeVault.Domain.csproj KnowledgeVault.Domain/
COPY KnowledgeVault.Infrastructure/KnowledgeVault.Infrastructure.csproj KnowledgeVault.Infrastructure/
COPY KnowledgeVault.Providers/KnowledgeVault.Providers.csproj KnowledgeVault.Providers/

# Restore dependencies
RUN dotnet restore KnowledgeVault/KnowledgeVault.csproj

# Copy remaining source and publish
COPY . ./
WORKDIR /src/KnowledgeVault
RUN dotnet publish -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app ./

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:5030
ENV ConnectionStrings__KnowledgeVaultDb="Data Source=/app/data/knowledge-vault.db"
ENV Jwt__SigningKey=ChangeMeInProduction-UseAStrongRandomKey-32Chars!

RUN mkdir -p /app/data /app/logs
VOLUME ["/app/data", "/app/logs"]

EXPOSE 5030
ENTRYPOINT ["dotnet", "KnowledgeVault.dll"]

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props global.json ./
COPY src/LivePoll.Domain/LivePoll.Domain.csproj src/LivePoll.Domain/
COPY src/LivePoll.Application/LivePoll.Application.csproj src/LivePoll.Application/
COPY src/LivePoll.Infrastructure/LivePoll.Infrastructure.csproj src/LivePoll.Infrastructure/
COPY src/LivePoll.Api/LivePoll.Api.csproj src/LivePoll.Api/

RUN dotnet restore src/LivePoll.Api/LivePoll.Api.csproj

COPY src/LivePoll.Domain/ src/LivePoll.Domain/
COPY src/LivePoll.Application/ src/LivePoll.Application/
COPY src/LivePoll.Infrastructure/ src/LivePoll.Infrastructure/
COPY src/LivePoll.Api/ src/LivePoll.Api/

RUN dotnet publish src/LivePoll.Api/LivePoll.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "LivePoll.Api.dll"]

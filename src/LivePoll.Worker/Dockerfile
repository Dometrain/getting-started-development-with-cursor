FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props global.json ./
COPY src/LivePoll.Domain/LivePoll.Domain.csproj src/LivePoll.Domain/
COPY src/LivePoll.Application/LivePoll.Application.csproj src/LivePoll.Application/
COPY src/LivePoll.Infrastructure/LivePoll.Infrastructure.csproj src/LivePoll.Infrastructure/
COPY src/LivePoll.Worker/LivePoll.Worker.csproj src/LivePoll.Worker/

RUN dotnet restore src/LivePoll.Worker/LivePoll.Worker.csproj

COPY src/LivePoll.Domain/ src/LivePoll.Domain/
COPY src/LivePoll.Application/ src/LivePoll.Application/
COPY src/LivePoll.Infrastructure/ src/LivePoll.Infrastructure/
COPY src/LivePoll.Worker/ src/LivePoll.Worker/

RUN dotnet publish src/LivePoll.Worker/LivePoll.Worker.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "LivePoll.Worker.dll"]

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props global.json ./
COPY src/LivePoll.Simulator/LivePoll.Simulator.csproj src/LivePoll.Simulator/

RUN dotnet restore src/LivePoll.Simulator/LivePoll.Simulator.csproj

COPY src/LivePoll.Simulator/ src/LivePoll.Simulator/

RUN dotnet publish src/LivePoll.Simulator/LivePoll.Simulator.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "LivePoll.Simulator.dll"]

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props global.json ./
COPY src/LivePoll.Bff/LivePoll.Bff.csproj src/LivePoll.Bff/

RUN dotnet restore src/LivePoll.Bff/LivePoll.Bff.csproj

COPY src/LivePoll.Bff/ src/LivePoll.Bff/

RUN dotnet publish src/LivePoll.Bff/LivePoll.Bff.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "LivePoll.Bff.dll"]

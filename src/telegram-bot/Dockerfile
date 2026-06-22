FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Slowrig.TelegramBot.csproj ./
RUN dotnet restore

COPY Program.cs ./
RUN dotnet publish -c Release -o /out --no-restore

FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build /out ./
ENTRYPOINT ["dotnet", "Slowrig.TelegramBot.dll"]

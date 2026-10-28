# Dev: SDK + hot reload (dotnet watch). Source is bind-mounted by compose.
FROM mcr.microsoft.com/dotnet/sdk:9.0

WORKDIR /app

# Restore first to cache dependencies
COPY *.csproj ./
RUN dotnet restore

COPY . ./

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_USE_POLLING_FILE_WATCHER=1

CMD ["dotnet", "watch", "run", "--urls", "http://+:8080"]

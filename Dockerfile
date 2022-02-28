#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:5.0 AS base
WORKDIR /app
ENV RABBITMQ_HOST localhost
ENV RABBITMQ_PORT 31672 
ENV ASPNETCORE_URLS=http://*:80
ENV ASPNETCORE_ENVIRONMENT=Development

FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build
WORKDIR /src
COPY ["CartService.csproj", "."]
RUN dotnet restore "./CartService.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "CartService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "CartService.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "CartService.dll"]
FROM microsoft/dotnet:2.1-aspnetcore-runtime-stretch-slim-arm32v7 AS base
WORKDIR /app

FROM microsoft/dotnet:2.1-sdk AS build
WORKDIR /src
COPY . .
RUN dotnet restore -nowarn:msb3202,nu1503
RUN dotnet build -c Debug -o /app

FROM build AS publish
RUN dotnet publish -c Debug -r linux-arm -o /app

FROM base AS final
WORKDIR /app
COPY --from=publish /app .
EXPOSE 80
CMD ["./Rboard.Server"]

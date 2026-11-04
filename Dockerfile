FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY CasamentoTatianaDiogo.csproj ./
COPY NuGet.Config ./
RUN dotnet restore CasamentoTatianaDiogo.csproj
COPY . ./
RUN dotnet publish CasamentoTatianaDiogo.csproj -c Release --no-restore -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080 \
    PORT=8080 \
    Database__Provider=PostgreSql \
    TZ=Europe/Lisbon
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "CasamentoTatianaDiogo.dll"]

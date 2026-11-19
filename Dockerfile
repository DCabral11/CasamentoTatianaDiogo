FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG RUNTIME_IDENTIFIER=linux-x64
ARG PUBLISH_READY_TO_RUN=true
WORKDIR /src
COPY CasamentoTatianaDiogo.csproj ./
COPY NuGet.Config ./
RUN dotnet restore CasamentoTatianaDiogo.csproj -r $RUNTIME_IDENTIFIER -p:PublishReadyToRun=$PUBLISH_READY_TO_RUN
COPY . ./
RUN dotnet publish CasamentoTatianaDiogo.csproj -c Release --no-restore -r $RUNTIME_IDENTIFIER --self-contained false -o /app/publish -p:UseAppHost=false -p:PublishReadyToRun=$PUBLISH_READY_TO_RUN

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

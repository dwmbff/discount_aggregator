FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY AggStudentDiscounts.sln ./
COPY src/AggStudentDiscounts.Api/AggStudentDiscounts.Api.csproj src/AggStudentDiscounts.Api/
RUN dotnet restore src/AggStudentDiscounts.Api/AggStudentDiscounts.Api.csproj
COPY src/ src/
RUN dotnet publish src/AggStudentDiscounts.Api/AggStudentDiscounts.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
RUN mkdir -p /data/uploads && chown $APP_UID /data/uploads
ENV Storage__Path=/data/uploads
VOLUME /data/uploads
USER $APP_UID
ENTRYPOINT ["dotnet", "AggStudentDiscounts.Api.dll"]

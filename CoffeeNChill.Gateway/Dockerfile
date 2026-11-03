# ---- build stage ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY *.csproj ./
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app/publish

# ---- runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
# .NET images listen on 8080 by default; map it with  -p 5000:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "CoffeeNChill.Gateway.dll"]

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["Directory.Build.props", "."]
COPY ["Directory.Packages.props", "."]
COPY ["01-aspire/02-ServiceDefaults/RentifyxIdentity.ServiceDefaults/RentifyxIdentity.ServiceDefaults.csproj", "01-aspire/02-ServiceDefaults/RentifyxIdentity.ServiceDefaults/"]
COPY ["02-src/03-Domain/RentifyxIdentity.Domain/RentifyxIdentity.Domain.csproj", "02-src/03-Domain/RentifyxIdentity.Domain/"]
COPY ["02-src/02-Application/RentifyxIdentity.Application/RentifyxIdentity.Application.csproj", "02-src/02-Application/RentifyxIdentity.Application/"]
COPY ["02-src/05-Infrastructure/RentifyxIdentity.Infrastructure/RentifyxIdentity.Infrastructure.csproj", "02-src/05-Infrastructure/RentifyxIdentity.Infrastructure/"]
COPY ["02-src/04-IoC/RentifyxIdentity.IoC/RentifyxIdentity.IoC.csproj", "02-src/04-IoC/RentifyxIdentity.IoC/"]
COPY ["02-src/01-Api/RentifyxIdentity.Api/RentifyxIdentity.Api.csproj", "02-src/01-Api/RentifyxIdentity.Api/"]

RUN dotnet restore "02-src/01-Api/RentifyxIdentity.Api/RentifyxIdentity.Api.csproj"

COPY . .

RUN dotnet publish "02-src/01-Api/RentifyxIdentity.Api/RentifyxIdentity.Api.csproj" \
    --no-restore \
    --configuration Release \
    --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "RentifyxIdentity.Api.dll"]

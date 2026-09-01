FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore src/SkillScanner.csproj
RUN dotnet publish src/SkillScanner.csproj -c Release -o /app --no-restore

 FROM mcr.microsoft.com/dotnet/runtime:9.0
 WORKDIR /app
 COPY --from=build /app .
 ENTRYPOINT [ "dotnet", "SkillScanner.dll" ]
 
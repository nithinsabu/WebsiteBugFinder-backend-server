# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Copy the solution and project files
COPY WBF.sln ./
COPY WBF/WBF.csproj WBF/
RUN dotnet restore WBF/WBF.csproj

# Copy the rest of the project and build it
COPY WBF/ WBF/
RUN dotnet publish WBF/WBF.csproj -c Release -o out

# Stage 2: Run
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/out ./

# Expose backend port
EXPOSE 8080

# Run the application
CMD ["dotnet", "WBF.dll"]

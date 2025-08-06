# GdprApi/Dockerfile
# This Dockerfile is located in the GdprApi directory and is used to build the API image.

# Use a multi-stage build to create a small, efficient final image.

# Stage 1: Build the application
# Use the official .NET 8 SDK image to build the project
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
# Set the working directory inside the container
WORKDIR /src
# Copy the solution file and restore dependencies. This is optimized for caching.
COPY GdprApi.sln .
# Copy all project files and restore dependencies.
# The paths are relative to the WORKDIR and must match your project structure.
COPY GdprApi/*.csproj ./GdprApi/
COPY Services/*.csproj ./Services/
COPY GdprConfigurations/*.csproj ./GdprConfigurations/
RUN dotnet restore "GdprApi/GdprApi.csproj"

# Copy the rest of the source code
COPY . .
# Navigate to the API project directory
WORKDIR /src/GdprApi
# Publish the application in Release mode
RUN dotnet publish "GdprApi.csproj" -c Release -o /app/publish --no-restore

# Stage 2: Create the final production image
# Use the official ASP.NET runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
# Set the working directory
WORKDIR /app
# Copy the published application from the build stage
COPY --from=build /app/publish .
# Expose port 80 for the web server
EXPOSE 80
# Define the entry point for the container
ENTRYPOINT ["dotnet", "GdprApi.dll"]
# Use the official Microsoft .NET 6.0 SDK image as the build environment
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build-env

# Set the working directory in the container to /app
WORKDIR /app

# Copy csproj and restore dependencies
COPY *.csproj ./
RUN dotnet restore

# Copy the rest of the files and build the application
COPY . ./
RUN dotnet publish -c Release -o out

# Generate runtime image using Microsoft .NET 6.0 runtime image
FROM mcr.microsoft.com/dotnet/aspnet:6.0

# Set the working directory in the container to /app
WORKDIR /app

# Copy build output from build-env
COPY --from=build-env /app/out .

# Expose port 80 for the application
EXPOSE 80

# Define the entry point for the Docker container.
# This is the command that will run when the container starts
ENTRYPOINT ["dotnet", "MealGeniusBackend.dll"]

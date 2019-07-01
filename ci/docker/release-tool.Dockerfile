FROM microsoft/dotnet:2.2-sdk as build

# Copy everything and build
WORKDIR /app
COPY ./tools ./
RUN dotnet publish -c Release -o out

# Build runtime image
FROM mcr.microsoft.com/dotnet/core/runtime:2.2
WORKDIR /app
COPY --from=build /app/*/out ./
ENTRYPOINT ["dotnet", "ReleaseTool.dll"]
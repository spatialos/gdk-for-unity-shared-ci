FROM microsoft/dotnet:2.2-sdk as build

# Copy everything and build
WORKDIR /app
COPY ./tools ./
RUN dotnet publish -c Release -o out

# Build runtime image
FROM mcr.microsoft.com/dotnet/core/runtime:2.2
WORKDIR /app
COPY --from=build /app/*/out ./

# Setup GIT
RUN apt-get update && \
    apt-get upgrade -y && \
    apt-get install -y git
ARG SSH_KEY
ENV SSH_KEY=$SSH_KEY
# Make ssh dir
RUN mkdir /root/.ssh/

# Create id_rsa from string arg, and set permissions
RUN echo "$SSH_KEY" > /root/.ssh/id_rsa
RUN chmod 600 /root/.ssh/id_rsa

# Create known_hosts
RUN touch /root/.ssh/known_hosts

# Add git providers to known_hosts
RUN ssh-keyscan github.com >> /root/.ssh/known_hosts

COPY ./github_key ./

ENTRYPOINT ["dotnet", "ReleaseTool.dll"]
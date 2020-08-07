FROM mcr.microsoft.com/dotnet/core/sdk:3.1 as build

# Copy everything and build
WORKDIR /app
COPY ./tools ./
RUN dotnet publish -c Release -o out

# Build runtime image
# Requires the bionic image due to runtime requirements of the libgit2sharp library
FROM mcr.microsoft.com/dotnet/core/runtime:3.1-bionic
WORKDIR /app
COPY --from=build /app/out ./

# Setup GIT
RUN apt-get update && \
    apt-get upgrade -y && \
    apt-get install -y git && \
    git config --global user.name "GDK for Unity Bot" && \
    git config --global user.email "gdk-for-unity-bot@improbable.io" && \
    git config --global core.sshCommand "ssh -i /var/ssh/id_rsa" && \
    mkdir -p /root/.ssh && \
    touch /root/.ssh/known_hosts && \
    ssh-keyscan github.com >> /root/.ssh/known_hosts

# Create a volume to mount our SSH key into and configure git to use it.
VOLUME /var/ssh
# Volume to mount our Github token into.
VOLUME /var/github
# Volume to output logs & Buildkite metadata to
VOLUME /var/logs

ENTRYPOINT ["dotnet", "ReleaseTool.dll"]

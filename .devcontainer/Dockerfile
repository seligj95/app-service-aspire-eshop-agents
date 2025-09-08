# Use Ubuntu 24.04 as base
FROM mcr.microsoft.com/devcontainers/base:ubuntu-24.04

# Install Python 3.12 and pip
RUN apt-get update && apt-get install -y \
    python3.12 \
    python3.12-venv \
    python3-pip \
    && rm -rf /var/lib/apt/lists/*

# Create symlinks for python and pip
RUN ln -s /usr/bin/python3.12 /usr/local/bin/python \
    && ln -s /usr/bin/python3.12 /usr/local/bin/python3

# Install .NET 9 SDK
RUN wget https://dotnet.microsoft.com/download/dotnet/scripts/v1/dotnet-install.sh -O dotnet-install.sh \
    && bash dotnet-install.sh --version 9.0.100 \
    && rm dotnet-install.sh \
    && ln -s /root/.dotnet/dotnet /usr/local/bin/dotnet

# Install Azure CLI
RUN curl -sL https://aka.ms/InstallAzureCLIDeb | bash

# Install Azure Developer CLI
RUN curl -fsSL https://aka.ms/install-azd.sh | bash

# Install Bicep CLI
RUN az bicep install

# Add dotnet to PATH
ENV PATH="/root/.dotnet:$PATH"

# Verify installations
RUN dotnet --info && azd --version && az --version && python --version && pip --version && az bicep version

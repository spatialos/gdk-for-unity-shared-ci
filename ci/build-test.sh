#!/usr/bin/env bash

set -e -u -o pipefail

if [[ -n "${DEBUG-}" ]]; then
  set -x
fi

cd "$(dirname "$0")/../"

source scripts/pinned-tools.sh

echo "--- Build Tools.sln :construction:"
dotnet build tools/Tools.sln

echo "--- Test DocsLinter.csproj :link:"
dotnet test --logger:"nunit;LogFilePath=$(pwd)/logs/docslinter-test-results.xml" "tools/DocsLinter/DocsLinter.csproj"

echo "--- Test ReleaseTool.csproj :fork:"
dotnet test --logger:"nunit;LogFilePath=$(pwd)/logs/releasetool-test-results.xml" "tools/ReleaseTool.Tests/ReleaseTool.Tests.csproj"

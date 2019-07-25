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
TOOLS_TEST_RESULTS_FILES="$(pwd)/logs/tools-test-results.xml"
dotnet test --logger:"nunit;LogFilePath=${TOOLS_TEST_RESULTS_FILES}" "tools/DocsLinter/DocsLinter.csproj"

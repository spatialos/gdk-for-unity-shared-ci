#!/usr/bin/env bash

set -e -u -o -x pipefail

cd "$(dirname "$0")/../"

source scripts/pinned-tools.sh

echo "Build Tools"
dotnet build tools/Tools.sln

echo "Test Tools"
TOOLS_TEST_RESULTS_FILES="$(pwd)/logs/tools-test-results.xml"
dotnet test --logger:"nunit;LogFilePath=${TOOLS_TEST_RESULTS_FILES}" "tools/DocsLinter/DocsLinter.csproj"

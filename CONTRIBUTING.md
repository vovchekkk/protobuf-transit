# Contributing to ProtobufTransit

Contributions and improvements are welcome. Please adhere to the guidelines below when proposing changes.

## Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Docker](https://www.docker.com/) (required only for running integration tests against live PostgreSQL and RabbitMQ instances)

## Build and Test Commands

### 1. Restore & Build

```shell
dotnet restore ProtobufTransit.sln
dotnet build ProtobufTransit.sln -c Release --no-restore
```

### 2. Unit Tests

```shell
dotnet test ProtobufTransit.sln -c Release --no-build
```

### 3. Integration Tests (Optional / Local Verification)

An isolated `docker-compose.yml` provides PostgreSQL (port `55439`) and RabbitMQ (port `56789`):

```shell
docker compose up -d --wait
$env:PROTOBUFTRANSIT_INTEGRATION = "1"
dotnet test ProtobufTransit.sln -c Release --no-build
docker compose down -v
```

### 4. NuGet Packaging

```shell
dotnet pack src/ProtobufTransit/ProtobufTransit.csproj -c Release --no-build -o artifacts
```

## Architectural Guidelines & Dependencies

- **Target Framework**: .NET 8 (`net8.0`).
- **Dependencies**: MassTransit is constrained to `[8.3.6, 9.0.0)`. Google.Protobuf is pinned to `3.26.1`.
- **Contract Decoupling**: ProtobufTransit is a contract-agnostic serialization layer. Core library code must never take dependencies on application messages, concrete queue names, or business logic.
- **Fail-Closed Principle**: Missing or unregistered message type headers must fail closed by raising `SerializationException`.

## Submitting Pull Requests

1. Open an issue first to discuss non-trivial architectural changes.
2. Keep pull requests focused on a single change or fix.
3. Ensure all unit and integration tests pass prior to submitting.

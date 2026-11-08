# Wallet service

The core financial engine for managing balances and transactions.

[![.NET Version](https://img.shields.io/badge/.NET-10.0-512BD4?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com/)
[![gRPC](https://img.shields.io/badge/gRPC-API-244c5a?style=flat-square&logo=grpc)](https://grpc.io/)
[![PostgreSQL](https://img.shields.io/badge/Database-PostgreSQL-4169E1?style=flat-square&logo=postgresql)](https://www.postgresql.org/)

## Overview

The wallet service operates as the financial backend for the platform. It models and manages user wallets, transaction histories, and balances. It orchestrates complex financial operations purely through locked-down PostgreSQL stored procedures to guarantee ACID compliance and absolute consistency.

## Architecture

This section explains the technologies and physical layout of the service.

- **Framework**: Built on .NET 10 Web SDK with `Grpc.AspNetCore` for the RPC server
- **Database driver**: Direct PostgreSQL interactions via `Npgsql` and `Dapper`
- **Validation**: Incoming RPC requests are validated using `ProtoValidate`
- **Logging**: Idiomatic structured logging via ASP.NET Core ILogger

### Project structure

- `Services/`: gRPC service implementations
- `Db/`: Database connection and pooling configurations
- `Models/`: Internal data models
- `Validation/`: Category request validation (plugged into `Contracts.Grpcx`, which provides server setup, interceptors and health checks)
- `Sql/`: Schema definitions, stored procedures, and role grants
- `Program.cs`: Application entrypoint and dependency wiring
- `wallet.csproj`: Dependency management

## Features

This section outlines the capabilities of the wallet service.

- **High-performance API**: Concurrent gRPC server built with modern .NET.
- **Automated validation**: Real-time payload validation via a gRPC interceptor to reject invalid requests early.
- **Relational integrity**: Uses PostgreSQL stored procedures to safely handle complex financial writes and consistency checks.
- **Graceful shutdown**: Safely drains active connections and closes database pools on OS signals.

## Database security

- Operates strictly under its own isolated schema
- Connects using the `wallet_service` database role, which does **not** have direct `INSERT`, `UPDATE`, or `DELETE` permissions on tables
- All complex writes and balance updates are handled safely via strictly defined database functions

## Getting started

This section explains how to run the wallet service locally.

### Prerequisites

- .NET 10 SDK
- PostgreSQL 18+ database instance
- Protocol buffer compilation tools if modifying definitions

### Configuration

Export these variables directly in your shell environment or `.env` file:

| Variable | Description | Required |
| :--- | :--- | :---: |
| `POSTGRES_URL` | Connection string for the PostgreSQL database | **Yes** |
| `PORT` | The port on which the gRPC server will listen (Default: `7297`) | No |
| `INTERNAL_BIND_IP` | Private IP to listen on (the address the gateway dials). No default | **Yes** |
| `INTERNAL_AUTH_TOKEN` | Shared secret (32+ chars) the gateway must send as `authorization: Bearer <token>`. Same value on every service and the gateway | **Yes** |
| `INTERNAL_TLS_CERT` / `INTERNAL_TLS_KEY` | PEM certificate and key; enables TLS on the gRPC listener when both are set | No |
| `RPC_TIMEOUT_MS` | Upper bound per RPC (default `5000`). A shorter deadline sent by the gateway wins | No |

### Running locally

Run the service using the .NET CLI:

```bash
dotnet run
```

To watch for file changes and automatically rebuild and restart the server:

```bash
dotnet watch run
```

## Deployment

The service is containerized and leverages GitHub Actions (`.github/workflows/main.yml`) for automated builds and deployment to a remote VM using Podman.
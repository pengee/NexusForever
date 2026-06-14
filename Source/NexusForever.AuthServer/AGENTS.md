# Auth Servers

Covers authentication and ticketing servers that handle client login flows.

## Purpose

Define rules for `NexusForever.AuthServer` (authentication, port 23115) and `NexusForever.StsServer` (STS ticket service). Both are minimal hosted services with similar structure: config → DI setup → HostedService → Network handlers.

## Ownership

- AuthServer owns the login handshake and session token issuance pipeline
- StsServer owns STS ticket validation and delegation

## Local Contracts

- Config files follow `*Server.example.json` + `*ServerConfiguration.cs` pattern
- NLog configured via `nlog.config` in each project root
- Network handlers under `Network/` directory, implement protocol-specific packet parsing
- Both depend on shared DI/logging from `NexusForever.Shared`

## Work Guidance

- Authentication flows must not leak timing information (constant-time comparisons for tokens)
- STS tickets are signed; never trust unsigned ticket payloads
- Keep server entry points minimal — delegate all logic to hosted services and message handlers
- Config changes that affect both servers should be documented in `Source/AGENTS.md` under User Preferences

## Verification

- `dotnet build Source/NexusForever.slnx` — compiles auth stack with all dependencies
- No unit tests exist; manual login flow testing against running server is the primary verification

## Child DOX Index

None. AuthServer and StsServer are sibling projects at the same complexity level.

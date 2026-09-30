# CivicConnect Project Documentation

This directory contains the controlled engineering documentation and
supporting artefacts for the CivicConnect SEN381 project.

## Current Milestone

Milestone 2 — Architecture, Technology & Initial Design Baseline

## Project Engineering Document

Controlled PED versions are stored in:

`PED/`

Current baseline:

`PED/CivicConnect_PED_v2.0.pdf`

The previous PED v1.0 baseline is retained to preserve project history.

## Architecture

The `architecture/` directory contains:

- Logical CivicConnect architecture
- Deployment direction
- Architecture Decision Records (ADRs)

## Data

The `data/` directory contains:

- Initial CivicConnect Entity Relationship Diagram
- Persistence design evidence

## Design

The `design/` directory contains:

- Request validation design
- Request status event design

## User Interface

The `ui/` directory contains:

- Requester wireframes
- Staff and management wireframes

## Current Architecture

CivicConnect uses a modular monolith with layered internal architecture.

The main responsibilities are separated into:

- Web
- Application
- Domain
- Infrastructure

## Current Technology Stack

- .NET 10
- C# 14
- ASP.NET Core MVC
- Razor Views
- Bootstrap 5
- PostgreSQL 18
- Entity Framework Core 10
- Npgsql
- ASP.NET Core Identity
- xUnit
- GitHub Actions

## Documentation Control

Milestone 2 documentation is maintained progressively through GitHub
branches, commits, Pull Requests and peer review.

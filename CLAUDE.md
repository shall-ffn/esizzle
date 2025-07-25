# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This project (esizzle) is a **modernization effort** to rewrite the legacy Hydra Due Diligence App from a Windows desktop application to a modern web application. The original DD-App is a comprehensive Windows desktop application built on .NET Framework (WinForms) for FFN analysts and administrators that digitizes and streamlines the entire lifecycle of loan-related paperwork.

**Important**: The legacy application logic resides in `/Users/shall/dev/Git/ffncorp/array_us/Fedor/Hydra.DueDiligence.App`. Reference this repository to understand existing functionality, business logic, and workflows that need to be migrated to the new web-based architecture.

## Legacy Architecture (Reference)

The original WinForms application uses a three-tier architecture with dependency injection (StructureMap):

```
WinForms UI Layer → Application/Service Layer → Data Access Layer → External Systems
```

This architecture should be referenced when designing the new web application to ensure all functionality is preserved while modernizing the technology stack.

### Key Components

- **UI Layer**: MDI shell (`MenuForm`) with three main areas:
  - `ImagingMain` - Document manipulation and loan workflow
  - `ITMain` - System administration and plugin management  
  - `DataMain` - Data export utilities

- **Service Layer**: 
  - Form Repositories (`IFormRepos`)
  - Core services: `ImagingSvc`, `ExportSvc`, `WatchmanSvc`
  - AutoMapper for DTO↔Domain mapping

- **Data Layer**:
  - `global.DAL.Aurora` - Primary data access
  - `DynamoLoanmasterDbCache` - AWS DynamoDB caching
  - `ImageCache` - Local image caching
  - Hydra Security Repository

## Core Workflow: eStacker Module

The central user workflow follows a cascading selection pattern:

1. **Select Offering** → Service call to Aurora database
2. **Select Sale** → Filtered by chosen offering
3. **Select Loan** → Filtered by chosen sale  
4. **Load Images** → Query Aurora for metadata, fetch files from S3, display in viewer

## Key External Dependencies

- **Aurora**: MySQL/PostgreSQL compatible Amazon RDS cluster (loan & imaging metadata)
- **Loanmaster**: Legacy database ported to DynamoDB for read scaling
- **Amazon S3**: Document storage (PDFs, TIFFs)
- **Watchman**: Service watchdog for plugin health & hot-reload

## Build & Deployment

- **Configurations**: Debug, Release, Production
- **Deployment**: Publishes to `\\app-deploy.arraytechnology.com\FFN\Applications\eStacker`
- **CI/CD**: Legacy TeamCity builds with ClickOnce deployment

## Key Code Areas

| Area | Location |
|------|----------|
| App Entry | `Program.cs` |
| IoC Configuration | `IoC/HydraRegistry.cs` |  
| UI Shell | `MenuForm.cs` |
| Imaging Core | `Imaging/Stacker/*`, `Imaging/Splitter/*` |
| IT Services | `IT/WatchmanMgr/*`, `IT/PluginManager/*` |
| Data Tools | `Exporter/*`, `Data/DataMain.cs` |

## Document Operations

The application provides extensive document manipulation capabilities:
- **Stacking**: Combine multiple files into master PDFs
- **Splitting**: Break large PDFs into smaller documents  
- **Conversion**: Image-to-PDF, Office-to-PDF (using Aspose)
- **Verification**: PDF corruption/password checks
- **Security**: Encryption and password protection
- **Data Extraction**: OCR/zonal template extraction for structured sales

## Data Flow Patterns

### Imaging Workflow
1. Scanner integration (TWAIN) → Image cache → Temp S3
2. User arranges pages → StackerEngine bundles PDF → Aurora metadata
3. Audit logging → Cache invalidation → Watchman notification

### Export Process  
1. Configuration (query, date range) → ExportService + EF
2. Batch streaming to CSV → Progress reporting
3. Optional copy to network share

### Plugin Management
1. Role validation (`IsSuperUser`) → DLL upload to shared volume
2. Watchman detects changes → Graceful service restart
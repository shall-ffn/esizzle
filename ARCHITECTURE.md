# Hydra Due Diligence App – Architecture & Functionality

**Document version:** 1.1 (2025-07-22)

> NOTE: This document is additive; the original `README.md` remains unmodified.

---

## 1. Overview & Purpose

Hydra **Due Diligence App** (DD-App) is a comprehensive, internal Windows desktop application designed for FFN analysts and administrators. Its primary purpose is to **digitize and streamline the entire lifecycle of loan-related paperwork**. It acts as a digital workbench for managing the massive volume of documents associated with loan portfolios, ensuring all necessary files are correctly processed, organized, associated with the correct loans, and securely archived.

The application is built on the .NET Framework (WinForms) and is broken down into three main functional areas:

1.  **Imaging:** The heart of the application, where all document manipulation and loan-level work occurs.
2.  **IT Admin:** A console for technical staff to manage system configurations, monitor automated services (`Watchman`), and manage application plugins.
3.  **Data:** Utilities for exporting data from the system for reporting or integration with other platforms.

---

## 2. Core Functionality & Use Cases

### 2.1 The Loan Image Workflow (`eStacker`)

The central task for most users is viewing and managing loan documents. This workflow occurs within the `eStacker` module and follows a clear, cascading selection process:

1.  **Select Offering:** The user first chooses a loan portfolio (an "Offering") from a dropdown list. This list is populated by a service call to the **Aurora** database.
2.  **Select Sale:** Based on the chosen Offering, a second dropdown is populated with the specific sales belonging to that portfolio. This ensures data integrity and guides the user.
3.  **Select Loan:** The user is then presented with a list of all loans within the selected sale. 
4.  **Load Images:** Upon selecting a loan, the application retrieves all associated documents. This involves:
    *   Querying **Aurora** for document metadata (e.g., document type, date, S3 path).
    *   Using this metadata to fetch the actual image files (PDF, TIFF) from their storage location in **Amazon S3**.
    *   Displaying the documents in a feature-rich image viewer that supports zooming, panning, and rotation.
    *   Leveraging a `DynamoLoanmasterDbCache` and a local `ImageCache` to accelerate subsequent loads of the same data.

### 2.2 Document Manipulation Capabilities

The application provides a powerful suite of tools for working with PDF and image files, primarily accessed through the "Utilities" and "eStacker" menus:

*   **Stacking:** The core feature of the `eStacker` module. It allows users to combine multiple individual image files (e.g., single-page TIFFs from a scanner) or separate PDFs into a single, cohesive master PDF for a loan.
*   **Splitting (`SplitterForm`):** The inverse of stacking. Users can take a large, multi-page PDF and split it into several smaller, distinct documents (e.g., splitting a 100-page file into a promissory note and a title report).
*   **Conversion Utilities:**
    *   **Image to PDF (`ConvertImageToPDF`):** Converts standard image files (TIFF, JPG, PNG) into PDF format.
    *   **Office to PDF (`ConvertOfficeDocsToPDF`):** Uses the Aspose library to convert Microsoft Office documents (Word, Excel) into non-editable, consistently formatted PDFs for archival.
*   **Verification and Quality Control:**
    *   **Verify PDFs (`VerifyPDF`):** Checks PDFs for corruption, password-protection, or other issues that might impede processing.
    *   **Get Page Counts (`GetPageCounts`):** A utility to quickly audit the page counts of multiple PDF files.
*   **Security (`Encrypt`):** Provides tools to apply password protection and encryption to sensitive documents.
*   **Data Extraction (`StructuredSaleExtractForm`):** An advanced feature to extract specific data points from documents, likely using OCR or zonal templates, for use in structured sales.

---

## 3. High-Level Architecture Diagram

```
┌─────────────────────────────┐      DI / IoC (StructureMap)
│  WinForms UI Layer          │◀─────────────────────────────────────────┐
│  • MenuForm (MDI Shell)     │                                          │
│  • ImagingMain              │                                          │
│  • DataMain                 │                                          │
│  • ITMain                   │                                          │
└───────────────┬─────────────┘                                          │
                │ Event & Command objects                                │
┌───────────────▼─────────────┐  AutoMapper DTO↔Domain                   │
│  Application / Service      │◀──────────────────────────────────────────┤
│  • Form Repositories (IFormRepos)                                      │
│  • ImagingSvc, ExportSvc, WatchmanSvc                                  │
└───────────────┬─────────────┘                                          │
                │ POCO domain objects                                    │
┌───────────────▼─────────────┐                                          │
│  Data Access Layer          │  EF + Custom DAL                         │
│  • global.DAL.Aurora        │                                          │
│  • Hydra Security Repo      │                                          │
│  • DynamoLoanmasterDbCache  │◀── AWS DynamoDB                          │
└───────────────┬─────────────┘                                          │
                │ ADO / EF                                              │
┌───────────────▼─────────────┐                                          │
│   External Systems          │                                          │
│   • Aurora SQL (RDS)        │                                          │
│   • Loanmaster DB (Dynamo)  │                                          │
│   • S3 Imaging buckets      │                                          │
└─────────────────────────────┘                                          │
```

---

## 4. Technical Data Flow Scenarios

### 4.1 Imaging – Scan & Stack
1.  Analyst selects **Imaging** → `ImagingMain`.
2.  UI invokes `ScannerService.Scan()` (TWAIN integration).
3.  Raw images cached in `ImageCache`; also saved to temp S3 path.
4.  User arranges pages; on **Commit**, `StackerEngine` bundles a PDF and writes metadata to **Aurora**.
5.  Audit event persisted, cache invalidated, message sent to Watchman via `IT` channel.

### 4.2 Exporter – Generate Bulk File
1.  **Exporter** form requests config (query, date range).
2.  `ExportService` pulls rows via EF from **Aurora**.
3.  Batches streamed to CSV on local disk; progress events reported to UI.
4.  Completion dialog offers copy to network share `\\app-deploy.arraytechnology.com\FFN\Exports`.

### 4.3 Plugin Manager – Upload Hot-Fix DLL
1.  IT Admin navigates to **Plugin Manager** (within `ITMain`).
2.  Form validates role (`IsSuperUser`).
3.  Selected DLL pushed to plugins folder on shared volume.
4.  Watchman Mgr detects checksum change and restarts service nodes gracefully.

---

## 5. Build & Deployment Pipeline

*   **Configurations:** `Debug`, `Release`, `Production`.
*   **Publish Profile:** Deploys to `\\app-deploy.arraytechnology.com\FFN\Applications\eStacker`.
*   **CI/CD:** Legacy TeamCity builds; artifacts are zipped and copied to the above share where ClickOnce manifests are updated.

---

## 6. Key Code Artifacts

| Area          | Representative File(s)                                    |
|---------------|-----------------------------------------------------------|
| App Entry     | `Program.cs`                                              |
| IoC Config    | `IoC/HydraRegistry.cs`                                    |
| UI Shell      | `MenuForm.cs` & Designer                                  |
| Imaging Core  | `Imaging/Stacker/*`, `Imaging/Splitter/*`                 |
| IT Services   | `IT/WatchmanMgr/*`, `IT/PluginManager/*`                  |
| Data Tools    | `Exporter/*`, `Data/DataMain.cs`                          |

---

## 7. Glossary

*   **Aurora** – MySQL/PostgreSQL compatible Amazon RDS cluster storing loan & imaging metadata.
*   **Loanmaster** – Legacy home-grown DB (ported to DynamoDB for read scaling).
*   **Watchman** – Service watchdog ensuring plugin health & dynamic hot-reload.

---

© 2025 FFN Corp. Internal Use Only.

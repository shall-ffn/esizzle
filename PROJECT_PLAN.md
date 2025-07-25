# Vue.js Hydra Due Diligence App - Complete Implementation Plan

## Project Overview
Modernize the legacy WinForms eStacker application to a Vue.js web application using existing .NET Core Lambda API architecture, preserving all functionality while improving user experience and accessibility.

## Architecture Overview

```
┌─────────────────┐    ┌──────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   Vue.js SPA    │───▶│ .NET Core Lambda │───▶│ MySQL           │    │ Amazon S3       │
│   (Frontend)    │    │ API (Backend)    │    │ (Loanmaster DB) │    │ (Documents)     │
│                 │    │                  │    │                 │    │                 │
│ • Three-panel   │    │ • JWT Auth       │    │ • 337 tables    │    │ • PDF/TIFF      │
│   layout        │    │ • Repository     │    │ • Production    │    │ • Image cache   │
│ • PDF viewer    │    │   pattern        │    │   data          │    │ • Thumbnails    │
│ • Document ops  │    │ • AWS Secrets    │    │ • Existing      │    │                 │
└─────────────────┘    └──────────────────┘    │   relationships │    └─────────────────┘
                                               └─────────────────┘
```

## Phase 1: Backend API Development (Weeks 1-3)

### 1.1 .NET Core Lambda API Setup
**Following existing ArrayAPI pattern:**

```
esizzle-api/
├── src/
│   └── EsizzleAPI/
│       ├── Controllers/
│       │   ├── OfferingController.cs     # Offering management & listing
│       │   ├── SaleController.cs         # Sale operations by offering
│       │   ├── LoanController.cs         # Loan data & filtering
│       │   ├── DocumentController.cs     # Document/Image CRUD operations
│       │   ├── SecurityController.cs     # User access control
│       │   └── ViewerController.cs       # Document viewing & manipulation
│       ├── Models/
│       │   ├── OfferingModel.cs         # Maps to Offerings table
│       │   ├── SaleModel.cs             # Maps to Sales table  
│       │   ├── LoanModel.cs             # Maps to Loan table
│       │   ├── DocumentModel.cs         # Maps to Image table
│       │   └── UserModel.cs             # Maps to Users table
│       ├── Repositories/
│       │   ├── IOfferingRepository.cs
│       │   ├── ISaleRepository.cs
│       │   ├── ILoanRepository.cs
│       │   ├── IDocumentRepository.cs
│       │   └── ISecurityRepository.cs
│       ├── Services/
│       │   ├── S3DocumentService.cs     # S3 integration for documents
│       │   ├── DocumentProcessor.cs     # PDF operations (rotate, redact)
│       │   └── IndexingService.cs       # Document classification
│       ├── Middleware/
│       │   └── AuthorizedUser.cs        # Reuse existing auth middleware
│       ├── Program.cs                   # Lambda configuration
│       └── serverless.template          # AWS SAM deployment
```

### 1.2 Database Integration (Loanmaster MySQL)

**Critical Relationships Preserved:**
```sql
-- Primary workflow chain
Offerings.OfferingID → Sales.ClientID 
Sales.sale_id → Loan.SALE_ID
Loan.loan_id → Image.LoanID

-- Security model
Users.UserID → OfferingUnderwriterAccess.UserID
OfferingUnderwriterAccess.OfferingID → Offerings.OfferingID
```

**Repository Implementation:**
```csharp
public class OfferingRepository : IOfferingRepository
{
    private readonly IDbConnectionFactory _dbConnectionFactory;
    
    public async Task<IEnumerable<OfferingModel>> GetUserOfferingsAsync(int userId)
    {
        using var connection = _dbConnectionFactory.CreateConnection();
        const string sql = @"
            SELECT o.OfferingID, o.OfferingName, o.OfferingDescription, 
                   o.DueDiligenceStart, o.DueDiligenceEnd, o.Visible
            FROM Offerings o
            INNER JOIN OfferingUnderwriterAccess oua ON o.OfferingID = oua.OfferingID
            WHERE oua.UserID = @userId AND o.Visible = 1
            ORDER BY o.OfferingName";
            
        return await connection.QueryAsync<OfferingModel>(sql, new { userId });
    }
}
```

### 1.3 Key API Endpoints

```csharp
// OfferingController
[HttpGet("user-offerings")]
public async Task<IActionResult> GetUserOfferings()

[HttpGet("{id}/sales")]  
public async Task<IActionResult> GetOfferingSales(int id)

// LoanController  
[HttpGet("by-sale/{saleId}")]
public async Task<IActionResult> GetLoansBySale(int saleId)

[HttpGet("{id}")]
public async Task<IActionResult> GetLoan(int id)

// DocumentController
[HttpGet("by-loan/{loanId}")]
public async Task<IActionResult> GetDocumentsByLoan(int loanId)

[HttpGet("{id}/url")]
public async Task<IActionResult> GetDocumentUrl(int id)

[HttpPost("{id}/rotate")]
public async Task<IActionResult> RotateDocument(int id, [FromBody] RotateRequest request)

[HttpPost("{id}/redact")]
public async Task<IActionResult> RedactDocument(int id, [FromBody] RedactRequest request)

[HttpPut("{id}/classification")]
public async Task<IActionResult> UpdateDocumentType(int id, [FromBody] string documentType)
```

## Phase 2: Vue.js Frontend Development (Weeks 4-8)

### 2.1 Project Setup & Architecture
```bash
# Vue 3 + TypeScript + Vite setup
npm create vue@latest esizzle-frontend
cd esizzle-frontend
npm install

# Additional dependencies
npm install @headlessui/vue @heroicons/vue tailwindcss
npm install axios pinia vue-router
npm install pdfjs-dist fabric konva
```

### 2.2 Component Structure

```
src/
├── components/
│   ├── layout/
│   │   ├── AppShell.vue           # Main 3-panel MDI layout
│   │   ├── LeftPanel.vue          # Document grid + actions
│   │   ├── CenterPanel.vue        # PDF viewer + thumbnails
│   │   └── RightPanel.vue         # Selection + indexing
│   ├── selection/
│   │   ├── OfferingSelector.vue   # Dropdown with user's offerings
│   │   ├── SaleSelector.vue       # Sales filtered by offering
│   │   └── LoanList.vue           # Loans with search/filter
│   ├── documents/
│   │   ├── DocumentGrid.vue       # Table matching legacy layout
│   │   ├── DocumentActions.vue    # Bookmarks, Redactions, Save Status tabs
│   │   └── DocumentViewer.vue     # PDF.js integration
│   ├── viewer/
│   │   ├── PDFCanvas.vue          # Main document display
│   │   ├── ThumbnailStrip.vue     # Page thumbnails
│   │   ├── ViewerToolbar.vue      # Zoom, rotate, tools
│   │   └── RedactionTool.vue      # Canvas-based redaction
│   └── indexing/
│       ├── IndexingPanel.vue      # Document classification
│       ├── DataExtraction.vue     # OCR data entry
│       └── SearchIndex.vue        # Index search functionality
├── stores/
│   ├── main.ts                    # Primary Pinia store
│   ├── auth.ts                    # Authentication state
│   └── documents.ts               # Document operations state
├── services/
│   ├── api.ts                     # Axios HTTP client
│   ├── auth.service.ts            # JWT token management
│   └── document.service.ts        # Document operations
└── types/
    ├── api.types.ts               # API response interfaces
    ├── domain.types.ts            # Business domain types
    └── ui.types.ts                # UI-specific types
```

### 2.3 State Management (Pinia)

```typescript
// stores/main.ts
export const useMainStore = defineStore('main', {
  state: () => ({
    // User context  
    currentUser: null as User | null,
    userOfferings: [] as Offering[],
    
    // Selection hierarchy (preserving exact legacy workflow)
    selectedOffering: null as Offering | null,
    availableSales: [] as Sale[],
    selectedSale: null as Sale | null,
    availableLoans: [] as Loan[],
    selectedLoan: null as Loan | null,
    filteredLoans: [] as Loan[],
    loanSearchTerm: '',
    
    // Document state
    availableDocuments: [] as DocumentModel[],
    selectedDocument: null as DocumentModel | null,
    currentPage: 1,
    totalPages: 0,
    
    // UI state matching legacy
    documentViewMode: 'single' as 'single' | 'thumbnail',
    zoomLevel: 100,
    panelSizes: {
      left: 300,
      center: 800, 
      right: 350
    },
    
    // Loading states
    loading: {
      offerings: false,
      sales: false,
      loans: false,
      documents: false,
      documentContent: false
    }
  }),

  getters: {
    visibleLoans: (state) => {
      if (!state.loanSearchTerm) return state.availableLoans
      return state.availableLoans.filter(loan => 
        loan.assetName.toLowerCase().includes(state.loanSearchTerm.toLowerCase()) ||
        loan.assetNo.toLowerCase().includes(state.loanSearchTerm.toLowerCase())
      )
    },
    
    documentsByType: (state) => {
      return state.availableDocuments.reduce((acc, doc) => {
        const type = doc.documentType || 'Unclassified'
        if (!acc[type]) acc[type] = []
        acc[type].push(doc)
        return acc
      }, {} as Record<string, DocumentModel[]>)
    }
  },

  actions: {
    // Selection workflow matching legacy cascading dropdowns
    async selectOffering(offering: Offering) {
      this.selectedOffering = offering
      this.selectedSale = null
      this.selectedLoan = null
      this.selectedDocument = null
      await this.loadSales()
    },

    async selectSale(sale: Sale) {
      this.selectedSale = sale  
      this.selectedLoan = null
      this.selectedDocument = null
      await this.loadLoans()
    },

    async selectLoan(loan: Loan) {
      this.selectedLoan = loan
      this.selectedDocument = null
      await this.loadDocuments()
    },

    async selectDocument(document: DocumentModel) {
      this.selectedDocument = document
      this.currentPage = 1
      // Load document content from S3
      await this.loadDocumentContent(document.id)
    }
  }
})
```

## Phase 3: Document Operations (Weeks 9-10)

### 3.1 PDF.js Integration
```typescript
// services/pdf.service.ts
import * as pdfjsLib from 'pdfjs-dist'

export class PDFService {
  private pdfjsLib = pdfjsLib

  constructor() {
    // Configure worker for performance
    pdfjsLib.GlobalWorkerOptions.workerSrc = '/pdf.worker.min.js'
  }

  async loadDocument(url: string): Promise<PDFDocument> {
    const loadingTask = pdfjsLib.getDocument(url)
    return await loadingTask.promise
  }

  async renderPage(pdf: PDFDocument, pageNum: number, scale: number = 1): Promise<HTMLCanvasElement> {
    const page = await pdf.getPage(pageNum)
    const viewport = page.getViewport({ scale })
    
    const canvas = document.createElement('canvas')
    const context = canvas.getContext('2d')!
    canvas.height = viewport.height
    canvas.width = viewport.width

    await page.render({ canvasContext: context, viewport }).promise
    return canvas
  }

  async generateThumbnails(pdf: PDFDocument, maxPages: number = 50): Promise<string[]> {
    const thumbnails: string[] = []
    const numPages = Math.min(pdf.numPages, maxPages)
    
    for (let i = 1; i <= numPages; i++) {
      const canvas = await this.renderPage(pdf, i, 0.2) // Small scale for thumbnails
      thumbnails.push(canvas.toDataURL('image/jpeg', 0.7))
    }
    
    return thumbnails
  }
}
```

### 3.2 Document Manipulation Tools
```vue
<!-- components/viewer/RedactionTool.vue -->
<template>
  <div class="redaction-tool">
    <canvas 
      ref="redactionCanvas"
      @mousedown="startRedaction"
      @mousemove="drawRedaction"
      @mouseup="endRedaction"
      class="absolute top-0 left-0 z-10"
    />
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'

const redactionCanvas = ref<HTMLCanvasElement>()
const isDrawing = ref(false)
const redactionAreas = ref<RedactionArea[]>([])

interface RedactionArea {
  x: number
  y: number
  width: number
  height: number
  pageNumber: number
}

const startRedaction = (event: MouseEvent) => {
  isDrawing.value = true
  const rect = redactionCanvas.value!.getBoundingClientRect()
  // Start drawing redaction rectangle
}

const drawRedaction = (event: MouseEvent) => {
  if (!isDrawing.value) return
  // Draw temporary redaction overlay
}

const endRedaction = (event: MouseEvent) => {
  isDrawing.value = false
  // Finalize redaction area and send to backend
  submitRedaction()
}

const submitRedaction = async () => {
  // Send redaction coordinates to .NET API
  await documentService.redactDocument(selectedDocument.value!.id, redactionAreas.value)
}
</script>
```

## Phase 4: Integration & Testing (Weeks 11-12)

### 4.1 Local Development Setup
```bash
# Backend API (local IIS Express)
cd esizzle-api/src/EsizzleAPI
dotnet run --urls="https://localhost:5001"

# Frontend (local Vite dev server)  
cd esizzle-frontend
npm run dev # Runs on http://localhost:5173
```

### 4.2 Authentication Integration
```typescript
// services/auth.service.ts
export class AuthService {
  private readonly API_BASE_URL = 'https://localhost:5001/api/v1'

  async login(username: string, password: string): Promise<AuthResult> {
    const response = await axios.post(`${this.API_BASE_URL}/auth/login`, {
      username,
      password
    })
    
    const { token, user } = response.data
    localStorage.setItem('auth_token', token)
    axios.defaults.headers.common['Authorization'] = `Bearer ${token}`
    
    return { token, user }
  }

  async getCurrentUser(): Promise<User> {
    const response = await axios.get(`${this.API_BASE_URL}/auth/me`)
    return response.data
  }

  logout() {
    localStorage.removeItem('auth_token')
    delete axios.defaults.headers.common['Authorization']
  }
}
```

## Phase 5: Testing & Validation (Weeks 13-14)

### 5.1 Data Integrity Testing
- Verify all 337 Loanmaster tables remain intact
- Test user access control with OfferingUnderwriterAccess
- Validate document operations preserve S3 paths
- Ensure audit logging matches legacy system

### 5.2 User Acceptance Testing  
- Side-by-side comparison with WinForms app
- Test all document manipulation features
- Verify keyboard shortcuts work as expected
- Performance benchmarking vs legacy app

## Success Metrics
- **Functionality Parity**: 100% of legacy features preserved
- **Performance**: Sub-2 second document loading
- **Data Integrity**: Zero data loss during migration
- **Security**: Maintain existing access controls
- **User Experience**: Match or exceed legacy workflow efficiency

## Technology Stack Summary

**Frontend:**
- Vue 3 + TypeScript + Vite
- Tailwind CSS + HeadlessUI  
- Pinia state management
- PDF.js for document rendering
- Fabric.js for canvas operations

**Backend:**
- .NET 8 + ASP.NET Core
- MySQL (existing Loanmaster DB)
- Local development only (no AWS deployment)
- JWT authentication
- Repository pattern with Dapper

**Development Environment:**
- Local IIS Express for API
- Vite dev server for frontend
- MySQL connection to existing Loanmaster database
- Local S3 mock or file system for document storage

## Important Notes
- **NO AWS DEPLOYMENTS** - All development and testing will be done locally
- Preserve existing production Loanmaster database structure
- Focus on functionality parity with legacy WinForms application
- Maintain existing user workflows and security model
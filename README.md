# Hydra Due Diligence - eStacker Web Application

A modern Vue.js web application that reimagines the legacy WinForms eStacker application for loan document management and due diligence processing.

## Project Structure

```
esizzle/
├── esizzle-api/                    # .NET Core Lambda API Backend
│   └── src/EsizzleAPI/
│       ├── Controllers/            # API Controllers
│       ├── Models/                 # Data Models
│       ├── Repositories/           # Data Access Layer
│       ├── Middleware/             # Custom Middleware
│       └── Program.cs              # Application Entry Point
├── esizzle-frontend/               # Vue.js Frontend Application
│   ├── src/
│   │   ├── components/             # Vue Components
│   │   │   ├── layout/             # Layout Components
│   │   │   └── viewer/             # Document Viewer Components
│   │   ├── services/               # API Services
│   │   ├── stores/                 # Pinia State Management
│   │   ├── types/                  # TypeScript Type Definitions
│   │   └── views/                  # Page Views
│   ├── public/                     # Public Assets
│   └── package.json                # Frontend Dependencies
└── PROJECT_PLAN.md                # Detailed Implementation Plan
```

## Technology Stack

### Backend (.NET Core API)
- **.NET 8** - Modern web API framework
- **ASP.NET Core** - Web API with Lambda hosting
- **Dapper** - Lightweight ORM for database access
- **MySQL** - Existing Loanmaster database
- **AWS Lambda** - Serverless deployment (configured but not deployed)
- **JWT Bearer** - Authentication
- **Swagger/OpenAPI** - API documentation

### Frontend (Vue.js)
- **Vue 3** - Progressive JavaScript framework
- **TypeScript** - Type-safe JavaScript
- **Vite** - Fast build tool and development server
- **Tailwind CSS** - Utility-first CSS framework
- **Pinia** - State management
- **Vue Router** - Client-side routing
- **PDF.js** - Client-side PDF rendering
- **Axios** - HTTP client for API communication

## Features

### Core Functionality
- **Three-Panel Layout** - Matches legacy eStacker interface exactly
- **Hierarchical Selection** - Offering → Sale → Loan → Document workflow
- **Document Viewing** - PDF.js integration with zoom, rotate, navigation
- **Search & Filter** - Real-time search for loans and documents
- **Document Classification** - Index documents by type
- **Security** - Role-based access control via OfferingUnderwriterAccess

### Document Operations
- **View PDF Documents** - High-quality PDF rendering
- **Rotate Documents** - 90°, 180°, 270° rotation
- **Redaction** - Mark sensitive information for removal
- **Page Navigation** - Navigate multi-page documents
- **Thumbnails** - Quick page overview and navigation

## Database Schema

The application connects to the existing **Loanmaster MySQL database** with 337 tables. Key relationships:

```sql
-- Primary workflow chain
Offerings.OfferingID → Sales.ClientID 
Sales.sale_id → Loan.SALE_ID
Loan.loan_id → Image.LoanID

-- Security model
Users.UserID → OfferingUnderwriterAccess.UserID
OfferingUnderwriterAccess.OfferingID → Offerings.OfferingID
```

## Local Development Setup

### Prerequisites
- **.NET 8 SDK** - For backend API development
- **Node.js 18+** - For frontend development
- **MySQL Server** - Access to Loanmaster database
- **Git** - Version control

### Backend Setup (API)

1. **Navigate to API directory:**
   ```bash
   cd esizzle-api/src/EsizzleAPI
   ```

2. **Install dependencies:**
   ```bash
   dotnet restore
   ```

3. **Configure database connection:**
   Update `appsettings.Development.json` with your MySQL connection string:
   ```json
   {
     "ConnectionStrings": {
       "LoanmasterDatabase": "Server=localhost;Port=3306;Database=Loanmaster;User=root;Password=your_password;"
     }
   }
   ```

4. **Run the API:**
   ```bash
   dotnet run
   ```
   
   The API will start at `https://localhost:5001` with Swagger documentation available.

### Frontend Setup (Vue.js)

1. **Navigate to frontend directory:**
   ```bash
   cd esizzle-frontend
   ```

2. **Install dependencies:**
   ```bash
   npm install
   ```

3. **Start development server:**
   ```bash
   npm run dev
   ```
   
   The application will start at `http://localhost:5173`

## Development Workflow

### API Development
- **Controllers**: Add new endpoints in `Controllers/` directory
- **Models**: Define data models in `Models/` directory
- **Repositories**: Implement data access in `Repositories/` directory
- **Testing**: Use Swagger UI at `https://localhost:5001/swagger`

### Frontend Development
- **Components**: Create reusable Vue components in `components/` directory
- **Views**: Add new pages in `views/` directory
- **Services**: Implement API calls in `services/` directory
- **State**: Manage application state with Pinia stores in `stores/` directory

### Code Quality
- **Backend**: C# code style with nullable reference types enabled
- **Frontend**: ESLint + Prettier for code formatting
- **TypeScript**: Strict type checking enabled
- **Tailwind CSS**: Utility-first styling approach

## Authentication

The application uses **JWT Bearer token authentication**:

- **Development**: Mock authentication accepts any username/password
- **Production**: Integrate with existing Active Directory via JWT tokens
- **Authorization**: Role-based access via `OfferingUnderwriterAccess` table

## Deployment

### Local Development Only
**Important**: This project is configured for **local development only**. No AWS deployments should be performed.

### Build Commands
```bash
# Backend
cd esizzle-api/src/EsizzleAPI
dotnet build --configuration Release

# Frontend  
cd esizzle-frontend
npm run build
```

## Key Components

### Backend Controllers
- **OfferingController** - Manage loan portfolio offerings
- **SaleController** - Handle sales within offerings
- **LoanController** - Loan data and search operations
- **DocumentController** - Document CRUD and manipulation

### Frontend Components
- **AppShell** - Main three-panel layout
- **LeftPanel** - Document grid and actions
- **CenterPanel** - PDF viewer and tools
- **RightPanel** - Selection hierarchy and indexing
- **PDFViewer** - PDF.js integration component

## Data Flow

1. **User Authentication** → JWT token stored in localStorage
2. **Load Offerings** → API call to get user's accessible offerings
3. **Select Offering** → Load associated sales
4. **Select Sale** → Load loans for that sale
5. **Select Loan** → Load documents for that loan
6. **Select Document** → Generate presigned URL and load PDF

## Security Considerations

- **Database Access**: All queries filtered by user's OfferingUnderwriterAccess
- **Document URLs**: Temporary presigned URLs for document access
- **Input Validation**: Server-side validation for all API endpoints
- **SQL Injection Prevention**: Parameterized queries using Dapper
- **XSS Prevention**: Vue.js built-in protection + Content Security Policy

## Performance Optimizations

- **Virtual Scrolling**: Large lists use virtual scrolling
- **Lazy Loading**: Documents loaded only when selected
- **Caching**: Client-side caching of user data and document metadata
- **PDF Rendering**: Efficient PDF.js rendering with worker threads
- **Database Indexing**: Optimized queries for large datasets

## Browser Support

- **Chrome 90+** (recommended)
- **Firefox 88+**
- **Safari 14+**
- **Edge 90+**

## Quick Development Setup

### 1. Run Environment Check
```bash
./scripts/dev-check.sh
```

### 2. Backend Setup
```bash
cd esizzle-api/src/EsizzleAPI
dotnet restore
dotnet run
```

### 3. Frontend Setup  
```bash
cd esizzle-frontend
npm install
npm run dev
```

### 4. Verify Setup
- Frontend: http://localhost:5173
- Backend API: https://localhost:5001/swagger
- Health Check: https://localhost:5001/api/health

## Development Features

- **DevStatus Component**: Development status panel (bottom-right corner in dev mode)
- **Health Checks**: API and database connectivity monitoring  
- **Hot Reload**: Both frontend and backend support hot reload
- **Type Safety**: Full TypeScript coverage
- **Error Handling**: Comprehensive error boundaries and logging

## Contributing

1. Follow the existing code patterns and conventions
2. Maintain TypeScript strict typing
3. Add appropriate error handling
4. Test changes with the Loanmaster database
5. Update documentation for new features
6. Use the development status panel to verify your changes

## License

Internal use only - FFN Corporation. All rights reserved.
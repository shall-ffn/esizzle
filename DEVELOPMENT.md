# Development Setup Guide

This guide covers setting up the local development environment for the Hydra Due Diligence eStacker Web Application.

## Prerequisites

### Software Requirements
- **.NET 8.0 SDK** - For backend API development
  ```bash
  # Check if installed
  dotnet --version
  # Should show 8.0.x or higher
  ```

- **Node.js 18+** - For frontend development
  ```bash
  # Check if installed
  node --version
  npm --version
  ```

- **MySQL Server 8.0+** - Database server
  - Ensure you have access to the Loanmaster database
  - Connection details should be available in your environment

- **Git** - Version control
  ```bash
  git --version
  ```

### Development Tools (Recommended)
- **Visual Studio Code** with extensions:
  - C# Dev Kit
  - Vue - Official
  - TypeScript Vue Plugin (Volar)
  - Tailwind CSS IntelliSense
  - MySQL (cweijan.vscode-mysql-client2)

- **Postman** or **Insomnia** - For API testing
- **MySQL Workbench** - Database management

## Project Structure Overview

```
esizzle/
├── esizzle-api/                    # .NET Core Backend API
│   ├── src/EsizzleAPI/
│   │   ├── Controllers/            # API Controllers
│   │   ├── Models/                 # Data Models & DTOs
│   │   ├── Repositories/           # Data Access Layer
│   │   ├── Middleware/             # Custom Middleware
│   │   ├── Program.cs              # Application Entry Point
│   │   └── appsettings.json        # Configuration
│   └── EsizzleAPI.sln              # Solution File
├── esizzle-frontend/               # Vue.js Frontend
│   ├── src/
│   │   ├── components/             # Vue Components
│   │   ├── services/               # API Services
│   │   ├── stores/                 # Pinia State Management
│   │   ├── types/                  # TypeScript Definitions
│   │   └── views/                  # Page Views
│   ├── public/                     # Static Assets
│   ├── package.json                # Dependencies
│   └── vite.config.ts              # Build Configuration
├── README.md                       # Project Documentation
├── PROJECT_PLAN.md                 # Implementation Plan
└── DEVELOPMENT.md                  # This file
```

## Backend Setup (.NET API)

### 1. Navigate to the API Directory
```bash
cd esizzle-api/src/EsizzleAPI
```

### 2. Restore Dependencies
```bash
dotnet restore
```

### 3. Configure Database Connection

Create or update `appsettings.Development.json`:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "EsizzleAPI": "Debug"
    }
  },
  "ConnectionStrings": {
    "LoanmasterDatabase": "Server=localhost;Port=3306;Database=Loanmaster;User=your_username;Password=your_password;AllowUserVariables=true;UseAffectedRows=false;"
  },
  "JwtSettings": {
    "SecretKey": "your-development-secret-key-here",
    "Issuer": "EsizzleAPI",
    "Audience": "EsizzleApp",
    "ExpirationMinutes": 60
  }
}
```

**Important Security Notes:**
- Never commit real passwords to version control
- Use environment variables for sensitive data in production
- The JWT secret key should be at least 32 characters for security

### 4. Environment Variables (Alternative Configuration)

You can also use environment variables instead of appsettings files:
```bash
export CONNECTIONSTRINGS__LOANMASTERDATABASE="Server=localhost;Port=3306;Database=Loanmaster;User=root;Password=yourpassword;"
export JWTSETTINGS__SECRETKEY="your-super-secret-jwt-key-for-development"
```

### 5. Build the API
```bash
dotnet build
```

### 6. Run the API
```bash
dotnet run
```

The API will start at:
- **HTTPS**: `https://localhost:5001`
- **HTTP**: `http://localhost:5000`
- **Swagger UI**: `https://localhost:5001/swagger`

### 7. Verify API is Running
Open your browser and navigate to `https://localhost:5001/swagger` to see the API documentation.

## Frontend Setup (Vue.js)

### 1. Navigate to the Frontend Directory
```bash
cd esizzle-frontend
```

### 2. Install Dependencies
```bash
npm install
```

### 3. Configure Environment Variables

Create `.env.development`:
```bash
# API Configuration
VITE_API_BASE_URL=https://localhost:5001/api
VITE_API_TIMEOUT=30000

# Feature Flags
VITE_ENABLE_DEBUG_LOGGING=true
VITE_ENABLE_MOCK_AUTH=true

# PDF.js Configuration
VITE_PDF_WORKER_URL=/pdf.worker.min.js
VITE_PDF_CMAP_URL=/cmaps/
```

### 4. Set up PDF.js Assets

Copy PDF.js assets to the public directory:
```bash
# If you have pdf.js installed via npm
cp node_modules/pdfjs-dist/build/pdf.worker.min.js public/
cp -r node_modules/pdfjs-dist/cmaps public/
```

### 5. Start Development Server
```bash
npm run dev
```

The application will start at:
- **Frontend**: `http://localhost:5173`

### 6. Verify Frontend is Running
Open your browser and navigate to `http://localhost:5173` to see the application.

## Full Stack Development Workflow

### 1. Start Both Services
You'll need two terminal windows:

**Terminal 1 - Backend API:**
```bash
cd esizzle-api/src/EsizzleAPI
dotnet run
```

**Terminal 2 - Frontend:**
```bash
cd esizzle-frontend
npm run dev
```

### 2. Development URLs
- **Frontend Application**: http://localhost:5173
- **Backend API**: https://localhost:5001
- **API Documentation**: https://localhost:5001/swagger

### 3. Testing the Integration
1. Open the frontend at http://localhost:5173
2. The application should load and show the three-panel layout
3. Check browser console for any errors
4. Test the selection workflow: Offering → Sale → Loan → Document

## Database Setup

### 1. MySQL Connection Testing
Test your database connection using MySQL Workbench or command line:
```bash
mysql -h localhost -u your_username -p Loanmaster
```

### 2. Verify Required Tables
The application uses these key tables:
- `Offerings`
- `OfferingUnderwriterAccess`  
- `Sales`
- `Loan`
- `Image` (documents)
- `Users`

### 3. Sample Query to Test Access
```sql
SELECT COUNT(*) FROM Offerings;
SELECT COUNT(*) FROM Sales;
SELECT COUNT(*) FROM Loan;
SELECT COUNT(*) FROM Image;
```

## Common Development Tasks

### Backend Development

#### Adding a New API Endpoint
1. Create/update the controller in `Controllers/`
2. Add any new models in `Models/`
3. Update repository if needed in `Repositories/`
4. Test using Swagger UI

#### Database Queries
- Use Dapper for database access
- All queries are in repository classes
- Follow the existing patterns for security filtering

#### Debugging
```bash
# Run with detailed logging
dotnet run --environment Development

# Run specific configuration
dotnet run --configuration Debug
```

### Frontend Development

#### Adding a New Component
1. Create component in `src/components/`
2. Export from appropriate index file
3. Import and use in parent components
4. Follow TypeScript patterns

#### State Management
- Use Pinia stores in `src/stores/`
- Follow the hierarchical selection pattern
- Update loading states appropriately

#### Styling
- Use Tailwind CSS utility classes
- Follow the existing color scheme (Hydra brand colors)
- Maintain responsive design principles

#### API Integration
- Use services in `src/services/`
- Handle errors consistently
- Update loading states in stores

### Code Quality

#### Backend (.NET)
```bash
# Format code
dotnet format

# Run static analysis
dotnet build --verbosity normal
```

#### Frontend (Vue.js)
```bash
# Lint code
npm run lint

# Format code  
npm run format

# Type checking
npm run type-check

# Build for production
npm run build
```

## Troubleshooting

### Backend Issues

#### Connection String Problems
- Check MySQL server is running
- Verify username/password
- Ensure database name is correct
- Check firewall settings

#### Port Conflicts
```bash
# Check what's using port 5001
netstat -an | grep 5001
# or
lsof -i :5001
```

#### SSL Certificate Issues
```bash
# Trust development certificates
dotnet dev-certs https --trust
```

### Frontend Issues

#### Node Module Problems
```bash
# Clear cache and reinstall
rm -rf node_modules package-lock.json
npm install
```

#### CORS Issues
- Ensure backend CORS is configured for http://localhost:5173
- Check browser console for CORS errors

#### PDF.js Issues
- Verify PDF.js assets are in public/ directory
- Check browser console for worker loading errors

### Database Issues

#### Connection Timeouts
- Increase connection timeout in connection string
- Check network connectivity to database server

#### Access Denied
- Verify user has proper permissions on Loanmaster database
- Check OfferingUnderwriterAccess table for user access rights

## Performance Optimization

### Backend
- Use async/await patterns consistently
- Implement proper caching strategies
- Optimize database queries

### Frontend
- Use lazy loading for large components
- Implement virtual scrolling for large lists
- Optimize PDF rendering performance

## Security Considerations

### Development Environment
- Use HTTPS for API endpoints
- Implement proper CORS configuration
- Validate all inputs server-side
- Use parameterized queries to prevent SQL injection

### Authentication
- JWT tokens expire after 60 minutes in development
- Implement proper token refresh mechanism
- Store tokens securely (localStorage for dev, httpOnly cookies for prod)

## Deployment Notes

**Important**: This application is configured for **local development only**. Do not deploy to AWS or any production environment without explicit approval and additional security configuration.

### Local Build Testing
```bash
# Backend
cd esizzle-api/src/EsizzleAPI
dotnet build --configuration Release

# Frontend
cd esizzle-frontend
npm run build
```

## Getting Help

### Documentation Resources
- [.NET 8 Documentation](https://docs.microsoft.com/en-us/dotnet/)
- [Vue.js 3 Documentation](https://vuejs.org/)
- [TypeScript Documentation](https://www.typescriptlang.org/docs/)
- [Tailwind CSS Documentation](https://tailwindcss.com/docs)
- [PDF.js Documentation](https://mozilla.github.io/pdf.js/)

### Common Commands Reference

```bash
# Backend
dotnet restore                 # Restore packages
dotnet build                  # Build project
dotnet run                    # Run development server
dotnet test                   # Run tests

# Frontend  
npm install                   # Install dependencies
npm run dev                   # Start development server
npm run build                 # Build for production
npm run preview               # Preview production build
npm run lint                  # Run linter
npm run type-check           # Run TypeScript checks

# Database
mysql -u username -p          # Connect to MySQL
mysqldump -u username -p db   # Backup database
```

This development environment provides a complete setup for modernizing the legacy eStacker application with a clean separation between backend API and frontend application while maintaining all the functionality of the original Windows Forms application.
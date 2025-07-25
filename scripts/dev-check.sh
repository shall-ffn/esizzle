#!/bin/bash

# Development Environment Check Script
# This script validates that the development environment is properly set up

echo "🔍 Hydra Due Diligence - Development Environment Check"
echo "=================================================="

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Check functions
check_command() {
    if command -v "$1" &> /dev/null; then
        echo -e "${GREEN}✓${NC} $1 is installed"
        return 0
    else
        echo -e "${RED}✗${NC} $1 is not installed"
        return 1
    fi
}

check_version() {
    local cmd=$1
    local min_version=$2
    local version_output=$($cmd --version 2>/dev/null | head -n1)
    echo -e "${GREEN}  ${NC} $version_output"
}

echo ""
echo "📋 Checking Prerequisites..."

# Check .NET
echo ""
echo "🔹 .NET SDK:"
if check_command "dotnet"; then
    check_version "dotnet" "8.0"
else
    echo -e "${YELLOW}   Install from: https://dotnet.microsoft.com/download${NC}"
fi

# Check Node.js
echo ""
echo "🔹 Node.js:"
if check_command "node"; then
    check_version "node" "18.0"
else
    echo -e "${YELLOW}   Install from: https://nodejs.org/${NC}"
fi

# Check npm
echo ""
echo "🔹 npm:"
if check_command "npm"; then
    check_version "npm" "8.0"
fi

# Check MySQL
echo ""
echo "🔹 MySQL:"
if check_command "mysql"; then
    check_version "mysql" "8.0"
else
    echo -e "${YELLOW}   Install MySQL Server or ensure it's accessible${NC}"
fi

# Check Git
echo ""
echo "🔹 Git:"
if check_command "git"; then
    check_version "git" "2.0"
fi

echo ""
echo "📁 Checking Project Structure..."

# Check backend
if [ -d "esizzle-api/src/EsizzleAPI" ]; then
    echo -e "${GREEN}✓${NC} Backend API directory exists"
    
    if [ -f "esizzle-api/src/EsizzleAPI/Program.cs" ]; then
        echo -e "${GREEN}✓${NC} Program.cs found"
    else
        echo -e "${RED}✗${NC} Program.cs not found"
    fi
else
    echo -e "${RED}✗${NC} Backend API directory not found"
fi

# Check frontend
if [ -d "esizzle-frontend" ]; then
    echo -e "${GREEN}✓${NC} Frontend directory exists"
    
    if [ -f "esizzle-frontend/package.json" ]; then
        echo -e "${GREEN}✓${NC} package.json found"
    else
        echo -e "${RED}✗${NC} package.json not found"
    fi
    
    if [ -d "esizzle-frontend/src" ]; then
        echo -e "${GREEN}✓${NC} src directory found"
    else
        echo -e "${RED}✗${NC} src directory not found"
    fi
else
    echo -e "${RED}✗${NC} Frontend directory not found"
fi

echo ""
echo "🔧 Checking Backend Dependencies..."

cd esizzle-api/src/EsizzleAPI 2>/dev/null
if [ $? -eq 0 ]; then
    if [ -f "EsizzleAPI.csproj" ]; then
        echo -e "${GREEN}✓${NC} Project file found"
        
        # Check if dependencies are restored
        if [ -d "obj" ]; then
            echo -e "${GREEN}✓${NC} Dependencies appear to be restored"
        else
            echo -e "${YELLOW}⚠${NC} Run 'dotnet restore' to restore dependencies"
        fi
        
        # Try to build
        echo -e "${YELLOW}🔨${NC} Testing backend build..."
        if dotnet build --verbosity quiet > /dev/null 2>&1; then
            echo -e "${GREEN}✓${NC} Backend builds successfully"
        else
            echo -e "${RED}✗${NC} Backend build failed"
            echo -e "${YELLOW}   Run 'dotnet build' for details${NC}"
        fi
    fi
    cd - > /dev/null
fi

echo ""
echo "🎨 Checking Frontend Dependencies..."

cd esizzle-frontend 2>/dev/null
if [ $? -eq 0 ]; then
    if [ -d "node_modules" ]; then
        echo -e "${GREEN}✓${NC} Node modules installed"
    else
        echo -e "${YELLOW}⚠${NC} Run 'npm install' to install dependencies"
    fi
    
    # Check for key files
    if [ -f "src/main.ts" ]; then
        echo -e "${GREEN}✓${NC} Main TypeScript file found"
    fi
    
    if [ -f "src/App.vue" ]; then
        echo -e "${GREEN}✓${NC} Root Vue component found"
    fi
    
    # Check if build works
    if [ -d "node_modules" ]; then
        echo -e "${YELLOW}🔨${NC} Testing frontend build..."
        if npm run build > /dev/null 2>&1; then
            echo -e "${GREEN}✓${NC} Frontend builds successfully"
        else
            echo -e "${RED}✗${NC} Frontend build failed"
            echo -e "${YELLOW}   Run 'npm run build' for details${NC}"
        fi
    fi
    
    cd - > /dev/null
fi

echo ""
echo "📄 Checking Configuration Files..."

# Check for development configuration
if [ -f "esizzle-api/src/EsizzleAPI/appsettings.Development.json" ]; then
    echo -e "${GREEN}✓${NC} Backend development config found"
else
    echo -e "${YELLOW}⚠${NC} Create appsettings.Development.json for backend"
fi

if [ -f "esizzle-frontend/.env.development" ]; then
    echo -e "${GREEN}✓${NC} Frontend development config found"
else
    echo -e "${YELLOW}⚠${NC} Create .env.development for frontend"
fi

echo ""
echo "🌐 Development URLs:"
echo -e "${GREEN}Frontend:${NC} http://localhost:5173"
echo -e "${GREEN}Backend API:${NC} https://localhost:5001"
echo -e "${GREEN}API Docs:${NC} https://localhost:5001/swagger"

echo ""
echo "🚀 Quick Start Commands:"
echo ""
echo "Backend (Terminal 1):"
echo "  cd esizzle-api/src/EsizzleAPI"
echo "  dotnet restore"
echo "  dotnet run"
echo ""
echo "Frontend (Terminal 2):"
echo "  cd esizzle-frontend"
echo "  npm install"
echo "  npm run dev"

echo ""
echo "=================================================="
echo "Development environment check complete!"
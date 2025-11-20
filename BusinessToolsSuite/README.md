# Business Tools Suite (C# Rewrite)

A modern, cross-platform desktop application suite built with .NET 8 and Avalonia UI, providing three essential business management tools.

## 🎯 Overview

Business Tools Suite combines three powerful business tools into one unified application:

1. **📦 ExpireWise** - Modern expiration tracking and inventory lifecycle management
2. **📊 Allocation Buddy** - Store allocation management with smart categorization
3. **📋 Essentials Buddy** - Business Central bin contents reporting

## 🏗️ Architecture

This application follows **Clean Architecture** principles with a modular design:

```
BusinessToolsSuite/
├── src/
│   ├── BusinessToolsSuite.Core/              # Domain models, interfaces, business logic
│   ├── BusinessToolsSuite.Infrastructure/     # Data access, external services
│   ├── BusinessToolsSuite.Shared/             # Shared UI components, utilities
│   ├── BusinessToolsSuite.Desktop/            # Main Avalonia UI application
│   └── Features/
│       ├── BusinessToolsSuite.Features.ExpireWise/
│       ├── BusinessToolsSuite.Features.AllocationBuddy/
│       └── BusinessToolsSuite.Features.EssentialsBuddy/
└── tests/
    └── BusinessToolsSuite.UnitTests/
```

## 🚀 Technology Stack

- **.NET 8.0** - Modern, cross-platform framework
- **C# 12** - Latest language features
- **Avalonia UI 11.x** - Cross-platform XAML-based UI framework
- **CommunityToolkit.Mvvm** - Modern MVVM helpers with source generators
- **LiteDB** - Embedded NoSQL database
- **Serilog** - Structured logging
- **xUnit** - Unit testing framework

## 📋 Prerequisites

- .NET 8.0 SDK or later
- Windows 10/11, macOS 10.15+, or Linux (Ubuntu 20.04+)
- Visual Studio 2022, JetBrains Rider, or VS Code

## 🛠️ Getting Started

### Clone and Build

```bash
# Navigate to the solution directory
cd BusinessToolsSuite

# Restore dependencies
dotnet restore

# Build the solution
dotnet build

# Run the application
dotnet run --project src/BusinessToolsSuite.Desktop
```

### Run Tests

```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test /p:CollectCoverage=true
```

## 📦 Project Structure

### Core Layer
- **Domain Models**: Business entities and value objects
- **Interfaces**: Repository and service contracts
- **Business Logic**: Domain services and validation

### Infrastructure Layer
- **Data Access**: LiteDB repositories
- **File Operations**: Excel/CSV import/export
- **External Services**: Any third-party integrations

### Shared Layer
- **Common UI Components**: Reusable controls
- **Utilities**: Helpers, extensions, converters
- **Services**: Theme manager, notification service

### Desktop Application
- **Views**: XAML-based UI pages
- **ViewModels**: MVVM pattern with CommunityToolkit
- **Navigation**: Shell and routing
- **Dependency Injection**: Service registration

### Feature Modules
Each feature is self-contained with its own:
- Models and ViewModels
- Services and repositories
- Views and resources

## 🎨 Modern C# Features Used

- **File-scoped namespaces** - Cleaner code organization
- **Records** - Immutable data models
- **Nullable reference types** - Improved null safety
- **Pattern matching** - Enhanced switch expressions
- **Source generators** - MVVM boilerplate reduction
- **Global usings** - Reduced using statements
- **Primary constructors** - Concise initialization

## 🔧 Configuration

Application settings are stored in:
- **Windows**: `%APPDATA%/BusinessToolsSuite/`
- **macOS**: `~/Library/Application Support/BusinessToolsSuite/`
- **Linux**: `~/.config/BusinessToolsSuite/`

## 📊 Data Storage

- **LiteDB** for structured data (inventories, allocations)
- **JSON** for settings and configuration
- **Excel/CSV** for import/export operations

## 🎯 Development Principles

1. **SOLID Principles** - Clean, maintainable code
2. **DRY** - Don't Repeat Yourself
3. **KISS** - Keep It Simple, Stupid
4. **YAGNI** - You Aren't Gonna Need It
5. **Separation of Concerns** - Clear boundaries
6. **Dependency Injection** - Loose coupling
7. **Test-Driven Development** - Quality assurance

## 🔐 Security

- Input validation using FluentValidation
- SQL injection prevention (parameterized queries)
- Secure file operations
- No hardcoded credentials
- Principle of least privilege

## 🌐 Cross-Platform Support

Fully supported on:
- ✅ Windows 10/11
- ✅ macOS 10.15+ (Catalina and later)
- ✅ Linux (Ubuntu 20.04+, Debian, Fedora)

## 📝 License

MIT License - See LICENSE file for details

## 👥 Team

Business Tools Team - 2025

---

**Built with ❤️ using modern .NET and Avalonia UI**

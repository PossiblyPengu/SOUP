# C# Project Workspace

This is a starter C# project workspace.

## Structure
- `src/` - Main application code
- `tests/` - Test project

## Build & Run

1. Make sure you have the .NET SDK installed.
2. To create a new solution and projects, use the .NET CLI:
   - Create a solution: `dotnet new sln -n Cshp`
   - Create a console app: `dotnet new console -o src/CshpApp`
   - Create a test project: `dotnet new xunit -o tests/CshpApp.Tests`
   - Add projects to solution:
     - `dotnet sln add src/CshpApp/CshpApp.csproj`
     - `dotnet sln add tests/CshpApp.Tests/CshpApp.Tests.csproj`
   - Reference main project in test project:
     - `dotnet add tests/CshpApp.Tests/CshpApp.Tests.csproj reference src/CshpApp/CshpApp.csproj`
3. Build the solution: `dotnet build`
4. Run the app: `dotnet run --project src/CshpApp/CshpApp.csproj`
5. Run tests: `dotnet test`

## Requirements
- [.NET SDK](https://dotnet.microsoft.com/download)

---
Replace placeholder names as needed for your project.
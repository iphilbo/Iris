# Project Review - Missing Items and Issues

This project was previously part of a different project (subpathed/submoduled). Based on the migration document from Prometheus, this review identifies what's missing and what needs to be addressed.

## Migration Context

RaiseTracker was extracted from the Prometheus repository where it ran as a sub-application on the `/RaiseTracker` path. The project was designed to work both as:
- **Standalone application** (using `Program.cs`)
- **Sub-application** (using `RaiseTrackerApp.cs` methods)

This dual-mode design is intentional and correct.

## ✅ What's Present (Verified Against Migration Doc)

### Core Application Components ✓
- ✅ **RaiseTrackerApp class** with both required methods:
  - `ConfigureRaiseTracker(WebApplicationBuilder)` - Service configuration
  - `SetupRaiseTrackerPipeline(IApplicationBuilder)` - Pipeline setup
- ✅ **Program.cs** - Standalone application entry point
- ✅ All models, services, and middleware
- ✅ Frontend files (HTML, CSS, JS) in `RaiseTracker.Api/wwwroot/`
- ✅ Documentation in Docs/ folder
- ✅ NuGet packages properly configured
- ✅ Project builds successfully

### Service Configuration ✓
- ✅ `IBlobStorageService` and `BlobStorageService` registered
- ✅ `IAuthService` and `AuthService` registered
- ✅ CORS configuration
- ✅ Static web assets configuration

### Pipeline Setup ✓
- ✅ Middleware pipeline (CORS, Rate Limiting, Session)
- ✅ Routing configuration
- ✅ Static file serving
- ✅ All API endpoints (Auth, Investors, Tasks, Users)

## ❌ Missing Standard Project Files

### 1. **.gitignore**
**Status**: Missing
**Priority**: High
**Action**: Create a standard .NET .gitignore file to exclude:
- `bin/` and `obj/` folders
- User-specific files
- Build artifacts
- IDE-specific files

### 2. **Solution File (.sln)**
**Status**: Missing
**Priority**: Medium
**Action**: Create a Visual Studio solution file to properly organize the project. Currently only the `.csproj` file exists.

### 3. **.editorconfig**
**Status**: Missing
**Priority**: Low
**Action**: Optional but recommended for consistent code formatting across the team.

### 4. **.gitattributes**
**Status**: Missing
**Priority**: Low
**Action**: Optional but recommended for consistent line endings and Git behavior.

## ⚠️ Issues Found

### 1. **Duplicate wwwroot Folders**
**Location**:
- `wwwroot/` (root level)
- `RaiseTracker.Api/wwwroot/` (project level)

**Issue**: There are two wwwroot folders with different content:
- Root `wwwroot/` has `index.html`
- `RaiseTracker.Api/wwwroot/` has `raise-tracker.html`

**Action Required**:
- Determine which is the correct frontend
- Remove the duplicate or consolidate
- Update `Program.cs` to serve the correct file

### 2. **Hardcoded Parent Path References**
**File**: `RaiseTracker.Api/RaiseTrackerApp.cs` (Line 76)

**Issue**: The code references a parent project structure:
```csharp
var devPath = Path.Combine(currentDir, "..", "RaiseTracker", "RaiseTracker.Api", "wwwroot");
```

This suggests the project was expecting to be at:
```
ParentProject/
  RaiseTracker/
    RaiseTracker.Api/
      wwwroot/
```

**Current Structure**:
```
Iris/
  RaiseTracker.Api/
    wwwroot/
```

**Action Required**:
- Update `RaiseTrackerApp.cs` to use correct relative paths for standalone operation
- Or remove the parent path logic if not needed for standalone mode

### 3. **Program.cs vs RaiseTrackerApp.cs** ✅ RESOLVED
**Status**: This is **intentional and correct** per migration document

**Explanation**:
- `Program.cs` - Standalone application entry point (for running independently)
- `RaiseTrackerApp.cs` - Sub-application configuration (for embedding in another app like Prometheus)

**Action**: No action needed - both modes are supported by design. The project can run standalone OR be integrated into another application.

### 4. **Default File Configuration**
**Issue**:
- `Program.cs` uses `UseDefaultFiles()` which serves `index.html` by default
- `RaiseTracker.Api/wwwroot/` contains `raise-tracker.html` (not `index.html`)
- Root `wwwroot/` contains `index.html`

**Action Required**:
- Align default file names
- Update `Program.cs` or `RaiseTrackerApp.cs` to specify the correct default file

## 📋 Recommended Actions

### ✅ Completed
1. ✅ **Created `.gitignore` file** - Standard .NET gitignore added
2. ✅ **Created solution file (`Iris.sln`)** - Solution file created and project added
3. ✅ **Fixed hardcoded parent path in `RaiseTrackerApp.cs`** - Updated to work in standalone mode
4. ✅ **Updated default file configuration in `Program.cs`** - Now correctly serves `raise-tracker.html`

### ✅ Additional Fixes Completed
5. ✅ **Added missing user management endpoints to `Program.cs`** - POST, PUT, DELETE for `/api/users` (admin-only)
6. ✅ **Updated GET `/api/users` endpoint** - Now returns full details for admins, summaries for others (matching `RaiseTrackerApp.cs`)

### ⚠️ Still Needs Attention

#### 1. Duplicate wwwroot Folders
**Status**: Needs decision
**Issue**:
- Root `wwwroot/` contains `index.html` (simpler version, 109 lines)
- `RaiseTracker.Api/wwwroot/` contains `raise-tracker.html` (complete version, 210 lines with user management)

**Recommendation**:
- The `RaiseTracker.Api/wwwroot/` version appears to be the correct/complete one
- Consider removing the root `wwwroot/` folder if it's legacy
- Or document which one should be used

#### 2. Test Standalone Mode
**Status**: Should be tested
**Action**: Run the application and verify it works correctly in standalone mode

### Optional (Low Priority)
- Add `.editorconfig` for code formatting consistency
- Add `.gitattributes` for line ending consistency
- Add deployment configuration files (if needed)

## 🔍 Additional Notes

- The project builds successfully, so all dependencies are present
- All NuGet packages are properly referenced
- The code structure is complete and functional
- The main issues are related to project configuration and file organization from the subpath migration

## 📝 Next Steps

1. ✅ Review this document
2. ✅ Fix the hardcoded paths in `RaiseTrackerApp.cs` (COMPLETED)
3. ✅ Create the missing standard project files (COMPLETED)
4. ⚠️ **Decide on the wwwroot folder structure** - Remove root `wwwroot/` if it's legacy
5. ⚠️ **Test the application in standalone mode** - Verify it runs correctly
6. ⚠️ **Update CORS origin** - Change placeholder `"https://your-app-service.azurewebsites.net"` to actual production URL when deploying

## Migration Checklist Status (from Migration Doc)

Based on the migration document checklist:

- [x] **Project Structure** - Complete
- [x] **Service Configuration** - Complete (`ConfigureRaiseTracker` has all services)
- [x] **Pipeline Setup** - Complete (`SetupRaiseTrackerPipeline` has full pipeline)
- [x] **Authentication** - Complete (standalone auth mechanism)
- [x] **Static Assets** - Present in `RaiseTracker.Api/wwwroot/`
- [x] **Configuration** - `appsettings.json` present with connection strings
- [ ] **Deployment** - May need deployment scripts/config (not in scope of this review)

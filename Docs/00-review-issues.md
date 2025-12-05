# Documentation Review - Issues and Gaps

## Issues Found

### 1. Missing API Endpoint
- **Issue**: Frontend mentions `/api/session` as an optional endpoint, but it's not defined in the API specification.
- **Status**: Intentional - the original spec says "via an /api/session check **or** by attempting GET /api/investors"
- **Action**: Clarify in API spec that `/api/session` is optional

### 2. Missing API Response Details
- **Issue**: API endpoints don't specify response formats, status codes, or error response structures
- **Action**: Add response format details to API spec

### 3. Missing Request Body Validation
- **Issue**: No specification for required vs optional fields in POST/PUT requests
- **Action**: Add validation requirements

### 4. Missing Error Response Format
- **Issue**: No standard error response format specified
- **Action**: Define error response structure

## Questions Before Building

1. **Technology Stack**:
   - Backend framework confirmed (.NET 8 minimal API mentioned, but framework-agnostic)
   - Frontend: Existing HTML/JS page - do we have access to it?
   - Azure credentials: Do we have Azure Storage account details?

2. **Initial Data**:
   - Do we need to create initial `users.json` with 7 users?
   - Any seed data for investors?

3. **Session Implementation**:
   - JWT vs opaque token preference?
   - Session storage (in-memory vs persistent)?

4. **Deployment**:
   - Azure App Service configuration details?
   - Environment variables/secrets management?

5. **Frontend Integration**:
   - Do we have the existing "vibe-coded" page to refactor?
   - Or are we building from scratch?

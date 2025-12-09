# CSV Import Field Mapping

## CSV Columns
1. **Investor Type** - "Strategic" or "Financial"
2. **Company** - Company name
3. **IntraLogic Call Responsibility** - Person responsible (may be empty)
4. **Primary Investor Type** - e.g., "Corporate Venture Capital", "Venture Capital", "Corporation", etc.
5. **Preferred Investment Types** - Comma-separated list, e.g., "Early Stage VC, Later Stage VC, Seed Round"
6. **Primary Contact** - Contact person name
7. **Primary Contact Phone** - Phone number
8. **Primary Contact Email** - Email address

## Field Mapping

| CSV Column | Investor Model Field | Mapping Logic | Notes |
|------------|---------------------|---------------|-------|
| Company | `Name` | Direct mapping | ✅ Straightforward |
| Primary Contact | `MainContact` | Direct mapping | ✅ Straightforward |
| Primary Contact Email | `ContactEmail` | Direct mapping | ✅ Straightforward |
| Primary Contact Phone | `ContactPhone` | Direct mapping | ✅ Straightforward |
| IntraLogic Call Responsibility | `Owner` | Direct mapping (if not empty) | ✅ Straightforward - may be empty |
| Investor Type + Primary Investor Type + Preferred Investment Types | `Notes` | Formatted text | ⚠️ See formatting below |
| Investor Type | `Category` | **MAPPING NEEDED** | ❌ **ISSUE #1** |
| (none) | `Stage` | Default value | ❌ **ISSUE #2** |
| (none) | `Status` | Default value | ❌ **ISSUE #3** |
| (none) | `CommitAmount` | null | ✅ No issue - will be null |

## Issues Identified

### Issue #1: Category Mapping Mismatch
**Problem:**
- CSV has: "Strategic" or "Financial"
- Model expects: "existing", "known", or "new"

**Recommendations:**
1. **Option A (Recommended):** Map CSV values directly
   - "Strategic" → "Strategic" (add to model)
   - "Financial" → "Financial" (add to model)
   - Update frontend dropdowns to include these values

2. **Option B:** Map to existing values
   - "Strategic" → "existing" (assume strategic investors are existing relationships)
   - "Financial" → "known" (assume financial investors are known contacts)

3. **Option C:** Add new category values
   - Keep existing: "existing", "known", "new"
   - Add: "strategic", "financial"
   - Map CSV: "Strategic" → "strategic", "Financial" → "financial"

**Decision Needed:** Which approach should we use?

### Issue #2: Stage Field Missing
**Problem:** CSV doesn't have a stage field, but it's required in the model.

**Recommendation:**
- Default all imported investors to `"target"` (first stage in the pipeline)
- Users can manually update stages after import

**Default Value:** `"target"`

### Issue #3: Status Field Missing
**Problem:** CSV doesn't have a status field, but it's required in the model.

**Recommendation:**
- Default all imported investors to `"Interested: Move to NDA"` (first status option)
- Users can manually update status after import

**Default Value:** `"Interested: Move to NDA"`

## Notes Field Formatting

Format the Notes field as follows:

```
Investor Type: [Investor Type value]
Primary Investor Type: [Primary Investor Type value]

Preferred Investment Types:
[Preferred Investment Types - one per line or comma-separated]
```

**Example Output:**
```
Investor Type: Strategic
Primary Investor Type: Corporate Venture Capital

Preferred Investment Types: Early Stage VC, Later Stage VC, Seed Round
```

**Alternative (more compact):**
```
Investor Type: Strategic
Primary Investor Type: Corporate Venture Capital
Preferred Investment Types: Early Stage VC, Later Stage VC, Seed Round
```

## Data Quality Issues to Watch For

1. **Empty rows:** Row 2 appears to be empty - skip during import
2. **Missing contact info:** Many rows have empty Primary Contact, Phone, or Email - this is OK
3. **Missing Owner:** Many rows have empty "IntraLogic Call Responsibility" - this is OK
4. **Special characters in company names:** Some have quotes (e.g., "Becton, Dickinson and Company") - handle CSV parsing correctly
5. **Phone number formats:** Various formats (+1, country codes, etc.) - store as-is
6. **Email validation:** Some may be invalid - store as-is, validate later if needed

## Import Script Requirements

1. Skip empty rows
2. Handle CSV parsing correctly (quoted fields, commas within quotes)
3. Generate unique IDs for each investor (GUID)
4. Set audit fields:
   - `CreatedBy`: "CSV Import" or system user
   - `CreatedAt`: Current UTC timestamp
   - `UpdatedBy`: "CSV Import" or system user
   - `UpdatedAt`: Current UTC timestamp
5. Handle null/empty values appropriately
6. Format Notes field as specified above

## Decisions Made

1. **CreatedBy:** Set to user "JP" (looks up user ID from database, falls back to "JP" string if not found)
2. **Duplicate handling:** Create duplicates (always insert, don't check for existing)
3. **Category, Stage, Status:** User is working on these mappings - script uses placeholders:
   - Category: Uses CSV "Investor Type" value directly (needs mapping)
   - Stage: Defaults to "target" (needs mapping)
   - Status: Defaults to "Interested: Move to NDA" (needs mapping)

## Import Script

The import script `ImportInvestorsFromCsv.cs` has been created in the `Scripts` folder.

**Usage:**
```bash
cd Scripts
dotnet run --project ImportInvestorsFromCsv.csproj [path-to-csv-file]
```

If no path is provided, it defaults to `../Investor Contact List.csv`

**Features:**
- Handles CSV parsing correctly (quoted fields, commas within quotes)
- Skips empty rows
- Formats Notes field with Investor Type, Primary Investor Type, and Preferred Investment Types
- Sets CreatedBy to JP user
- Creates duplicates (always inserts, doesn't check for existing)
- Generates unique GUIDs for each investor
- Sets audit timestamps

-- Add Owner column to Investors table
-- Run this script to add the Owner column to the existing Investors table

IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[Investors]')
    AND name = 'Owner'
)
BEGIN
    ALTER TABLE [dbo].[Investors]
    ADD [Owner] NVARCHAR(256) NULL;

    PRINT 'Owner column added successfully to Investors table';
END
ELSE
BEGIN
    PRINT 'Owner column already exists in Investors table';
END

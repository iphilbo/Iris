-- Add Status column to existing Investors table
-- Run this script if the database already exists

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Investors') AND name = 'Status')
BEGIN
    ALTER TABLE Investors
    ADD Status NVARCHAR(50) NOT NULL DEFAULT 'Active';

    PRINT 'Status column added to Investors table';
END
ELSE
BEGIN
    PRINT 'Status column already exists';
END

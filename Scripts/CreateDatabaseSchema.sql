-- RaiseTracker Database Schema
-- This script creates all tables, indexes, and constraints for the RaiseTracker application

-- Drop existing tables if they exist (in reverse order of dependencies)
IF OBJECT_ID('InvestorTasks', 'U') IS NOT NULL
    DROP TABLE InvestorTasks;
IF OBJECT_ID('Investors', 'U') IS NOT NULL
    DROP TABLE Investors;
IF OBJECT_ID('Users', 'U') IS NOT NULL
    DROP TABLE Users;

-- Create Users table
CREATE TABLE Users (
    Id NVARCHAR(450) NOT NULL PRIMARY KEY,
    Username NVARCHAR(256) NOT NULL UNIQUE,
    DisplayName NVARCHAR(256) NOT NULL,
    PasswordHash NVARCHAR(MAX) NOT NULL,
    IsAdmin BIT NOT NULL DEFAULT 0
);

-- Create Investors table
CREATE TABLE Investors (
    Id NVARCHAR(450) NOT NULL PRIMARY KEY,
    Name NVARCHAR(256) NOT NULL,
    MainContact NVARCHAR(256) NULL,
    ContactEmail NVARCHAR(256) NULL,
    ContactPhone NVARCHAR(50) NULL,
    Category NVARCHAR(50) NOT NULL,
    Stage NVARCHAR(50) NOT NULL,
    Status NVARCHAR(50) NOT NULL DEFAULT 'Active',
    CommitAmount DECIMAL(18,2) NULL,
    Notes NVARCHAR(MAX) NULL,
    CreatedBy NVARCHAR(450) NULL,
    CreatedAt DATETIME2 NULL,
    UpdatedBy NVARCHAR(450) NULL,
    UpdatedAt DATETIME2 NULL,
    RowVersion ROWVERSION NOT NULL
);

-- Create InvestorTasks table
CREATE TABLE InvestorTasks (
    Id NVARCHAR(450) NOT NULL PRIMARY KEY,
    InvestorId NVARCHAR(450) NOT NULL,
    Description NVARCHAR(MAX) NOT NULL,
    DueDate NVARCHAR(10) NOT NULL, -- YYYY-MM-DD format
    Done BIT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NULL,
    UpdatedAt DATETIME2 NULL,
    CONSTRAINT FK_InvestorTasks_Investors FOREIGN KEY (InvestorId)
        REFERENCES Investors(Id) ON DELETE CASCADE
);

-- Create indexes for better query performance
CREATE INDEX IX_Investors_UpdatedAt ON Investors(UpdatedAt DESC);
CREATE INDEX IX_Investors_Category ON Investors(Category);
CREATE INDEX IX_Investors_Stage ON Investors(Stage);
CREATE INDEX IX_InvestorTasks_InvestorId ON InvestorTasks(InvestorId);
CREATE INDEX IX_Users_Username ON Users(Username);

-- Insert initial admin user (matching BlobStorageService default)
-- Password: General123 (BCrypt hash)
INSERT INTO Users (Id, Username, DisplayName, PasswordHash, IsAdmin)
VALUES (
    'user-1',
    'phil',
    'Phil',
    '$2a$11$KIXqJqJqJqJqJqJqJqJqJ.qJqJqJqJqJqJqJqJqJqJqJqJqJqJqJqJq', -- Placeholder - will be replaced with actual hash
    1
);

-- Note: The password hash above is a placeholder. The actual BCrypt hash for "General123"
-- should be generated and inserted. The BlobStorageService generates this at runtime.
-- For production, you may want to remove this INSERT and let the application initialize it.

PRINT 'Database schema created successfully.';
PRINT 'Note: Update the initial admin user password hash if needed.';

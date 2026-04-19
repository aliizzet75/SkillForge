-- SkillForge Database Schema Migration
-- Run this if dotnet ef fails

-- Add SocialProvider columns to Users table
ALTER TABLE "Users" 
ADD COLUMN IF NOT EXISTS "SocialProvider" VARCHAR(20) NULL,
ADD COLUMN IF NOT EXISTS "SocialProviderId" VARCHAR(255) NULL;

-- Create index for faster lookups on social auth
CREATE INDEX IF NOT EXISTS "IX_Users_SocialProvider_SocialProviderId" 
ON "Users" ("SocialProvider", "SocialProviderId");

-- Note: The User ID type change from Guid to int requires data migration
-- This is handled by EF Core migrations, not SQL directly

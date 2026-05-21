IF DB_ID(N'KnowledgeVault') IS NULL
BEGIN
    CREATE DATABASE [KnowledgeVault];
END;
GO

USE [KnowledgeVault];
GO

IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260521113312_InitialCreate'
)
BEGIN
    CREATE TABLE [Users] (
        [Id] uniqueidentifier NOT NULL,
        [UserName] nvarchar(64) NOT NULL,
        [NormalizedUserName] nvarchar(64) NOT NULL,
        [Email] nvarchar(256) NOT NULL,
        [NormalizedEmail] nvarchar(256) NOT NULL,
        [PasswordHash] nvarchar(256) NOT NULL,
        [PasswordSalt] nvarchar(128) NOT NULL,
        [LastLoginAt] datetimeoffset NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260521113312_InitialCreate'
)
BEGIN
    CREATE TABLE [Categories] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Name] nvarchar(128) NOT NULL,
        [NormalizedName] nvarchar(128) NOT NULL,
        [Description] nvarchar(512) NULL,
        [Color] nvarchar(32) NULL,
        [SortOrder] int NOT NULL,
        [IsArchived] bit NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        CONSTRAINT [PK_Categories] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Categories_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260521113312_InitialCreate'
)
BEGIN
    CREATE TABLE [Tags] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Name] nvarchar(64) NOT NULL,
        [NormalizedName] nvarchar(64) NOT NULL,
        [Color] nvarchar(32) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        CONSTRAINT [PK_Tags] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Tags_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260521113312_InitialCreate'
)
BEGIN
    CREATE TABLE [KnowledgeItems] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [CategoryId] uniqueidentifier NULL,
        [Title] nvarchar(256) NOT NULL,
        [Content] nvarchar(max) NOT NULL,
        [Summary] nvarchar(1024) NULL,
        [SourceUrl] nvarchar(2048) NULL,
        [Status] int NOT NULL,
        [PublishedAt] datetimeoffset NULL,
        [ArchivedAt] datetimeoffset NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        CONSTRAINT [PK_KnowledgeItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_KnowledgeItems_Categories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [Categories] ([Id]),
        CONSTRAINT [FK_KnowledgeItems_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260521113312_InitialCreate'
)
BEGIN
    CREATE TABLE [KnowledgeItemTags] (
        [KnowledgeItemId] uniqueidentifier NOT NULL,
        [TagId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_KnowledgeItemTags] PRIMARY KEY ([KnowledgeItemId], [TagId]),
        CONSTRAINT [FK_KnowledgeItemTags_KnowledgeItems_KnowledgeItemId] FOREIGN KEY ([KnowledgeItemId]) REFERENCES [KnowledgeItems] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_KnowledgeItemTags_Tags_TagId] FOREIGN KEY ([TagId]) REFERENCES [Tags] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260521113312_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Categories_UserId_NormalizedName] ON [Categories] ([UserId], [NormalizedName]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260521113312_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_KnowledgeItems_CategoryId] ON [KnowledgeItems] ([CategoryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260521113312_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_KnowledgeItems_UserId_CategoryId] ON [KnowledgeItems] ([UserId], [CategoryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260521113312_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_KnowledgeItems_UserId_Status] ON [KnowledgeItems] ([UserId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260521113312_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_KnowledgeItemTags_TagId] ON [KnowledgeItemTags] ([TagId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260521113312_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Tags_UserId_NormalizedName] ON [Tags] ([UserId], [NormalizedName]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260521113312_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Users_NormalizedEmail] ON [Users] ([NormalizedEmail]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260521113312_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Users_NormalizedUserName] ON [Users] ([NormalizedUserName]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260521113312_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260521113312_InitialCreate', N'10.0.8');
END;

COMMIT;
GO


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
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetRoles] (
        [Id] nvarchar(450) NOT NULL,
        [Name] nvarchar(256) NULL,
        [NormalizedName] nvarchar(256) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUsers] (
        [Id] nvarchar(450) NOT NULL,
        [FullName] nvarchar(150) NOT NULL,
        [ProfileImagePath] nvarchar(300) NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [LastLoginAt] datetime2 NULL,
        [UserName] nvarchar(256) NULL,
        [NormalizedUserName] nvarchar(256) NULL,
        [Email] nvarchar(256) NULL,
        [NormalizedEmail] nvarchar(256) NULL,
        [EmailConfirmed] bit NOT NULL,
        [PasswordHash] nvarchar(max) NULL,
        [SecurityStamp] nvarchar(max) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        [PhoneNumber] nvarchar(max) NULL,
        [PhoneNumberConfirmed] bit NOT NULL,
        [TwoFactorEnabled] bit NOT NULL,
        [LockoutEnd] datetimeoffset NULL,
        [LockoutEnabled] bit NOT NULL,
        [AccessFailedCount] int NOT NULL,
        CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE TABLE [AuditLogs] (
        [Id] bigint NOT NULL IDENTITY,
        [UserId] nvarchar(450) NULL,
        [UserName] nvarchar(256) NULL,
        [Action] nvarchar(100) NOT NULL,
        [EntityName] nvarchar(100) NOT NULL,
        [EntityId] nvarchar(50) NULL,
        [Details] nvarchar(max) NULL,
        [IpAddress] nvarchar(64) NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_AuditLogs] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE TABLE [Governorates] (
        [Id] int NOT NULL IDENTITY,
        [NameAr] nvarchar(100) NOT NULL,
        [NameEn] nvarchar(100) NOT NULL,
        [SortOrder] int NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Governorates] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE TABLE [Seasons] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(30) NOT NULL,
        [StartDate] date NOT NULL,
        [EndDate] date NOT NULL,
        [IsCurrent] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Seasons] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Seasons_Dates] CHECK ([EndDate] > [StartDate])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE TABLE [Sports] (
        [Id] int NOT NULL IDENTITY,
        [NameAr] nvarchar(80) NOT NULL,
        [NameEn] nvarchar(80) NOT NULL,
        [Code] nvarchar(30) NOT NULL,
        [IconClass] nvarchar(60) NULL,
        [PlayersOnField] int NOT NULL,
        [PeriodsCount] int NOT NULL,
        [PeriodDurationMinutes] int NOT NULL,
        [PointsForWin] int NOT NULL,
        [PointsForDraw] int NOT NULL,
        [PointsForLoss] int NOT NULL,
        [IsActive] bit NOT NULL,
        [SortOrder] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Sports] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetRoleClaims] (
        [Id] int NOT NULL IDENTITY,
        [RoleId] nvarchar(450) NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUserClaims] (
        [Id] int NOT NULL IDENTITY,
        [UserId] nvarchar(450) NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUserLogins] (
        [LoginProvider] nvarchar(450) NOT NULL,
        [ProviderKey] nvarchar(450) NOT NULL,
        [ProviderDisplayName] nvarchar(max) NULL,
        [UserId] nvarchar(450) NOT NULL,
        CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
        CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUserRoles] (
        [UserId] nvarchar(450) NOT NULL,
        [RoleId] nvarchar(450) NOT NULL,
        CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
        CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUserTokens] (
        [UserId] nvarchar(450) NOT NULL,
        [LoginProvider] nvarchar(450) NOT NULL,
        [Name] nvarchar(450) NOT NULL,
        [Value] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
        CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE TABLE [PushSubscriptions] (
        [Id] int NOT NULL IDENTITY,
        [UserId] nvarchar(450) NOT NULL,
        [Endpoint] nvarchar(800) NOT NULL,
        [P256dh] nvarchar(200) NOT NULL,
        [Auth] nvarchar(200) NOT NULL,
        [UserAgent] nvarchar(300) NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_PushSubscriptions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PushSubscriptions_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE TABLE [Wilayats] (
        [Id] int NOT NULL IDENTITY,
        [GovernorateId] int NOT NULL,
        [NameAr] nvarchar(100) NOT NULL,
        [NameEn] nvarchar(100) NOT NULL,
        [SortOrder] int NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Wilayats] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Wilayats_Governorates_GovernorateId] FOREIGN KEY ([GovernorateId]) REFERENCES [Governorates] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE TABLE [SportPositions] (
        [Id] int NOT NULL IDENTITY,
        [SportId] int NOT NULL,
        [NameAr] nvarchar(60) NOT NULL,
        [NameEn] nvarchar(60) NOT NULL,
        [Code] nvarchar(10) NOT NULL,
        [SortOrder] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_SportPositions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SportPositions_Sports_SportId] FOREIGN KEY ([SportId]) REFERENCES [Sports] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE TABLE [Areas] (
        [Id] int NOT NULL IDENTITY,
        [WilayatId] int NOT NULL,
        [NameAr] nvarchar(100) NOT NULL,
        [NameEn] nvarchar(100) NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Areas] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Areas_Wilayats_WilayatId] FOREIGN KEY ([WilayatId]) REFERENCES [Wilayats] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE TABLE [Players] (
        [Id] int NOT NULL IDENTITY,
        [SportId] int NOT NULL,
        [FullNameAr] nvarchar(150) NOT NULL,
        [FullNameEn] nvarchar(150) NULL,
        [PhotoPath] nvarchar(300) NULL,
        [DateOfBirth] date NULL,
        [Nationality] nvarchar(60) NULL,
        [PositionId] int NULL,
        [Bio] nvarchar(1000) NULL,
        [UserId] nvarchar(450) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Players] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Players_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Players_SportPositions_PositionId] FOREIGN KEY ([PositionId]) REFERENCES [SportPositions] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Players_Sports_SportId] FOREIGN KEY ([SportId]) REFERENCES [Sports] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE TABLE [Competitions] (
        [Id] int NOT NULL IDENTITY,
        [SportId] int NOT NULL,
        [NameAr] nvarchar(150) NOT NULL,
        [NameEn] nvarchar(150) NULL,
        [Slug] nvarchar(170) NOT NULL,
        [LogoPath] nvarchar(300) NULL,
        [Description] nvarchar(2000) NULL,
        [OrganizerName] nvarchar(150) NULL,
        [GovernorateId] int NULL,
        [WilayatId] int NULL,
        [AreaId] int NULL,
        [IsActive] bit NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Competitions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Competitions_Areas_AreaId] FOREIGN KEY ([AreaId]) REFERENCES [Areas] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Competitions_Governorates_GovernorateId] FOREIGN KEY ([GovernorateId]) REFERENCES [Governorates] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Competitions_Sports_SportId] FOREIGN KEY ([SportId]) REFERENCES [Sports] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Competitions_Wilayats_WilayatId] FOREIGN KEY ([WilayatId]) REFERENCES [Wilayats] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE TABLE [Venues] (
        [Id] int NOT NULL IDENTITY,
        [NameAr] nvarchar(150) NOT NULL,
        [NameEn] nvarchar(150) NULL,
        [AreaId] int NULL,
        [Address] nvarchar(300) NULL,
        [Capacity] int NULL,
        [Latitude] decimal(9,6) NULL,
        [Longitude] decimal(9,6) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Venues] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Venues_Areas_AreaId] FOREIGN KEY ([AreaId]) REFERENCES [Areas] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE TABLE [Villages] (
        [Id] int NOT NULL IDENTITY,
        [AreaId] int NOT NULL,
        [NameAr] nvarchar(100) NOT NULL,
        [NameEn] nvarchar(100) NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Villages] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Villages_Areas_AreaId] FOREIGN KEY ([AreaId]) REFERENCES [Areas] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE TABLE [CompetitionManagers] (
        [CompetitionId] int NOT NULL,
        [UserId] nvarchar(450) NOT NULL,
        [AssignedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_CompetitionManagers] PRIMARY KEY ([CompetitionId], [UserId]),
        CONSTRAINT [FK_CompetitionManagers_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CompetitionManagers_Competitions_CompetitionId] FOREIGN KEY ([CompetitionId]) REFERENCES [Competitions] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE TABLE [Teams] (
        [Id] int NOT NULL IDENTITY,
        [SportId] int NOT NULL,
        [NameAr] nvarchar(150) NOT NULL,
        [NameEn] nvarchar(150) NOT NULL,
        [Slug] nvarchar(170) NOT NULL,
        [ShortName] nvarchar(30) NULL,
        [LogoPath] nvarchar(300) NULL,
        [FoundedYear] int NULL,
        [Description] nvarchar(2000) NULL,
        [ContactPhone] nvarchar(30) NULL,
        [ContactEmail] nvarchar(150) NULL,
        [SocialUrl] nvarchar(300) NULL,
        [PrimaryColor] nvarchar(9) NULL,
        [Status] int NOT NULL,
        [ApprovedAt] datetime2 NULL,
        [VillageId] int NULL,
        [HomeVenueId] int NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Teams] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Teams_FoundedYear] CHECK ([FoundedYear] IS NULL OR [FoundedYear] BETWEEN 1900 AND 2100),
        CONSTRAINT [FK_Teams_Sports_SportId] FOREIGN KEY ([SportId]) REFERENCES [Sports] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Teams_Venues_HomeVenueId] FOREIGN KEY ([HomeVenueId]) REFERENCES [Venues] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Teams_Villages_VillageId] FOREIGN KEY ([VillageId]) REFERENCES [Villages] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE TABLE [UserAreas] (
        [Id] int NOT NULL IDENTITY,
        [UserId] nvarchar(450) NOT NULL,
        [GovernorateId] int NOT NULL,
        [WilayatId] int NULL,
        [AreaId] int NULL,
        [VillageId] int NULL,
        [IsDefault] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_UserAreas] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UserAreas_Areas_AreaId] FOREIGN KEY ([AreaId]) REFERENCES [Areas] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_UserAreas_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_UserAreas_Governorates_GovernorateId] FOREIGN KEY ([GovernorateId]) REFERENCES [Governorates] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_UserAreas_Villages_VillageId] FOREIGN KEY ([VillageId]) REFERENCES [Villages] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_UserAreas_Wilayats_WilayatId] FOREIGN KEY ([WilayatId]) REFERENCES [Wilayats] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE TABLE [CompetitionSeasons] (
        [Id] int NOT NULL IDENTITY,
        [CompetitionId] int NOT NULL,
        [SeasonId] int NOT NULL,
        [Format] int NOT NULL,
        [Status] int NOT NULL,
        [StartDate] date NULL,
        [EndDate] date NULL,
        [PointsForWin] int NOT NULL,
        [PointsForDraw] int NOT NULL,
        [PointsForLoss] int NOT NULL,
        [TieBreakers] nvarchar(300) NOT NULL,
        [Regulations] nvarchar(4000) NULL,
        [ChampionTeamId] int NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_CompetitionSeasons] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_CompetitionSeasons_Dates] CHECK ([EndDate] IS NULL OR [StartDate] IS NULL OR [EndDate] >= [StartDate]),
        CONSTRAINT [CK_CompetitionSeasons_Points] CHECK ([PointsForWin] >= [PointsForDraw] AND [PointsForDraw] >= [PointsForLoss]),
        CONSTRAINT [FK_CompetitionSeasons_Competitions_CompetitionId] FOREIGN KEY ([CompetitionId]) REFERENCES [Competitions] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CompetitionSeasons_Seasons_SeasonId] FOREIGN KEY ([SeasonId]) REFERENCES [Seasons] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CompetitionSeasons_Teams_ChampionTeamId] FOREIGN KEY ([ChampionTeamId]) REFERENCES [Teams] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE TABLE [PlayerTransfers] (
        [Id] int NOT NULL IDENTITY,
        [PlayerId] int NOT NULL,
        [FromTeamId] int NULL,
        [ToTeamId] int NULL,
        [TransferDate] date NOT NULL,
        [Notes] nvarchar(500) NULL,
        [RecordedByUserId] nvarchar(450) NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_PlayerTransfers] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PlayerTransfers_AspNetUsers_RecordedByUserId] FOREIGN KEY ([RecordedByUserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PlayerTransfers_Players_PlayerId] FOREIGN KEY ([PlayerId]) REFERENCES [Players] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PlayerTransfers_Teams_FromTeamId] FOREIGN KEY ([FromTeamId]) REFERENCES [Teams] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PlayerTransfers_Teams_ToTeamId] FOREIGN KEY ([ToTeamId]) REFERENCES [Teams] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE TABLE [TeamAchievements] (
        [Id] int NOT NULL IDENTITY,
        [TeamId] int NOT NULL,
        [TitleAr] nvarchar(200) NOT NULL,
        [Year] int NOT NULL,
        [CompetitionId] int NULL,
        [Description] nvarchar(1000) NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_TeamAchievements] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TeamAchievements_Competitions_CompetitionId] FOREIGN KEY ([CompetitionId]) REFERENCES [Competitions] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TeamAchievements_Teams_TeamId] FOREIGN KEY ([TeamId]) REFERENCES [Teams] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE TABLE [TeamApplications] (
        [Id] int NOT NULL IDENTITY,
        [ApplicantUserId] nvarchar(450) NOT NULL,
        [SportId] int NOT NULL,
        [NameAr] nvarchar(150) NOT NULL,
        [NameEn] nvarchar(150) NOT NULL,
        [LogoPath] nvarchar(300) NULL,
        [GovernorateId] int NOT NULL,
        [WilayatId] int NOT NULL,
        [AreaId] int NOT NULL,
        [VillageId] int NULL,
        [FoundedYear] int NULL,
        [Description] nvarchar(2000) NULL,
        [ContactPhone] nvarchar(30) NOT NULL,
        [ContactEmail] nvarchar(150) NULL,
        [Status] int NOT NULL,
        [SubmittedAt] datetime2 NOT NULL,
        [ReviewedByUserId] nvarchar(450) NULL,
        [ReviewedAt] datetime2 NULL,
        [ReviewNotes] nvarchar(1000) NULL,
        [CreatedTeamId] int NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_TeamApplications] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_TeamApplications_FoundedYear] CHECK ([FoundedYear] IS NULL OR [FoundedYear] BETWEEN 1900 AND 2100),
        CONSTRAINT [FK_TeamApplications_Areas_AreaId] FOREIGN KEY ([AreaId]) REFERENCES [Areas] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TeamApplications_AspNetUsers_ApplicantUserId] FOREIGN KEY ([ApplicantUserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TeamApplications_AspNetUsers_ReviewedByUserId] FOREIGN KEY ([ReviewedByUserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TeamApplications_Governorates_GovernorateId] FOREIGN KEY ([GovernorateId]) REFERENCES [Governorates] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TeamApplications_Sports_SportId] FOREIGN KEY ([SportId]) REFERENCES [Sports] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TeamApplications_Teams_CreatedTeamId] FOREIGN KEY ([CreatedTeamId]) REFERENCES [Teams] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TeamApplications_Villages_VillageId] FOREIGN KEY ([VillageId]) REFERENCES [Villages] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TeamApplications_Wilayats_WilayatId] FOREIGN KEY ([WilayatId]) REFERENCES [Wilayats] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE TABLE [TeamAreas] (
        [TeamId] int NOT NULL,
        [AreaId] int NOT NULL,
        [IsPrimary] bit NOT NULL,
        CONSTRAINT [PK_TeamAreas] PRIMARY KEY ([TeamId], [AreaId]),
        CONSTRAINT [FK_TeamAreas_Areas_AreaId] FOREIGN KEY ([AreaId]) REFERENCES [Areas] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TeamAreas_Teams_TeamId] FOREIGN KEY ([TeamId]) REFERENCES [Teams] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE TABLE [TeamMembers] (
        [Id] int NOT NULL IDENTITY,
        [TeamId] int NOT NULL,
        [UserId] nvarchar(450) NOT NULL,
        [Role] int NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_TeamMembers] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TeamMembers_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TeamMembers_Teams_TeamId] FOREIGN KEY ([TeamId]) REFERENCES [Teams] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE TABLE [TeamPlayers] (
        [Id] int NOT NULL IDENTITY,
        [TeamId] int NOT NULL,
        [PlayerId] int NOT NULL,
        [ShirtNumber] int NULL,
        [PositionId] int NULL,
        [JoinedOn] date NOT NULL,
        [LeftOn] date NULL,
        [IsActive] bit NOT NULL,
        [IsCaptain] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_TeamPlayers] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_TeamPlayers_Dates] CHECK ([LeftOn] IS NULL OR [LeftOn] >= [JoinedOn]),
        CONSTRAINT [CK_TeamPlayers_ShirtNumber] CHECK ([ShirtNumber] IS NULL OR [ShirtNumber] BETWEEN 1 AND 99),
        CONSTRAINT [FK_TeamPlayers_Players_PlayerId] FOREIGN KEY ([PlayerId]) REFERENCES [Players] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TeamPlayers_SportPositions_PositionId] FOREIGN KEY ([PositionId]) REFERENCES [SportPositions] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TeamPlayers_Teams_TeamId] FOREIGN KEY ([TeamId]) REFERENCES [Teams] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE TABLE [CompetitionStages] (
        [Id] int NOT NULL IDENTITY,
        [CompetitionSeasonId] int NOT NULL,
        [NameAr] nvarchar(100) NOT NULL,
        [StageType] int NOT NULL,
        [SortOrder] int NOT NULL,
        [Legs] int NOT NULL,
        [QualifiersPerGroup] int NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_CompetitionStages] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_CompetitionStages_Legs] CHECK ([Legs] IN (1, 2)),
        CONSTRAINT [FK_CompetitionStages_CompetitionSeasons_CompetitionSeasonId] FOREIGN KEY ([CompetitionSeasonId]) REFERENCES [CompetitionSeasons] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE TABLE [PlayerStatistics] (
        [Id] int NOT NULL IDENTITY,
        [PlayerId] int NOT NULL,
        [CompetitionSeasonId] int NOT NULL,
        [TeamId] int NOT NULL,
        [Appearances] int NOT NULL,
        [Starts] int NOT NULL,
        [Goals] int NOT NULL,
        [PenaltyGoals] int NOT NULL,
        [Assists] int NOT NULL,
        [OwnGoals] int NOT NULL,
        [YellowCards] int NOT NULL,
        [RedCards] int NOT NULL,
        [BestPlayerAwards] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_PlayerStatistics] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PlayerStatistics_CompetitionSeasons_CompetitionSeasonId] FOREIGN KEY ([CompetitionSeasonId]) REFERENCES [CompetitionSeasons] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PlayerStatistics_Players_PlayerId] FOREIGN KEY ([PlayerId]) REFERENCES [Players] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PlayerStatistics_Teams_TeamId] FOREIGN KEY ([TeamId]) REFERENCES [Teams] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE TABLE [TeamStatistics] (
        [Id] int NOT NULL IDENTITY,
        [TeamId] int NOT NULL,
        [CompetitionSeasonId] int NOT NULL,
        [Played] int NOT NULL,
        [Won] int NOT NULL,
        [Drawn] int NOT NULL,
        [Lost] int NOT NULL,
        [GoalsFor] int NOT NULL,
        [GoalsAgainst] int NOT NULL,
        [CleanSheets] int NOT NULL,
        [YellowCards] int NOT NULL,
        [RedCards] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_TeamStatistics] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TeamStatistics_CompetitionSeasons_CompetitionSeasonId] FOREIGN KEY ([CompetitionSeasonId]) REFERENCES [CompetitionSeasons] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TeamStatistics_Teams_TeamId] FOREIGN KEY ([TeamId]) REFERENCES [Teams] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE TABLE [TeamApplicationAreas] (
        [TeamApplicationId] int NOT NULL,
        [AreaId] int NOT NULL,
        CONSTRAINT [PK_TeamApplicationAreas] PRIMARY KEY ([TeamApplicationId], [AreaId]),
        CONSTRAINT [FK_TeamApplicationAreas_Areas_AreaId] FOREIGN KEY ([AreaId]) REFERENCES [Areas] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TeamApplicationAreas_TeamApplications_TeamApplicationId] FOREIGN KEY ([TeamApplicationId]) REFERENCES [TeamApplications] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE TABLE [CompetitionGroups] (
        [Id] int NOT NULL IDENTITY,
        [StageId] int NOT NULL,
        [Name] nvarchar(50) NOT NULL,
        [SortOrder] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_CompetitionGroups] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CompetitionGroups_CompetitionStages_StageId] FOREIGN KEY ([StageId]) REFERENCES [CompetitionStages] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE TABLE [CompetitionStandings] (
        [Id] int NOT NULL IDENTITY,
        [CompetitionSeasonId] int NOT NULL,
        [StageId] int NULL,
        [GroupId] int NULL,
        [TeamId] int NOT NULL,
        [Position] int NOT NULL,
        [Played] int NOT NULL,
        [Won] int NOT NULL,
        [Drawn] int NOT NULL,
        [Lost] int NOT NULL,
        [GoalsFor] int NOT NULL,
        [GoalsAgainst] int NOT NULL,
        [GoalDifference] AS [GoalsFor] - [GoalsAgainst] PERSISTED,
        [Points] int NOT NULL,
        [PointsAdjustment] int NOT NULL,
        [Form] nvarchar(10) NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_CompetitionStandings] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CompetitionStandings_CompetitionGroups_GroupId] FOREIGN KEY ([GroupId]) REFERENCES [CompetitionGroups] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CompetitionStandings_CompetitionSeasons_CompetitionSeasonId] FOREIGN KEY ([CompetitionSeasonId]) REFERENCES [CompetitionSeasons] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CompetitionStandings_CompetitionStages_StageId] FOREIGN KEY ([StageId]) REFERENCES [CompetitionStages] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CompetitionStandings_Teams_TeamId] FOREIGN KEY ([TeamId]) REFERENCES [Teams] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE TABLE [CompetitionTeams] (
        [Id] int NOT NULL IDENTITY,
        [CompetitionSeasonId] int NOT NULL,
        [TeamId] int NOT NULL,
        [GroupId] int NULL,
        [Seed] int NULL,
        [IsWithdrawn] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_CompetitionTeams] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CompetitionTeams_CompetitionGroups_GroupId] FOREIGN KEY ([GroupId]) REFERENCES [CompetitionGroups] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CompetitionTeams_CompetitionSeasons_CompetitionSeasonId] FOREIGN KEY ([CompetitionSeasonId]) REFERENCES [CompetitionSeasons] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CompetitionTeams_Teams_TeamId] FOREIGN KEY ([TeamId]) REFERENCES [Teams] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE TABLE [Matches] (
        [Id] int NOT NULL IDENTITY,
        [SportId] int NOT NULL,
        [CompetitionSeasonId] int NULL,
        [StageId] int NULL,
        [GroupId] int NULL,
        [RoundNumber] int NULL,
        [RoundName] nvarchar(100) NULL,
        [LegNumber] int NULL,
        [HomeTeamId] int NOT NULL,
        [AwayTeamId] int NOT NULL,
        [VenueId] int NULL,
        [KickoffAt] datetime2 NOT NULL,
        [Status] int NOT NULL,
        [HomeScore] int NOT NULL,
        [AwayScore] int NOT NULL,
        [HomePenaltyScore] int NULL,
        [AwayPenaltyScore] int NULL,
        [WinnerTeamId] int NULL,
        [CurrentPeriod] int NOT NULL,
        [StartedAt] datetime2 NULL,
        [CurrentPeriodStartedAt] datetime2 NULL,
        [FinishedAt] datetime2 NULL,
        [OperatorUserId] nvarchar(450) NULL,
        [Notes] nvarchar(500) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Matches] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Matches_DifferentTeams] CHECK ([HomeTeamId] <> [AwayTeamId]),
        CONSTRAINT [CK_Matches_Scores] CHECK ([HomeScore] >= 0 AND [AwayScore] >= 0),
        CONSTRAINT [CK_Matches_Winner] CHECK ([WinnerTeamId] IS NULL OR [WinnerTeamId] IN ([HomeTeamId], [AwayTeamId])),
        CONSTRAINT [FK_Matches_AspNetUsers_OperatorUserId] FOREIGN KEY ([OperatorUserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Matches_CompetitionGroups_GroupId] FOREIGN KEY ([GroupId]) REFERENCES [CompetitionGroups] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Matches_CompetitionSeasons_CompetitionSeasonId] FOREIGN KEY ([CompetitionSeasonId]) REFERENCES [CompetitionSeasons] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Matches_CompetitionStages_StageId] FOREIGN KEY ([StageId]) REFERENCES [CompetitionStages] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Matches_Sports_SportId] FOREIGN KEY ([SportId]) REFERENCES [Sports] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Matches_Teams_AwayTeamId] FOREIGN KEY ([AwayTeamId]) REFERENCES [Teams] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Matches_Teams_HomeTeamId] FOREIGN KEY ([HomeTeamId]) REFERENCES [Teams] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Matches_Teams_WinnerTeamId] FOREIGN KEY ([WinnerTeamId]) REFERENCES [Teams] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Matches_Venues_VenueId] FOREIGN KEY ([VenueId]) REFERENCES [Venues] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE TABLE [MatchEvents] (
        [Id] int NOT NULL IDENTITY,
        [MatchId] int NOT NULL,
        [EventType] int NOT NULL,
        [TeamId] int NULL,
        [PlayerId] int NULL,
        [RelatedPlayerId] int NULL,
        [Minute] int NOT NULL,
        [ExtraMinute] int NULL,
        [Period] int NOT NULL,
        [Notes] nvarchar(300) NULL,
        [ClientRequestId] uniqueidentifier NOT NULL,
        [IsCancelled] bit NOT NULL,
        [CreatedByUserId] nvarchar(450) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_MatchEvents] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_MatchEvents_Minute] CHECK ([Minute] BETWEEN 0 AND 200 AND ([ExtraMinute] IS NULL OR [ExtraMinute] BETWEEN 0 AND 30)),
        CONSTRAINT [FK_MatchEvents_AspNetUsers_CreatedByUserId] FOREIGN KEY ([CreatedByUserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_MatchEvents_Matches_MatchId] FOREIGN KEY ([MatchId]) REFERENCES [Matches] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_MatchEvents_Players_PlayerId] FOREIGN KEY ([PlayerId]) REFERENCES [Players] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_MatchEvents_Players_RelatedPlayerId] FOREIGN KEY ([RelatedPlayerId]) REFERENCES [Players] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_MatchEvents_Teams_TeamId] FOREIGN KEY ([TeamId]) REFERENCES [Teams] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE TABLE [MatchLineups] (
        [Id] int NOT NULL IDENTITY,
        [MatchId] int NOT NULL,
        [TeamId] int NOT NULL,
        [PlayerId] int NOT NULL,
        [ShirtNumber] int NULL,
        [PositionId] int NULL,
        [IsStarter] bit NOT NULL,
        [IsCaptain] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_MatchLineups] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MatchLineups_Matches_MatchId] FOREIGN KEY ([MatchId]) REFERENCES [Matches] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_MatchLineups_Players_PlayerId] FOREIGN KEY ([PlayerId]) REFERENCES [Players] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_MatchLineups_SportPositions_PositionId] FOREIGN KEY ([PositionId]) REFERENCES [SportPositions] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_MatchLineups_Teams_TeamId] FOREIGN KEY ([TeamId]) REFERENCES [Teams] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE TABLE [MatchOfficials] (
        [Id] int NOT NULL IDENTITY,
        [MatchId] int NOT NULL,
        [Role] int NOT NULL,
        [Name] nvarchar(150) NOT NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_MatchOfficials] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MatchOfficials_Matches_MatchId] FOREIGN KEY ([MatchId]) REFERENCES [Matches] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE TABLE [MatchReports] (
        [Id] int NOT NULL IDENTITY,
        [MatchId] int NOT NULL,
        [Summary] nvarchar(4000) NULL,
        [Attendance] int NULL,
        [BestPlayerId] int NULL,
        [WrittenByUserId] nvarchar(450) NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_MatchReports] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MatchReports_AspNetUsers_WrittenByUserId] FOREIGN KEY ([WrittenByUserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_MatchReports_Matches_MatchId] FOREIGN KEY ([MatchId]) REFERENCES [Matches] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_MatchReports_Players_BestPlayerId] FOREIGN KEY ([BestPlayerId]) REFERENCES [Players] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE TABLE [News] (
        [Id] int NOT NULL IDENTITY,
        [TitleAr] nvarchar(250) NOT NULL,
        [Slug] nvarchar(270) NOT NULL,
        [Summary] nvarchar(500) NULL,
        [Body] nvarchar(max) NOT NULL,
        [CoverImagePath] nvarchar(300) NULL,
        [VideoUrl] nvarchar(300) NULL,
        [Category] int NOT NULL,
        [TeamId] int NULL,
        [PlayerId] int NULL,
        [CompetitionId] int NULL,
        [MatchId] int NULL,
        [AuthorUserId] nvarchar(450) NULL,
        [IsPublished] bit NOT NULL,
        [PublishedAt] datetime2 NULL,
        [ViewCount] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_News] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_News_AspNetUsers_AuthorUserId] FOREIGN KEY ([AuthorUserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_News_Competitions_CompetitionId] FOREIGN KEY ([CompetitionId]) REFERENCES [Competitions] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_News_Matches_MatchId] FOREIGN KEY ([MatchId]) REFERENCES [Matches] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_News_Players_PlayerId] FOREIGN KEY ([PlayerId]) REFERENCES [Players] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_News_Teams_TeamId] FOREIGN KEY ([TeamId]) REFERENCES [Teams] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE TABLE [UserFollows] (
        [Id] int NOT NULL IDENTITY,
        [UserId] nvarchar(450) NOT NULL,
        [TargetType] int NOT NULL,
        [TeamId] int NULL,
        [PlayerId] int NULL,
        [CompetitionId] int NULL,
        [MatchId] int NULL,
        [NotificationsEnabled] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_UserFollows] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_UserFollows_SingleTarget] CHECK (([TargetType] = 0 AND [TeamId] IS NOT NULL AND [PlayerId] IS NULL AND [CompetitionId] IS NULL AND [MatchId] IS NULL) OR ([TargetType] = 1 AND [PlayerId] IS NOT NULL AND [TeamId] IS NULL AND [CompetitionId] IS NULL AND [MatchId] IS NULL) OR ([TargetType] = 2 AND [CompetitionId] IS NOT NULL AND [TeamId] IS NULL AND [PlayerId] IS NULL AND [MatchId] IS NULL) OR ([TargetType] = 3 AND [MatchId] IS NOT NULL AND [TeamId] IS NULL AND [PlayerId] IS NULL AND [CompetitionId] IS NULL)),
        CONSTRAINT [FK_UserFollows_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_UserFollows_Competitions_CompetitionId] FOREIGN KEY ([CompetitionId]) REFERENCES [Competitions] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_UserFollows_Matches_MatchId] FOREIGN KEY ([MatchId]) REFERENCES [Matches] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_UserFollows_Players_PlayerId] FOREIGN KEY ([PlayerId]) REFERENCES [Players] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_UserFollows_Teams_TeamId] FOREIGN KEY ([TeamId]) REFERENCES [Teams] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE TABLE [MatchEventRevisions] (
        [Id] int NOT NULL IDENTITY,
        [MatchEventId] int NOT NULL,
        [Action] int NOT NULL,
        [OldValuesJson] nvarchar(max) NULL,
        [NewValuesJson] nvarchar(max) NULL,
        [Reason] nvarchar(500) NULL,
        [ChangedByUserId] nvarchar(450) NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_MatchEventRevisions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MatchEventRevisions_AspNetUsers_ChangedByUserId] FOREIGN KEY ([ChangedByUserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_MatchEventRevisions_MatchEvents_MatchEventId] FOREIGN KEY ([MatchEventId]) REFERENCES [MatchEvents] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE TABLE [MediaFiles] (
        [Id] int NOT NULL IDENTITY,
        [MediaType] int NOT NULL,
        [FilePath] nvarchar(500) NOT NULL,
        [OriginalFileName] nvarchar(260) NULL,
        [ContentType] nvarchar(100) NULL,
        [SizeBytes] bigint NOT NULL,
        [Caption] nvarchar(300) NULL,
        [NewsId] int NULL,
        [TeamId] int NULL,
        [PlayerId] int NULL,
        [MatchId] int NULL,
        [UploadedByUserId] nvarchar(450) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_MediaFiles] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MediaFiles_AspNetUsers_UploadedByUserId] FOREIGN KEY ([UploadedByUserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_MediaFiles_Matches_MatchId] FOREIGN KEY ([MatchId]) REFERENCES [Matches] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_MediaFiles_News_NewsId] FOREIGN KEY ([NewsId]) REFERENCES [News] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_MediaFiles_Players_PlayerId] FOREIGN KEY ([PlayerId]) REFERENCES [Players] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_MediaFiles_Teams_TeamId] FOREIGN KEY ([TeamId]) REFERENCES [Teams] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE TABLE [Notifications] (
        [Id] int NOT NULL IDENTITY,
        [UserId] nvarchar(450) NOT NULL,
        [Type] int NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [Message] nvarchar(500) NULL,
        [Url] nvarchar(300) NULL,
        [MatchId] int NULL,
        [TeamId] int NULL,
        [NewsId] int NULL,
        [IsRead] bit NOT NULL,
        [ReadAt] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Notifications] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Notifications_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_Notifications_Matches_MatchId] FOREIGN KEY ([MatchId]) REFERENCES [Matches] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Notifications_News_NewsId] FOREIGN KEY ([NewsId]) REFERENCES [News] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Notifications_Teams_TeamId] FOREIGN KEY ([TeamId]) REFERENCES [Teams] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedAt', N'IsActive', N'NameAr', N'NameEn', N'SortOrder', N'UpdatedAt') AND [object_id] = OBJECT_ID(N'[Governorates]'))
        SET IDENTITY_INSERT [Governorates] ON;
    EXEC(N'INSERT INTO [Governorates] ([Id], [CreatedAt], [IsActive], [NameAr], [NameEn], [SortOrder], [UpdatedAt])
    VALUES (1, ''2026-01-01T00:00:00.0000000Z'', CAST(1 AS bit), N''مسقط'', N''Muscat'', 1, NULL),
    (2, ''2026-01-01T00:00:00.0000000Z'', CAST(1 AS bit), N''ظفار'', N''Dhofar'', 2, NULL),
    (3, ''2026-01-01T00:00:00.0000000Z'', CAST(1 AS bit), N''مسندم'', N''Musandam'', 3, NULL),
    (4, ''2026-01-01T00:00:00.0000000Z'', CAST(1 AS bit), N''البريمي'', N''Al Buraimi'', 4, NULL),
    (5, ''2026-01-01T00:00:00.0000000Z'', CAST(1 AS bit), N''الداخلية'', N''Ad Dakhiliyah'', 5, NULL),
    (6, ''2026-01-01T00:00:00.0000000Z'', CAST(1 AS bit), N''شمال الباطنة'', N''North Al Batinah'', 6, NULL),
    (7, ''2026-01-01T00:00:00.0000000Z'', CAST(1 AS bit), N''جنوب الباطنة'', N''South Al Batinah'', 7, NULL),
    (8, ''2026-01-01T00:00:00.0000000Z'', CAST(1 AS bit), N''جنوب الشرقية'', N''South Ash Sharqiyah'', 8, NULL),
    (9, ''2026-01-01T00:00:00.0000000Z'', CAST(1 AS bit), N''شمال الشرقية'', N''North Ash Sharqiyah'', 9, NULL),
    (10, ''2026-01-01T00:00:00.0000000Z'', CAST(1 AS bit), N''الظاهرة'', N''Ad Dhahirah'', 10, NULL),
    (11, ''2026-01-01T00:00:00.0000000Z'', CAST(1 AS bit), N''الوسطى'', N''Al Wusta'', 11, NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedAt', N'IsActive', N'NameAr', N'NameEn', N'SortOrder', N'UpdatedAt') AND [object_id] = OBJECT_ID(N'[Governorates]'))
        SET IDENTITY_INSERT [Governorates] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Code', N'CreatedAt', N'IconClass', N'IsActive', N'NameAr', N'NameEn', N'PeriodDurationMinutes', N'PeriodsCount', N'PlayersOnField', N'PointsForDraw', N'PointsForLoss', N'PointsForWin', N'SortOrder', N'UpdatedAt') AND [object_id] = OBJECT_ID(N'[Sports]'))
        SET IDENTITY_INSERT [Sports] ON;
    EXEC(N'INSERT INTO [Sports] ([Id], [Code], [CreatedAt], [IconClass], [IsActive], [NameAr], [NameEn], [PeriodDurationMinutes], [PeriodsCount], [PlayersOnField], [PointsForDraw], [PointsForLoss], [PointsForWin], [SortOrder], [UpdatedAt])
    VALUES (1, N''football'', ''2026-01-01T00:00:00.0000000Z'', N''fa-solid fa-futbol'', CAST(1 AS bit), N''كرة القدم'', N''Football'', 45, 2, 11, 1, 0, 3, 1, NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Code', N'CreatedAt', N'IconClass', N'IsActive', N'NameAr', N'NameEn', N'PeriodDurationMinutes', N'PeriodsCount', N'PlayersOnField', N'PointsForDraw', N'PointsForLoss', N'PointsForWin', N'SortOrder', N'UpdatedAt') AND [object_id] = OBJECT_ID(N'[Sports]'))
        SET IDENTITY_INSERT [Sports] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Code', N'CreatedAt', N'NameAr', N'NameEn', N'SortOrder', N'SportId', N'UpdatedAt') AND [object_id] = OBJECT_ID(N'[SportPositions]'))
        SET IDENTITY_INSERT [SportPositions] ON;
    EXEC(N'INSERT INTO [SportPositions] ([Id], [Code], [CreatedAt], [NameAr], [NameEn], [SortOrder], [SportId], [UpdatedAt])
    VALUES (1, N''GK'', ''2026-01-01T00:00:00.0000000Z'', N''حارس مرمى'', N''Goalkeeper'', 1, 1, NULL),
    (2, N''DF'', ''2026-01-01T00:00:00.0000000Z'', N''مدافع'', N''Defender'', 2, 1, NULL),
    (3, N''MF'', ''2026-01-01T00:00:00.0000000Z'', N''لاعب وسط'', N''Midfielder'', 3, 1, NULL),
    (4, N''FW'', ''2026-01-01T00:00:00.0000000Z'', N''مهاجم'', N''Forward'', 4, 1, NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Code', N'CreatedAt', N'NameAr', N'NameEn', N'SortOrder', N'SportId', N'UpdatedAt') AND [object_id] = OBJECT_ID(N'[SportPositions]'))
        SET IDENTITY_INSERT [SportPositions] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedAt', N'GovernorateId', N'IsActive', N'NameAr', N'NameEn', N'SortOrder', N'UpdatedAt') AND [object_id] = OBJECT_ID(N'[Wilayats]'))
        SET IDENTITY_INSERT [Wilayats] ON;
    EXEC(N'INSERT INTO [Wilayats] ([Id], [CreatedAt], [GovernorateId], [IsActive], [NameAr], [NameEn], [SortOrder], [UpdatedAt])
    VALUES (1, ''2026-01-01T00:00:00.0000000Z'', 1, CAST(1 AS bit), N''مسقط'', N''Muscat'', 1, NULL),
    (2, ''2026-01-01T00:00:00.0000000Z'', 1, CAST(1 AS bit), N''مطرح'', N''Muttrah'', 2, NULL),
    (3, ''2026-01-01T00:00:00.0000000Z'', 1, CAST(1 AS bit), N''بوشر'', N''Bawshar'', 3, NULL),
    (4, ''2026-01-01T00:00:00.0000000Z'', 1, CAST(1 AS bit), N''السيب'', N''As Seeb'', 4, NULL),
    (5, ''2026-01-01T00:00:00.0000000Z'', 1, CAST(1 AS bit), N''العامرات'', N''Al Amarat'', 5, NULL),
    (6, ''2026-01-01T00:00:00.0000000Z'', 1, CAST(1 AS bit), N''قريات'', N''Qurayyat'', 6, NULL),
    (7, ''2026-01-01T00:00:00.0000000Z'', 2, CAST(1 AS bit), N''صلالة'', N''Salalah'', 1, NULL),
    (8, ''2026-01-01T00:00:00.0000000Z'', 2, CAST(1 AS bit), N''طاقة'', N''Taqah'', 2, NULL),
    (9, ''2026-01-01T00:00:00.0000000Z'', 2, CAST(1 AS bit), N''مرباط'', N''Mirbat'', 3, NULL),
    (10, ''2026-01-01T00:00:00.0000000Z'', 2, CAST(1 AS bit), N''رخيوت'', N''Rakhyut'', 4, NULL),
    (11, ''2026-01-01T00:00:00.0000000Z'', 2, CAST(1 AS bit), N''ثمريت'', N''Thumrait'', 5, NULL),
    (12, ''2026-01-01T00:00:00.0000000Z'', 2, CAST(1 AS bit), N''ضلكوت'', N''Dhalkut'', 6, NULL),
    (13, ''2026-01-01T00:00:00.0000000Z'', 2, CAST(1 AS bit), N''المزيونة'', N''Al Mazyunah'', 7, NULL),
    (14, ''2026-01-01T00:00:00.0000000Z'', 2, CAST(1 AS bit), N''مقشن'', N''Muqshin'', 8, NULL),
    (15, ''2026-01-01T00:00:00.0000000Z'', 2, CAST(1 AS bit), N''شليم وجزر الحلانيات'', N''Shalim and the Hallaniyat Islands'', 9, NULL),
    (16, ''2026-01-01T00:00:00.0000000Z'', 2, CAST(1 AS bit), N''سدح'', N''Sadah'', 10, NULL),
    (17, ''2026-01-01T00:00:00.0000000Z'', 3, CAST(1 AS bit), N''خصب'', N''Khasab'', 1, NULL),
    (18, ''2026-01-01T00:00:00.0000000Z'', 3, CAST(1 AS bit), N''دبا'', N''Dibba'', 2, NULL),
    (19, ''2026-01-01T00:00:00.0000000Z'', 3, CAST(1 AS bit), N''بخا'', N''Bukha'', 3, NULL),
    (20, ''2026-01-01T00:00:00.0000000Z'', 3, CAST(1 AS bit), N''مدحاء'', N''Madha'', 4, NULL),
    (21, ''2026-01-01T00:00:00.0000000Z'', 4, CAST(1 AS bit), N''البريمي'', N''Al Buraimi'', 1, NULL),
    (22, ''2026-01-01T00:00:00.0000000Z'', 4, CAST(1 AS bit), N''محضة'', N''Mahdah'', 2, NULL),
    (23, ''2026-01-01T00:00:00.0000000Z'', 4, CAST(1 AS bit), N''السنينة'', N''As Sunaynah'', 3, NULL),
    (24, ''2026-01-01T00:00:00.0000000Z'', 5, CAST(1 AS bit), N''نزوى'', N''Nizwa'', 1, NULL),
    (25, ''2026-01-01T00:00:00.0000000Z'', 5, CAST(1 AS bit), N''بهلاء'', N''Bahla'', 2, NULL),
    (26, ''2026-01-01T00:00:00.0000000Z'', 5, CAST(1 AS bit), N''منح'', N''Manah'', 3, NULL),
    (27, ''2026-01-01T00:00:00.0000000Z'', 5, CAST(1 AS bit), N''الحمراء'', N''Al Hamra'', 4, NULL),
    (28, ''2026-01-01T00:00:00.0000000Z'', 5, CAST(1 AS bit), N''أدم'', N''Adam'', 5, NULL),
    (29, ''2026-01-01T00:00:00.0000000Z'', 5, CAST(1 AS bit), N''إزكي'', N''Izki'', 6, NULL),
    (30, ''2026-01-01T00:00:00.0000000Z'', 5, CAST(1 AS bit), N''سمائل'', N''Samail'', 7, NULL),
    (31, ''2026-01-01T00:00:00.0000000Z'', 5, CAST(1 AS bit), N''بدبد'', N''Bidbid'', 8, NULL),
    (32, ''2026-01-01T00:00:00.0000000Z'', 6, CAST(1 AS bit), N''صحار'', N''Sohar'', 1, NULL),
    (33, ''2026-01-01T00:00:00.0000000Z'', 6, CAST(1 AS bit), N''شناص'', N''Shinas'', 2, NULL),
    (34, ''2026-01-01T00:00:00.0000000Z'', 6, CAST(1 AS bit), N''لوى'', N''Liwa'', 3, NULL),
    (35, ''2026-01-01T00:00:00.0000000Z'', 6, CAST(1 AS bit), N''صحم'', N''Saham'', 4, NULL),
    (36, ''2026-01-01T00:00:00.0000000Z'', 6, CAST(1 AS bit), N''الخابورة'', N''Al Khaburah'', 5, NULL),
    (37, ''2026-01-01T00:00:00.0000000Z'', 6, CAST(1 AS bit), N''السويق'', N''As Suwayq'', 6, NULL),
    (38, ''2026-01-01T00:00:00.0000000Z'', 7, CAST(1 AS bit), N''الرستاق'', N''Ar Rustaq'', 1, NULL),
    (39, ''2026-01-01T00:00:00.0000000Z'', 7, CAST(1 AS bit), N''العوابي'', N''Al Awabi'', 2, NULL),
    (40, ''2026-01-01T00:00:00.0000000Z'', 7, CAST(1 AS bit), N''نخل'', N''Nakhal'', 3, NULL),
    (41, ''2026-01-01T00:00:00.0000000Z'', 7, CAST(1 AS bit), N''وادي المعاول'', N''Wadi Al Maawil'', 4, NULL),
    (42, ''2026-01-01T00:00:00.0000000Z'', 7, CAST(1 AS bit), N''بركاء'', N''Barka'', 5, NULL);
    INSERT INTO [Wilayats] ([Id], [CreatedAt], [GovernorateId], [IsActive], [NameAr], [NameEn], [SortOrder], [UpdatedAt])
    VALUES (43, ''2026-01-01T00:00:00.0000000Z'', 7, CAST(1 AS bit), N''المصنعة'', N''Al Musannah'', 6, NULL),
    (44, ''2026-01-01T00:00:00.0000000Z'', 8, CAST(1 AS bit), N''صور'', N''Sur'', 1, NULL),
    (45, ''2026-01-01T00:00:00.0000000Z'', 8, CAST(1 AS bit), N''الكامل والوافي'', N''Al Kamil Wal Wafi'', 2, NULL),
    (46, ''2026-01-01T00:00:00.0000000Z'', 8, CAST(1 AS bit), N''جعلان بني بو حسن'', N''Jaalan Bani Bu Hassan'', 3, NULL),
    (47, ''2026-01-01T00:00:00.0000000Z'', 8, CAST(1 AS bit), N''جعلان بني بو علي'', N''Jaalan Bani Bu Ali'', 4, NULL),
    (48, ''2026-01-01T00:00:00.0000000Z'', 8, CAST(1 AS bit), N''مصيرة'', N''Masirah'', 5, NULL),
    (49, ''2026-01-01T00:00:00.0000000Z'', 9, CAST(1 AS bit), N''إبراء'', N''Ibra'', 1, NULL),
    (50, ''2026-01-01T00:00:00.0000000Z'', 9, CAST(1 AS bit), N''المضيبي'', N''Al Mudaybi'', 2, NULL),
    (51, ''2026-01-01T00:00:00.0000000Z'', 9, CAST(1 AS bit), N''بدية'', N''Bidiyah'', 3, NULL),
    (52, ''2026-01-01T00:00:00.0000000Z'', 9, CAST(1 AS bit), N''القابل'', N''Al Qabil'', 4, NULL),
    (53, ''2026-01-01T00:00:00.0000000Z'', 9, CAST(1 AS bit), N''وادي بني خالد'', N''Wadi Bani Khalid'', 5, NULL),
    (54, ''2026-01-01T00:00:00.0000000Z'', 9, CAST(1 AS bit), N''دماء والطائيين'', N''Dima Wa At Taiyyin'', 6, NULL),
    (55, ''2026-01-01T00:00:00.0000000Z'', 10, CAST(1 AS bit), N''عبري'', N''Ibri'', 1, NULL),
    (56, ''2026-01-01T00:00:00.0000000Z'', 10, CAST(1 AS bit), N''ينقل'', N''Yanqul'', 2, NULL),
    (57, ''2026-01-01T00:00:00.0000000Z'', 10, CAST(1 AS bit), N''ضنك'', N''Dhank'', 3, NULL),
    (58, ''2026-01-01T00:00:00.0000000Z'', 11, CAST(1 AS bit), N''هيماء'', N''Haima'', 1, NULL),
    (59, ''2026-01-01T00:00:00.0000000Z'', 11, CAST(1 AS bit), N''محوت'', N''Mahut'', 2, NULL),
    (60, ''2026-01-01T00:00:00.0000000Z'', 11, CAST(1 AS bit), N''الدقم'', N''Duqm'', 3, NULL),
    (61, ''2026-01-01T00:00:00.0000000Z'', 11, CAST(1 AS bit), N''الجازر'', N''Al Jazir'', 4, NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedAt', N'GovernorateId', N'IsActive', N'NameAr', N'NameEn', N'SortOrder', N'UpdatedAt') AND [object_id] = OBJECT_ID(N'[Wilayats]'))
        SET IDENTITY_INSERT [Wilayats] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Areas_WilayatId_NameAr] ON [Areas] ([WilayatId], [NameAr]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [AspNetRoleClaims] ([RoleId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [RoleNameIndex] ON [AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [AspNetUserRoles] ([RoleId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [EmailIndex] ON [AspNetUsers] ([NormalizedEmail]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UserNameIndex] ON [AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_CreatedAt] ON [AuditLogs] ([CreatedAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_EntityName_EntityId] ON [AuditLogs] ([EntityName], [EntityId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_UserId] ON [AuditLogs] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CompetitionGroups_StageId_Name] ON [CompetitionGroups] ([StageId], [Name]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CompetitionManagers_UserId] ON [CompetitionManagers] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Competitions_AreaId] ON [Competitions] ([AreaId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Competitions_GovernorateId] ON [Competitions] ([GovernorateId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Competitions_Slug] ON [Competitions] ([Slug]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Competitions_SportId_IsActive] ON [Competitions] ([SportId], [IsActive]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Competitions_WilayatId] ON [Competitions] ([WilayatId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CompetitionSeasons_ChampionTeamId] ON [CompetitionSeasons] ([ChampionTeamId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CompetitionSeasons_CompetitionId_SeasonId] ON [CompetitionSeasons] ([CompetitionId], [SeasonId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CompetitionSeasons_SeasonId] ON [CompetitionSeasons] ([SeasonId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CompetitionSeasons_Status] ON [CompetitionSeasons] ([Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CompetitionStages_CompetitionSeasonId_SortOrder] ON [CompetitionStages] ([CompetitionSeasonId], [SortOrder]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CompetitionStandings_CompetitionSeasonId_GroupId_Position] ON [CompetitionStandings] ([CompetitionSeasonId], [GroupId], [Position]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_CompetitionStandings_CompetitionSeasonId_StageId_GroupId_TeamId] ON [CompetitionStandings] ([CompetitionSeasonId], [StageId], [GroupId], [TeamId]) WHERE [StageId] IS NOT NULL AND [GroupId] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CompetitionStandings_GroupId] ON [CompetitionStandings] ([GroupId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CompetitionStandings_StageId] ON [CompetitionStandings] ([StageId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CompetitionStandings_TeamId] ON [CompetitionStandings] ([TeamId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CompetitionTeams_CompetitionSeasonId_TeamId] ON [CompetitionTeams] ([CompetitionSeasonId], [TeamId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CompetitionTeams_GroupId] ON [CompetitionTeams] ([GroupId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CompetitionTeams_TeamId] ON [CompetitionTeams] ([TeamId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Governorates_NameAr] ON [Governorates] ([NameAr]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Governorates_NameEn] ON [Governorates] ([NameEn]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Matches_AwayTeamId] ON [Matches] ([AwayTeamId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Matches_CompetitionSeasonId_RoundNumber] ON [Matches] ([CompetitionSeasonId], [RoundNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Matches_GroupId] ON [Matches] ([GroupId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Matches_HomeTeamId] ON [Matches] ([HomeTeamId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Matches_KickoffAt] ON [Matches] ([KickoffAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Matches_OperatorUserId] ON [Matches] ([OperatorUserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Matches_SportId] ON [Matches] ([SportId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Matches_StageId] ON [Matches] ([StageId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Matches_Status_KickoffAt] ON [Matches] ([Status], [KickoffAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Matches_VenueId] ON [Matches] ([VenueId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Matches_WinnerTeamId] ON [Matches] ([WinnerTeamId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_MatchEventRevisions_ChangedByUserId] ON [MatchEventRevisions] ([ChangedByUserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_MatchEventRevisions_MatchEventId] ON [MatchEventRevisions] ([MatchEventId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_MatchEvents_CreatedByUserId] ON [MatchEvents] ([CreatedByUserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_MatchEvents_MatchId_ClientRequestId] ON [MatchEvents] ([MatchId], [ClientRequestId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_MatchEvents_MatchId_IsCancelled_Period_Minute] ON [MatchEvents] ([MatchId], [IsCancelled], [Period], [Minute]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_MatchEvents_PlayerId] ON [MatchEvents] ([PlayerId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_MatchEvents_RelatedPlayerId] ON [MatchEvents] ([RelatedPlayerId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_MatchEvents_TeamId] ON [MatchEvents] ([TeamId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_MatchLineups_MatchId_PlayerId] ON [MatchLineups] ([MatchId], [PlayerId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_MatchLineups_MatchId_TeamId] ON [MatchLineups] ([MatchId], [TeamId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_MatchLineups_PlayerId] ON [MatchLineups] ([PlayerId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_MatchLineups_PositionId] ON [MatchLineups] ([PositionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_MatchLineups_TeamId] ON [MatchLineups] ([TeamId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_MatchOfficials_MatchId_Role] ON [MatchOfficials] ([MatchId], [Role]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_MatchReports_BestPlayerId] ON [MatchReports] ([BestPlayerId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_MatchReports_MatchId] ON [MatchReports] ([MatchId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_MatchReports_WrittenByUserId] ON [MatchReports] ([WrittenByUserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_MediaFiles_MatchId] ON [MediaFiles] ([MatchId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_MediaFiles_NewsId] ON [MediaFiles] ([NewsId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_MediaFiles_PlayerId] ON [MediaFiles] ([PlayerId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_MediaFiles_TeamId] ON [MediaFiles] ([TeamId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_MediaFiles_UploadedByUserId] ON [MediaFiles] ([UploadedByUserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_News_AuthorUserId] ON [News] ([AuthorUserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_News_CompetitionId] ON [News] ([CompetitionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_News_IsPublished_PublishedAt] ON [News] ([IsPublished], [PublishedAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_News_MatchId] ON [News] ([MatchId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_News_PlayerId] ON [News] ([PlayerId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_News_Slug] ON [News] ([Slug]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_News_TeamId] ON [News] ([TeamId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Notifications_MatchId] ON [Notifications] ([MatchId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Notifications_NewsId] ON [Notifications] ([NewsId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Notifications_TeamId] ON [Notifications] ([TeamId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Notifications_UserId_IsRead_CreatedAt] ON [Notifications] ([UserId], [IsRead], [CreatedAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Players_FullNameAr] ON [Players] ([FullNameAr]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Players_PositionId] ON [Players] ([PositionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Players_SportId] ON [Players] ([SportId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Players_UserId] ON [Players] ([UserId]) WHERE [UserId] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_PlayerStatistics_CompetitionSeasonId_Goals] ON [PlayerStatistics] ([CompetitionSeasonId], [Goals]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PlayerStatistics_PlayerId_CompetitionSeasonId_TeamId] ON [PlayerStatistics] ([PlayerId], [CompetitionSeasonId], [TeamId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_PlayerStatistics_TeamId] ON [PlayerStatistics] ([TeamId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_PlayerTransfers_FromTeamId] ON [PlayerTransfers] ([FromTeamId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_PlayerTransfers_PlayerId_TransferDate] ON [PlayerTransfers] ([PlayerId], [TransferDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_PlayerTransfers_RecordedByUserId] ON [PlayerTransfers] ([RecordedByUserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_PlayerTransfers_ToTeamId] ON [PlayerTransfers] ([ToTeamId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PushSubscriptions_Endpoint] ON [PushSubscriptions] ([Endpoint]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_PushSubscriptions_UserId] ON [PushSubscriptions] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Seasons_Name] ON [Seasons] ([Name]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_Seasons_Current] ON [Seasons] ([IsCurrent]) WHERE [IsCurrent] = 1');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SportPositions_SportId_Code] ON [SportPositions] ([SportId], [Code]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Sports_Code] ON [Sports] ([Code]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Sports_NameAr] ON [Sports] ([NameAr]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_TeamAchievements_CompetitionId] ON [TeamAchievements] ([CompetitionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_TeamAchievements_TeamId_Year] ON [TeamAchievements] ([TeamId], [Year]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_TeamApplicationAreas_AreaId] ON [TeamApplicationAreas] ([AreaId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_TeamApplications_ApplicantUserId] ON [TeamApplications] ([ApplicantUserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_TeamApplications_AreaId] ON [TeamApplications] ([AreaId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_TeamApplications_CreatedTeamId] ON [TeamApplications] ([CreatedTeamId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_TeamApplications_GovernorateId] ON [TeamApplications] ([GovernorateId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_TeamApplications_ReviewedByUserId] ON [TeamApplications] ([ReviewedByUserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_TeamApplications_SportId] ON [TeamApplications] ([SportId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_TeamApplications_Status_SubmittedAt] ON [TeamApplications] ([Status], [SubmittedAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_TeamApplications_VillageId] ON [TeamApplications] ([VillageId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_TeamApplications_WilayatId] ON [TeamApplications] ([WilayatId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_TeamAreas_AreaId] ON [TeamAreas] ([AreaId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_TeamAreas_TeamId_Primary] ON [TeamAreas] ([TeamId]) WHERE [IsPrimary] = 1');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TeamMembers_TeamId_UserId] ON [TeamMembers] ([TeamId], [UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_TeamMembers_UserId] ON [TeamMembers] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_TeamPlayers_PositionId] ON [TeamPlayers] ([PositionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_TeamPlayers_TeamId_IsActive] ON [TeamPlayers] ([TeamId], [IsActive]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_TeamPlayers_PlayerId_Active] ON [TeamPlayers] ([PlayerId]) WHERE [IsActive] = 1');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_TeamPlayers_TeamId_ShirtNumber_Active] ON [TeamPlayers] ([TeamId], [ShirtNumber]) WHERE [IsActive] = 1 AND [ShirtNumber] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Teams_HomeVenueId] ON [Teams] ([HomeVenueId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Teams_NameAr] ON [Teams] ([NameAr]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Teams_Slug] ON [Teams] ([Slug]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Teams_SportId] ON [Teams] ([SportId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Teams_Status_IsDeleted] ON [Teams] ([Status], [IsDeleted]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Teams_VillageId] ON [Teams] ([VillageId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_TeamStatistics_CompetitionSeasonId] ON [TeamStatistics] ([CompetitionSeasonId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TeamStatistics_TeamId_CompetitionSeasonId] ON [TeamStatistics] ([TeamId], [CompetitionSeasonId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_UserAreas_AreaId] ON [UserAreas] ([AreaId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_UserAreas_GovernorateId] ON [UserAreas] ([GovernorateId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_UserAreas_VillageId] ON [UserAreas] ([VillageId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_UserAreas_WilayatId] ON [UserAreas] ([WilayatId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_UserAreas_UserId_Default] ON [UserAreas] ([UserId]) WHERE [IsDefault] = 1');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_UserAreas_UserId_Region] ON [UserAreas] ([UserId], [GovernorateId], [WilayatId], [AreaId], [VillageId]) WHERE [WilayatId] IS NOT NULL AND [AreaId] IS NOT NULL AND [VillageId] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_UserFollows_CompetitionId] ON [UserFollows] ([CompetitionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_UserFollows_MatchId] ON [UserFollows] ([MatchId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_UserFollows_PlayerId] ON [UserFollows] ([PlayerId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_UserFollows_TeamId] ON [UserFollows] ([TeamId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_UserFollows_UserId_CompetitionId] ON [UserFollows] ([UserId], [CompetitionId]) WHERE [CompetitionId] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_UserFollows_UserId_MatchId] ON [UserFollows] ([UserId], [MatchId]) WHERE [MatchId] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_UserFollows_UserId_PlayerId] ON [UserFollows] ([UserId], [PlayerId]) WHERE [PlayerId] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_UserFollows_UserId_TeamId] ON [UserFollows] ([UserId], [TeamId]) WHERE [TeamId] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Venues_AreaId] ON [Venues] ([AreaId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Venues_NameAr] ON [Venues] ([NameAr]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Villages_AreaId_NameAr] ON [Villages] ([AreaId], [NameAr]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Wilayats_GovernorateId_NameAr] ON [Wilayats] ([GovernorateId], [NameAr]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101164724_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261101164724_InitialCreate', N'8.0.11');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101174232_AddProposedAreaName'
)
BEGIN
    DECLARE @var0 sysname;
    SELECT @var0 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TeamApplications]') AND [c].[name] = N'AreaId');
    IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [TeamApplications] DROP CONSTRAINT [' + @var0 + '];');
    ALTER TABLE [TeamApplications] ALTER COLUMN [AreaId] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101174232_AddProposedAreaName'
)
BEGIN
    ALTER TABLE [TeamApplications] ADD [ProposedAreaName] nvarchar(100) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101174232_AddProposedAreaName'
)
BEGIN
    EXEC(N'ALTER TABLE [TeamApplications] ADD CONSTRAINT [CK_TeamApplications_Area] CHECK ([AreaId] IS NOT NULL OR [ProposedAreaName] IS NOT NULL)');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101174232_AddProposedAreaName'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261101174232_AddProposedAreaName', N'8.0.11');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102204153_AddFriendlyOrganizer'
)
BEGIN
    ALTER TABLE [Matches] ADD [OrganizerTeamId] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102204153_AddFriendlyOrganizer'
)
BEGIN
    CREATE INDEX [IX_Matches_OrganizerTeamId] ON [Matches] ([OrganizerTeamId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102204153_AddFriendlyOrganizer'
)
BEGIN
    EXEC(N'ALTER TABLE [Matches] ADD CONSTRAINT [CK_Matches_Organizer] CHECK ([OrganizerTeamId] IS NULL OR ([CompetitionSeasonId] IS NULL AND [OrganizerTeamId] IN ([HomeTeamId], [AwayTeamId])))');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102204153_AddFriendlyOrganizer'
)
BEGIN
    ALTER TABLE [Matches] ADD CONSTRAINT [FK_Matches_Teams_OrganizerTeamId] FOREIGN KEY ([OrganizerTeamId]) REFERENCES [Teams] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102204153_AddFriendlyOrganizer'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261102204153_AddFriendlyOrganizer', N'8.0.11');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103142459_AddPushOutboxAndGuestPlayerNames'
)
BEGIN
    ALTER TABLE [Notifications] ADD [PushPending] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103142459_AddPushOutboxAndGuestPlayerNames'
)
BEGIN
    ALTER TABLE [MatchEvents] ADD [PlayerNameText] nvarchar(100) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103142459_AddPushOutboxAndGuestPlayerNames'
)
BEGIN
    ALTER TABLE [MatchEvents] ADD [RelatedPlayerNameText] nvarchar(100) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103142459_AddPushOutboxAndGuestPlayerNames'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_Notifications_PushPending] ON [Notifications] ([PushPending]) WHERE [PushPending] = 1');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103142459_AddPushOutboxAndGuestPlayerNames'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261103142459_AddPushOutboxAndGuestPlayerNames', N'8.0.11');
END;
GO

COMMIT;
GO


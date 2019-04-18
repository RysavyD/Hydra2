
-- --------------------------------------------------
-- Entity Designer DDL Script for SQL Server 2005, 2008, 2012 and Azure
-- --------------------------------------------------
-- Date Created: 12/25/2017 22:13:50
-- Generated from EDMX file: D:\Prace\Hydra\Hydra2\Hydra2.Model\Entities.edmx
-- --------------------------------------------------

SET QUOTED_IDENTIFIER OFF;
GO
USE [Hydra2];
GO
IF SCHEMA_ID(N'Hydra') IS NULL EXECUTE(N'CREATE SCHEMA [Hydra]');
GO

-- --------------------------------------------------
-- Dropping existing FOREIGN KEY constraints
-- --------------------------------------------------

IF OBJECT_ID(N'[dbo].[FK_Sample_Station]', 'F') IS NOT NULL
    ALTER TABLE [dbo].[Sample] DROP CONSTRAINT [FK_Sample_Station];
GO
IF OBJECT_ID(N'[dbo].[FK_Station_River]', 'F') IS NOT NULL
    ALTER TABLE [dbo].[Station] DROP CONSTRAINT [FK_Station_River];
GO

-- --------------------------------------------------
-- Dropping existing tables
-- --------------------------------------------------

IF OBJECT_ID(N'[dbo].[Config]', 'U') IS NOT NULL
    DROP TABLE [dbo].[Config];
GO
IF OBJECT_ID(N'[dbo].[River]', 'U') IS NOT NULL
    DROP TABLE [dbo].[River];
GO
IF OBJECT_ID(N'[dbo].[Sample]', 'U') IS NOT NULL
    DROP TABLE [dbo].[Sample];
GO
IF OBJECT_ID(N'[dbo].[Station]', 'U') IS NOT NULL
    DROP TABLE [dbo].[Station];
GO

-- --------------------------------------------------
-- Creating all tables
-- --------------------------------------------------

-- Creating table 'River'
CREATE TABLE [Hydra].[River] (
    [Id] int IDENTITY(1,1) NOT NULL,
    [Name] nvarchar(60)  NOT NULL,
    [RaftLink] nvarchar(60)  NULL
);
GO

-- Creating table 'Station'
CREATE TABLE [Hydra].[Station] (
    [Id] int IDENTITY(1,1) NOT NULL,
    [Spot] nvarchar(60)  NOT NULL,
    [Spa_val] int  NOT NULL,
    [Spa0] real  NULL,
    [Spa1] real  NULL,
    [Spa2] real  NULL,
    [Spa3] real  NULL,
    [Spa3e] real  NULL,
    [Type] int  NOT NULL,
    [Link] nvarchar(255)  NOT NULL,
    [Id_River] int  NOT NULL,
    [DownLoadType] int  NOT NULL
);
GO

-- Creating table 'Sample'
CREATE TABLE [Hydra].[Sample] (
    [Id] int IDENTITY(1,1) NOT NULL,
    [Id_Station] int  NOT NULL,
    [TimeStamp] datetime  NOT NULL,
    [Level] real  NULL,
    [Flow] real  NULL,
    [Temperature] real  NULL
);
GO

-- Creating table 'Config'
CREATE TABLE [Hydra].[Config] (
    [Id] int IDENTITY(1,1) NOT NULL,
    [Key] nvarchar(10)  NOT NULL,
    [Value] int  NOT NULL
);
GO

-- --------------------------------------------------
-- Creating all PRIMARY KEY constraints
-- --------------------------------------------------

-- Creating primary key on [Id] in table 'River'
ALTER TABLE [Hydra].[River]
ADD CONSTRAINT [PK_River]
    PRIMARY KEY CLUSTERED ([Id] ASC);
GO

-- Creating primary key on [Id] in table 'Station'
ALTER TABLE [Hydra].[Station]
ADD CONSTRAINT [PK_Station]
    PRIMARY KEY CLUSTERED ([Id] ASC);
GO

-- Creating primary key on [Id] in table 'Sample'
ALTER TABLE [Hydra].[Sample]
ADD CONSTRAINT [PK_Sample]
    PRIMARY KEY CLUSTERED ([Id] ASC);
GO

-- Creating primary key on [Id] in table 'Config'
ALTER TABLE [Hydra].[Config]
ADD CONSTRAINT [PK_Config]
    PRIMARY KEY CLUSTERED ([Id] ASC);
GO

-- --------------------------------------------------
-- Creating all FOREIGN KEY constraints
-- --------------------------------------------------

-- Creating foreign key on [Id_River] in table 'Station'
ALTER TABLE [Hydra].[Station]
ADD CONSTRAINT [FK_Station_River]
    FOREIGN KEY ([Id_River])
    REFERENCES [Hydra].[River]
        ([Id])
    ON DELETE NO ACTION ON UPDATE NO ACTION;
GO

-- Creating non-clustered index for FOREIGN KEY 'FK_Station_River'
CREATE INDEX [IX_FK_Station_River]
ON [Hydra].[Station]
    ([Id_River]);
GO

-- Creating foreign key on [Id_Station] in table 'Sample'
ALTER TABLE [Hydra].[Sample]
ADD CONSTRAINT [FK_Sample_Station]
    FOREIGN KEY ([Id_Station])
    REFERENCES [Hydra].[Station]
        ([Id])
    ON DELETE NO ACTION ON UPDATE NO ACTION;
GO

-- Creating non-clustered index for FOREIGN KEY 'FK_Sample_Station'
CREATE INDEX [IX_FK_Sample_Station]
ON [Hydra].[Sample]
    ([Id_Station]);
GO

-- --------------------------------------------------
-- Script has ended
-- --------------------------------------------------
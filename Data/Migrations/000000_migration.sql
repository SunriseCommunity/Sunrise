CREATE TABLE IF NOT EXISTS `migration`
(
    `Id`        INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL,
    `Name`      VARCHAR(1024) COLLATE NOCASE      NOT NULL,
    `AppliedAt` TEXT                              NOT NULL
);

CREATE TABLE IF NOT EXISTS `user`
(
    `Id`             INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL,
    `Username`       VARCHAR(64) COLLATE NOCASE        NOT NULL,
    `Email`          VARCHAR(64) COLLATE NOCASE        NOT NULL,
    `Passhash`       VARCHAR(64) COLLATE NOCASE        NOT NULL,
    `Country`        INTEGER                           NOT NULL,
    `Privilege`      INTEGER                           NOT NULL,
    `RegisterDate`   TEXT                              NOT NULL,
    `LastOnlineTime` TEXT                              NOT NULL,
    `Friends`        VARCHAR(1024) COLLATE NOCASE      NOT NULL,
    `IsRestricted`   TINYINT                           NOT NULL,
    `SilencedUntil`  TEXT                              NOT NULL
);

CREATE TABLE `user_file`
(
    `Id`        INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL,
    `OwnerId`   INTEGER                           NOT NULL,
    `Path`      VARCHAR(64) COLLATE NOCASE        NOT NULL,
    `Type`      INTEGER                           NOT NULL,
    `CreatedAt` TEXT                              NOT NULL
);

CREATE TABLE `restriction`
(
    `Id`         INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL,
    `UserId`     INTEGER                           NOT NULL,
    `ExecutorId` INTEGER                           NOT NULL,
    `Reason`     VARCHAR(1024) COLLATE NOCASE      NOT NULL,
    `Date`       TEXT                              NOT NULL,
    `ExpiryDate` TEXT                              NOT NULL
);

CREATE TABLE `beatmap_file`
(
    `Id`           INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL,
    `BeatmapId`    INTEGER                           NOT NULL,
    `BeatmapSetId` INTEGER                           NOT NULL,
    `Path`         VARCHAR(64) COLLATE NOCASE        NOT NULL,
    `CreatedAt`    TEXT                              NOT NULL
);

CREATE TABLE `score`
(
    `Id`                INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL,
    `UserId`            INTEGER                           NOT NULL,
    `BeatmapId`         INTEGER                           NOT NULL,
    `ScoreHash`         VARCHAR(64) COLLATE NOCASE        NOT NULL,
    `BeatmapHash`       VARCHAR(64) COLLATE NOCASE        NOT NULL,
    `ReplayFileId`      INTEGER                           NOT NULL,
    `TotalScore`        INTEGER                           NOT NULL,
    `MaxCombo`          INTEGER                           NOT NULL,
    `Count300`          INTEGER                           NOT NULL,
    `Count100`          INTEGER                           NOT NULL,
    `Count50`           INTEGER                           NOT NULL,
    `CountMiss`         INTEGER                           NOT NULL,
    `CountKatu`         INTEGER                           NOT NULL,
    `CountGeki`         INTEGER                           NOT NULL,
    `Perfect`           TINYINT                           NOT NULL,
    `Mods`              INTEGER                           NOT NULL,
    `Grade`             VARCHAR(64) COLLATE NOCASE        NOT NULL,
    `IsPassed`          TINYINT                           NOT NULL,
    `GameMode`          INTEGER                           NOT NULL,
    `WhenPlayed`        TEXT                              NOT NULL,
    `OsuVersion`        VARCHAR(64) COLLATE NOCASE        NOT NULL,
    `ClientTime`        TEXT                              NOT NULL,
    `Accuracy`          DECIMAL(100, 2)                   NOT NULL,
    `PerformancePoints` DECIMAL(100, 2)                   NOT NULL
);

CREATE TABLE "user_stats"
(
    `Id`                INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL,
    `UserId`            INTEGER                           NOT NULL,
    `GameMode`          INTEGER                           NOT NULL,
    `Accuracy`          DECIMAL(100, 2)                   NOT NULL,
    `TotalScore`        REAL                              NOT NULL,
    `RankedScore`       REAL                              NOT NULL,
    `PlayCount`         INTEGER                           NOT NULL,
    `PerformancePoints` INTEGER                           NOT NULL,
    `MaxCombo`          INTEGER                           NOT NULL,
    `PlayTime`          INTEGER                           NOT NULL
)
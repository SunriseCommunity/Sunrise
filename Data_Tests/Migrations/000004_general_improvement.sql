-- 1. Update existing tables
CREATE TABLE IF NOT EXISTS `user_file_new`
(
    `Id`        INTEGER PRIMARY KEY AUTOINCREMENT  NOT NULL,
    `OwnerId`   INTEGER                            NOT NULL,
    `Path`      VARCHAR(2147483647) COLLATE NOCASE NOT NULL,
    `Type`      INTEGER                            NOT NULL,
    `CreatedAt` TEXT                               NOT NULL
);

INSERT INTO user_file_new
SELECT Id,
       OwnerId,
       Path,
       Type,
       CreatedAt
FROM user_file;
DROP TABLE user_file;
ALTER TABLE user_file_new
    RENAME TO user_file;


CREATE TABLE IF NOT EXISTS `restriction_new`
(
    `Id`         INTEGER PRIMARY KEY AUTOINCREMENT  NOT NULL,
    `UserId`     INTEGER                            NOT NULL,
    `ExecutorId` INTEGER                            NOT NULL,
    `Reason`     VARCHAR(2147483647) COLLATE NOCASE NOT NULL,
    `Date`       TEXT                               NOT NULL,
    `ExpiryDate` TEXT                               NOT NULL
);

INSERT INTO restriction_new
SELECT Id,
       UserId,
       ExecutorId,
       Reason,
       Date,
       ExpiryDate
FROM restriction;
DROP TABLE restriction;
ALTER TABLE restriction_new
    RENAME TO restriction;

CREATE TABLE IF NOT EXISTS `beatmap_file_new`
(
    `Id`           INTEGER PRIMARY KEY AUTOINCREMENT  NOT NULL,
    `BeatmapId`    INTEGER                            NOT NULL,
    `BeatmapSetId` INTEGER                            NOT NULL,
    `Path`         VARCHAR(2147483647) COLLATE NOCASE NOT NULL,
    `CreatedAt`    TEXT                               NOT NULL
);

INSERT INTO beatmap_file_new
SELECT Id,
       BeatmapId,
       BeatmapSetId,
       Path,
       CreatedAt
FROM beatmap_file;
DROP TABLE beatmap_file;
ALTER TABLE beatmap_file_new
    RENAME TO beatmap_file;

CREATE TABLE IF NOT EXISTS `user_stats_new`
(
    `Id`                  INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL,
    `UserId`              INTEGER                           NOT NULL,
    `GameMode`            INTEGER                           NOT NULL,
    `Accuracy`            REAL                              NOT NULL,
    `TotalScore`          INTEGER                           NOT NULL,
    `RankedScore`         INTEGER                           NOT NULL,
    `PlayCount`           INTEGER                           NOT NULL,
    `PerformancePoints`   INTEGER                           NOT NULL,
    `MaxCombo`            INTEGER                           NOT NULL,
    `PlayTime`            INTEGER                           NOT NULL,
    `TotalHits`           INTEGER                           NOT NULL,
    `BestGlobalRank`      BIGINT,
    `BestGlobalRankDate`  TEXT,
    `BestCountryRank`     BIGINT,
    `BestCountryRankDate` TEXT
);

INSERT INTO user_stats_new
SELECT Id,
       UserId,
       GameMode,
       Accuracy,
       TotalScore,
       RankedScore,
       PlayCount,
       PerformancePoints,
       MaxCombo,
       PlayTime,
       TotalHits,
       BestGlobalRank,
       BestGlobalRankDate,
       BestCountryRank,
       BestCountryRankDate
FROM user_stats;
DROP TABLE user_stats;
ALTER TABLE user_stats_new
    RENAME TO user_stats;

CREATE TABLE IF NOT EXISTS `user_new`
(
    `Id`             INTEGER PRIMARY KEY AUTOINCREMENT  NOT NULL,
    `Username`       VARCHAR(64) COLLATE NOCASE         NOT NULL,
    `Email`          VARCHAR(1024) COLLATE NOCASE       NOT NULL,
    `Passhash`       VARCHAR(64) COLLATE NOCASE         NOT NULL,
    `Country`        INTEGER                            NOT NULL,
    `Privilege`      INTEGER                            NOT NULL,
    `RegisterDate`   TEXT                               NOT NULL,
    `LastOnlineTime` TEXT                               NOT NULL,
    `Friends`        VARCHAR(2147483647) COLLATE NOCASE NOT NULL,
    `IsRestricted`   TINYINT                            NOT NULL,
    `SilencedUntil`  TEXT                               NOT NULL
);

INSERT INTO user_new
SELECT Id,
       Username,
       Email,
       Passhash,
       Country,
       Privilege,
       RegisterDate,
       LastOnlineTime,
       Friends,
       IsRestricted,
       SilencedUntil
FROM user;

DROP TABLE user;
ALTER TABLE user_new
    RENAME TO user;



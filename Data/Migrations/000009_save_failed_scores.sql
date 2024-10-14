-- 1. Update existing tables
CREATE TABLE IF NOT EXISTS `score_new`
(
    `Id`                INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL,
    `UserId`            INTEGER                           NOT NULL,
    `BeatmapId`         INTEGER                           NOT NULL,
    `ScoreHash`         VARCHAR(64) COLLATE NOCASE        NOT NULL,
    `BeatmapHash`       VARCHAR(64) COLLATE NOCASE        NOT NULL,
    `ReplayFileId`      INTEGER,
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
    `IsRanked`          TINYINT                           NOT NULL,
    `GameMode`          INTEGER                           NOT NULL,
    `WhenPlayed`        TEXT                              NOT NULL,
    `OsuVersion`        VARCHAR(64) COLLATE NOCASE        NOT NULL,
    `ClientTime`        TEXT                              NOT NULL,
    `Accuracy`          DECIMAL(100, 2)                   NOT NULL,
    `PerformancePoints` DECIMAL(100, 2)                   NOT NULL
);

INSERT INTO score_new
SELECT Id,
       UserId,
       BeatmapId,
       ScoreHash,
       BeatmapHash,
       ReplayFileId,
       TotalScore,
       MaxCombo,
       Count300,
       Count100,
       Count50,
       CountMiss,
       CountKatu,
       CountGeki,
       Perfect,
       Mods,
       Grade,
       IsPassed,
       IsRanked,
       GameMode,
       WhenPlayed,
       OsuVersion,
       ClientTime,
       Accuracy,
       PerformancePoints
FROM score;
DROP TABLE score;
ALTER TABLE score_new
    RENAME TO score;


-- 1. Update existing tables
-- -- Update type of TotalScore, RankedScore, from INTEGER to BIGINT
CREATE TABLE "user_stats_new"
(
    `Id`                  INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL,
    `UserId`              INTEGER                           NOT NULL,
    `GameMode`            INTEGER                           NOT NULL,
    `Accuracy`            REAL                              NOT NULL,
    `TotalScore`          BIGINT                            NOT NULL,
    `RankedScore`         BIGINT                            NOT NULL,
    `PlayCount`           INTEGER                           NOT NULL,
    `PerformancePoints`   REAL                              NOT NULL,
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

-- -- Update type of TotalScore from INTEGER to BIGINT
CREATE TABLE IF NOT EXISTS `score_new`
(
    `Id`                INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL,
    `UserId`            INTEGER                           NOT NULL,
    `BeatmapId`         INTEGER                           NOT NULL,
    `ScoreHash`         VARCHAR(64) COLLATE NOCASE        NOT NULL,
    `BeatmapHash`       VARCHAR(64) COLLATE NOCASE        NOT NULL,
    `ReplayFileId`      INTEGER,
    `TotalScore`        BIGINT                            NOT NULL,
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
    `IsScoreable`       TINYINT                           NOT NULL,
    `SubmissionStatus`  INTEGER                           NOT NULL,
    `GameMode`          INTEGER                           NOT NULL,
    `WhenPlayed`        TEXT                              NOT NULL,
    `OsuVersion`        VARCHAR(64) COLLATE NOCASE        NOT NULL,
    `BeatmapStatus`     INTEGER                           NOT NULL,
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
       IsScoreable,
       SubmissionStatus,
       GameMode,
       WhenPlayed,
       OsuVersion,
       BeatmapStatus,
       ClientTime,
       Accuracy,
       PerformancePoints
FROM score;

DROP TABLE score;
ALTER TABLE score_new
    RENAME TO score;
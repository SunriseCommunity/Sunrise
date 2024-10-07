-- 1. Update existing tables
CREATE TABLE IF NOT EXISTS `user_stats_new`
(
    `Id`                  INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL,
    `UserId`              INTEGER                           NOT NULL,
    `GameMode`            INTEGER                           NOT NULL,
    `Accuracy`            DECIMAL(100, 2)                   NOT NULL,
    `TotalScore`          REAL                              NOT NULL,
    `RankedScore`         REAL                              NOT NULL,
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
       NULL,
       NULL,
       NULL,
       NULL
FROM user_stats;
DROP TABLE user_stats;
ALTER TABLE user_stats_new
    RENAME TO user_stats;

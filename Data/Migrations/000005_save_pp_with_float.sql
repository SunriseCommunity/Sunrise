-- 1. Update existing tables
CREATE TABLE IF NOT EXISTS `user_stats_new`
(
    `Id`                  INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL,
    `UserId`              INTEGER                           NOT NULL,
    `GameMode`            INTEGER                           NOT NULL,
    `Accuracy`            REAL                              NOT NULL,
    `TotalScore`          INTEGER                           NOT NULL,
    `RankedScore`         INTEGER                           NOT NULL,
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



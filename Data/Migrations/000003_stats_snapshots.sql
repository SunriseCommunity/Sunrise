-- 1. Add new table 
CREATE TABLE IF NOT EXISTS `user_stats_snapshot`
(
    `Id`            INTEGER PRIMARY KEY AUTOINCREMENT  NOT NULL,
    `UserId`        INTEGER                            NOT NULL,
    `GameMode`      INTEGER                            NOT NULL,
    `SnapshotsJson` VARCHAR(2147483647) COLLATE NOCASE NOT NULL
);

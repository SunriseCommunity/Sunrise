CREATE TABLE IF NOT EXISTS `migration`
(
    `Id`        INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL,
    `Name`      VARCHAR(1024) COLLATE NOCASE      NOT NULL,
    `AppliedAt` TEXT                              NOT NULL
)
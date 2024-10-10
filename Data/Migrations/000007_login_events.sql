-- 1. Add new table 
CREATE TABLE IF NOT EXISTS `login_event`
(
    `Id`        INTEGER PRIMARY KEY AUTOINCREMENT  NOT NULL,
    `UserId`    INTEGER                            NOT NULL,
    `Ip`        VARCHAR(64) COLLATE NOCASE         NOT NULL,
    `LoginData` VARCHAR(2147483647) COLLATE NOCASE NOT NULL,
    `Time`      TEXT                               NOT NULL
)
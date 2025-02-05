-- 1. Update existing tables
CREATE TABLE IF NOT EXISTS `user_new`
(
    `Id`             INTEGER PRIMARY KEY AUTOINCREMENT  NOT NULL,
    `Username`       VARCHAR(64) COLLATE NOCASE         NOT NULL,
    `Email`          VARCHAR(1024) COLLATE NOCASE       NOT NULL,
    `Passhash`       VARCHAR(64) COLLATE NOCASE         NOT NULL,
    `Description`    VARCHAR(2147483647) COLLATE NOCASE,
    `Country`        INTEGER                            NOT NULL,
    `Privilege`      INTEGER                            NOT NULL,
    `RegisterDate`   TEXT                               NOT NULL,
    `LastOnlineTime` TEXT                               NOT NULL,
    `Friends`        VARCHAR(2147483647) COLLATE NOCASE NOT NULL,
    `AccountStatus`  INTEGER                            NOT NULL,
    `SilencedUntil`  TEXT                               NOT NULL
);

INSERT INTO user_new
SELECT Id,
       Username,
       Email,
       Passhash,
       Description,
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



-- 1. Update existing tables
CREATE TABLE IF NOT EXISTS `event_user`
(
    `Id`        INTEGER PRIMARY KEY AUTOINCREMENT  NOT NULL,
    `UserId`    INTEGER                            NOT NULL,
    `EventType` INTEGER                            NOT NULL,
    `Ip`        VARCHAR(64) COLLATE NOCASE         NOT NULL,
    `JsonData`  VARCHAR(2147483647) COLLATE NOCASE NOT NULL,
    `Time`      TEXT                               NOT NULL
);

INSERT INTO event_user
SELECT Id,
       UserId,
       0,
       Ip,
       '{ "LoginData": "' || replace(LoginData, char(10), '\n') || '" }',
       Time
FROM login_event;

DROP TABLE login_event;




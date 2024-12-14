CREATE TABLE IF NOT EXISTS `user_file_new`
(
    `Id`        INTEGER PRIMARY KEY AUTOINCREMENT  NOT NULL,
    `OwnerId`   INTEGER                            NOT NULL,
    `Path`      VARCHAR(2147483647) COLLATE NOCASE NOT NULL,
    `Type`      INTEGER                            NOT NULL,
    `CreatedAt` TEXT                               NOT NULL,
    `UpdatedAt` TEXT                               NOT NULL
);

INSERT INTO user_file_new
SELECT Id,
       OwnerId,
       Path,
       Type,
       CreatedAt,
       CreatedAt
FROM user_file;

DROP TABLE user_file;
ALTER TABLE user_file_new
    RENAME TO user_file;

DELETE FROM user_file
WHERE Id IN (
    SELECT Id
    FROM (
        SELECT Id, 
               ROW_NUMBER() OVER (PARTITION BY OwnerId, Type ORDER BY CreatedAt ASC) AS rn

        FROM user_file
    )
    WHERE rn > 1
);
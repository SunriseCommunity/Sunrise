-- -- Use this if you can't migrate to EF due to any DB errors

-- Remove duplicates
WITH DuplicateBeatmapFileRows AS (
    SELECT
        ROW_NUMBER() OVER (PARTITION BY BeatmapId ORDER BY Id) AS RowNum,
        Id
    FROM beatmap_file
)

DELETE FROM beatmap_file
WHERE Id IN (
    SELECT Id
    FROM DuplicateBeatmapFileRows
    WHERE RowNum > 1
);
SELECT 'Deleted from beatmap_file' AS TableName, changes() AS DeletedRows;

WITH DuplicateScoreRows AS (
    SELECT
        ROW_NUMBER() OVER (PARTITION BY ScoreHash ORDER BY Id) AS RowNum,
        Id
    FROM score
)

DELETE FROM score
WHERE Id IN (
    SELECT Id
    FROM DuplicateScoreRows
    WHERE RowNum > 1
);
SELECT 'Deleted from score' AS TableName, changes() AS DeletedRows;

-- Remove deleted users data;
DELETE FROM user_stats_snapshot
WHERE UserId NOT IN (SELECT Id FROM user);
SELECT 'Deleted from user_stats_snapshot' AS TableName, changes() AS DeletedRows;

DELETE FROM user_stats
WHERE UserId NOT IN (SELECT Id FROM user);
SELECT 'Deleted from user_stats' AS TableName, changes() AS DeletedRows;

DELETE FROM restriction
WHERE UserId NOT IN (SELECT Id FROM user);
SELECT 'Deleted from restriction' AS TableName, changes() AS DeletedRows;

DELETE FROM score
WHERE UserId NOT IN (SELECT Id FROM user);
SELECT 'Deleted from score' AS TableName, changes() AS DeletedRows;

DELETE FROM event_user
WHERE UserId NOT IN (SELECT Id FROM user);
SELECT 'Deleted from event_user' AS TableName, changes() AS DeletedRows;

DELETE FROM user_medals
WHERE UserId NOT IN (SELECT Id FROM user);
SELECT 'Deleted from user_medals' AS TableName, changes() AS DeletedRows;

DELETE FROM user_favourite_beatmap
WHERE UserId NOT IN (SELECT Id FROM user);
SELECT 'Deleted from user_favourite_beatmap' AS TableName, changes() AS DeletedRows;

DELETE FROM user_file
WHERE OwnerId NOT IN (SELECT Id FROM user);
SELECT 'Deleted from user_file' AS TableName, changes() AS DeletedRows;

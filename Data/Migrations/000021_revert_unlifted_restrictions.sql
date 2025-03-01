-- Restricts all accidentally unrestricted users which were disabled

UPDATE user
SET AccountStatus = 1
WHERE Id IN (
    SELECT UserId FROM restriction 
) AND AccountStatus = 2;

SELECT 'Restrictions reverted'  AS TableName, changes() AS RevertedRestrictionsRows;
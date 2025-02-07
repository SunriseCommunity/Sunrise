
-- Remove the './Data/' prefix from the Path column in the medal_file table
UPDATE medal_file SET Path = REPLACE(Path, './Data/', '') WHERE Path LIKE './Data/%';

-- Remove the './Data/' prefix from the Path column in the user_file table
UPDATE user_file SET Path = REPLACE(Path, './Data/', '') WHERE Path LIKE './Data/%';

-- Remove the './Data/' prefix from the Path column in the beatmap_file table
UPDATE beatmap_file SET Path = REPLACE(Path, './Data/', '') WHERE Path LIKE './Data/%';
-- 1. Update existing tables
UPDATE medal
SET Condition =
        CASE
            WHEN Condition LIKE 'beatmap.DifficultyRating >= %' THEN
                Condition || ' && beatmap.DifficultyRating < ' ||
                (CAST(SUBSTR(Condition, INSTR(Condition, '>=') + 3) AS INTEGER) + 1)
            ELSE Condition
            END
WHERE Category = 4
  AND Condition LIKE 'beatmap.DifficultyRating >= %';

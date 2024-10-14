-- 1. Add new table 
CREATE TABLE IF NOT EXISTS `user_favourite_beatmap`
(
    `Id`           INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL,
    `UserId`       INTEGER                           NOT NULL,
    `BeatmapSetId` INTEGER                           NOT NULL,
    `DateAdded`    TEXT                              NOT NULL
)
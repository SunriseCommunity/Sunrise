CREATE TABLE IF NOT EXISTS `medal`
(
    `Id`          INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL,
    `Name`        VARCHAR(1024) COLLATE NOCASE      NOT NULL,
    `Description` VARCHAR(1024) COLLATE NOCASE      NOT NULL,
    `GameMode`    INTEGER,
    `Category`    INTEGER                           NOT NULL,
    `FileUrl`     VARCHAR(1024) COLLATE NOCASE,
    `FileId`      INTEGER,
    `Condition`   VARCHAR(1024) COLLATE NOCASE      NOT NULL
);

CREATE TABLE IF NOT EXISTS `medal_file`
(
    `Id`        INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL,
    `MedalId`   INTEGER                           NOT NULL,
    `Path`      VARCHAR(1024) COLLATE NOCASE      NOT NULL,
    `CreatedAt` TEXT                              NOT NULL
);

CREATE TABLE IF NOT EXISTS `user_medals`
(
    `Id`         INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL,
    `UserId`     INTEGER                           NOT NULL,
    `MedalId`    INTEGER                           NOT NULL,
    `UnlockedAt` TEXT                              NOT NULL
);

-- skill
-- -- std
INSERT INTO medal (Name, Description, GameMode, Category, FileUrl, Condition)
VALUES ('Rising Star', 'Can''t go forward without the first steps.', 0, 4, 'osu-skill-pass-1',
        'beatmap.DifficultyRating >= 1');
INSERT INTO medal (Name, Description, GameMode, Category, FileUrl, Condition)
VALUES ('Constellation Prize', 'Definitely not a consolation prize. Now things start getting hard!', 0, 4,
        'osu-skill-pass-2', 'beatmap.DifficultyRating >= 2');
INSERT INTO medal (Name, Description, GameMode, Category, FileUrl, Condition)
VALUES ('Building Confidence', 'Oh, you''ve SO got this.', 0, 4, 'osu-skill-pass-3', 'beatmap.DifficultyRating >= 3');
INSERT INTO medal (Name, Description, GameMode, Category, FileUrl, Condition)
VALUES ('Insanity Approaches', 'You''re not twitching, you''re just ready.', 0, 4, 'osu-skill-pass-4',
        'beatmap.DifficultyRating >= 4');
INSERT INTO medal (Name, Description, GameMode, Category, FileUrl, Condition)
VALUES ('These Clarion Skies', 'Everything seems so clear now.', 0, 4, 'osu-skill-pass-5',
        'beatmap.DifficultyRating >= 5');
INSERT INTO medal (Name, Description, GameMode, Category, FileUrl, Condition)
VALUES ('Above and Beyond', 'A cut above the rest.', 0, 4, 'osu-skill-pass-6', 'beatmap.DifficultyRating >= 6');
INSERT INTO medal (Name, Description, GameMode, Category, FileUrl, Condition)
VALUES ('Supremacy', 'All marvel before your prowess.', 0, 4, 'osu-skill-pass-7', 'beatmap.DifficultyRating >= 7');
INSERT INTO medal (Name, Description, GameMode, Category, FileUrl, Condition)
VALUES ('Absolution', 'My god, you''re full of stars!', 0, 4, 'osu-skill-pass-8', 'beatmap.DifficultyRating >= 8');
INSERT INTO medal (Name, Description, GameMode, Category, FileUrl, Condition)
VALUES ('Event Horizon', 'No force dares to pull you under.', 0, 4, 'osu-skill-pass-9',
        'beatmap.DifficultyRating >= 9');
INSERT INTO medal (Name, Description, GameMode, Category, FileUrl, Condition)
VALUES ('Phantasm', 'Fevered is your passion, extraordinary is your skill.', 0, 4, 'osu-skill-pass-10',
        'beatmap.DifficultyRating >= 10');

INSERT INTO medal (Name, Description, GameMode, Category, FileUrl, Condition)
VALUES ('Totality', 'All the notes. Every single one.', 0, 4, 'osu-skill-fc-1',
        'beatmap.DifficultyRating >= 1 && score.Perfect');
INSERT INTO medal (Name, Description, GameMode, Category, FileUrl, Condition)
VALUES ('Business As Usual', 'Two to go, please.', 0, 4, 'osu-skill-fc-2',
        'beatmap.DifficultyRating >= 2 && score.Perfect');
INSERT INTO medal (Name, Description, GameMode, Category, FileUrl, Condition)
VALUES ('Building Steam', 'Hey, this isn''t so bad.', 0, 4, 'osu-skill-fc-3',
        'beatmap.DifficultyRating >= 3 && score.Perfect');
INSERT INTO medal (Name, Description, GameMode, Category, FileUrl, Condition)
VALUES ('Moving Forward', 'Bet you feel good about that.', 0, 4, 'osu-skill-fc-4',
        'beatmap.DifficultyRating >= 4 && score.Perfect');
INSERT INTO medal (Name, Description, GameMode, Category, FileUrl, Condition)
VALUES ('Paradigm Shift', 'Surprisingly difficult.', 0, 4, 'osu-skill-fc-5',
        'beatmap.DifficultyRating >= 5 && score.Perfect');
INSERT INTO medal (Name, Description, GameMode, Category, FileUrl, Condition)
VALUES ('Anguish Quelled', 'Don''t choke.', 0, 4, 'osu-skill-fc-6', 'beatmap.DifficultyRating >= 6 && score.Perfect');
INSERT INTO medal (Name, Description, GameMode, Category, FileUrl, Condition)
VALUES ('Never Give Up', 'Excellence is its own reward.', 0, 4, 'osu-skill-fc-7',
        'beatmap.DifficultyRating >= 7 && score.Perfect');
INSERT INTO medal (Name, Description, GameMode, Category, FileUrl, Condition)
VALUES ('Aberration', 'They said it couldn''t be done. They were wrong.', 0, 4, 'osu-skill-fc-8',
        'beatmap.DifficultyRating >= 8 && score.Perfect');
INSERT INTO medal (Name, Description, GameMode, Category, FileUrl, Condition)
VALUES ('Chosen', 'Reign among the Prometheans, where you belong.', 0, 4, 'osu-skill-fc-9',
        'beatmap.DifficultyRating >= 9 && score.Perfect');
INSERT INTO medal (Name, Description, GameMode, Category, FileUrl, Condition)
VALUES ('Unfathomable', 'You have no equal.', 0, 4, 'osu-skill-fc-10',
        'beatmap.DifficultyRating >= 10 && score.Perfect');

INSERT INTO medal (Name, Description, GameMode, Category, FileUrl, Condition)
VALUES ('500 Combo', '500 big ones! You''re moving up in the world!', 0, 4, 'osu-combo-500', 'score.MaxCombo >= 500');
INSERT INTO medal (Name, Description, GameMode, Category, FileUrl, Condition)
VALUES ('750 Combo', '750 notes back to back? Woah.', 0, 4, 'osu-combo-750', 'score.MaxCombo >= 750');
INSERT INTO medal (Name, Description, GameMode, Category, FileUrl, Condition)
VALUES ('1,000 Combo', 'A thousand reasons why you rock at this game.', 0, 4, 'osu-combo-1000',
        'score.MaxCombo >= 1000');
INSERT INTO medal (Name, Description, GameMode, Category, FileUrl, Condition)
VALUES ('2,000 Combo', 'Nothing can stop you now.', 0, 4, 'osu-combo-2000', 'score.MaxCombo >= 2000');

INSERT INTO medal (Name, Description, GameMode, Category, FileUrl, Condition)
VALUES ('5,000 Plays', 'There''s a lot more where that came from.', 0, 4, 'osu-plays-5000', 'user.PlayCount >= 5000');
INSERT INTO medal (Name, Description, GameMode, Category, FileUrl, Condition)
VALUES ('15,000 Plays', 'Must.. click.. circles..', 0, 4, 'osu-plays-15000', 'user.PlayCount >= 15000');
INSERT INTO medal (Name, Description, GameMode, Category, FileUrl, Condition)
VALUES ('25,000 Plays', 'There''s no going back.', 0, 4, 'osu-plays-25000', 'user.PlayCount >= 25000');
INSERT INTO medal (Name, Description, GameMode, Category, FileUrl, Condition)
VALUES ('50,000 Plays', 'You''re here forever.', 0, 4, 'osu-plays-50000', 'user.PlayCount >= 50000');

-- -- taiko
INSERT INTO medal (Name, Description, GameMode, Category, FileUrl, Condition)
VALUES ('My First Don', 'Marching to the beat of your own drum. Literally.', 1, 4, 'taiko-skill-pass-1',
        'beatmap.DifficultyRating >= 1');
INSERT INTO medal (Name, Description, GameMode, Category, FileUrl, Condition)
VALUES ('Katsu Katsu Katsu', 'Hora! Ikuzo!', 1, 4, 'taiko-skill-pass-2', 'beatmap.DifficultyRating >= 2');
INSERT INTO medal (Name, Description, GameMode, Category, FileUrl, Condition)
VALUES ('Not Even Trying', 'Muzukashii? Not even.', 1, 4, 'taiko-skill-pass-3', 'beatmap.DifficultyRating >= 3');
INSERT INTO medal (Name, Description, GameMode, Category, FileUrl, Condition)
VALUES ('Face Your Demons', 'The first trials are now behind you, but are you a match for the Oni?', 1, 4,
        'taiko-skill-pass-4', 'beatmap.DifficultyRating >= 4');
INSERT INTO medal (Name, Description, GameMode, Category, FileUrl, Condition)
VALUES ('The Demon Within', 'No rest for the wicked.', 1, 4, 'taiko-skill-pass-5', 'beatmap.DifficultyRating >= 5');
INSERT INTO medal (Name, Description, GameMode, Category, FileUrl, Condition)
VALUES ('Drumbreaker', 'Too strong.', 1, 4, 'taiko-skill-pass-6', 'beatmap.DifficultyRating >= 6');
INSERT INTO medal (Name, Description, GameMode, Category, FileUrl, Condition)
VALUES ('The Godfather', 'You are the Don of Dons.', 1, 4, 'taiko-skill-pass-7', 'beatmap.DifficultyRating >= 7');
INSERT INTO medal (Name, Description, GameMode, Category, FileUrl, Condition)
VALUES ('Rhythm Incarnate', 'Feel the beat. Become the beat.', 1, 4, 'taiko-skill-pass-8',
        'beatmap.DifficultyRating >= 8');

INSERT INTO medal (Name, Description, GameMode, Category, FileUrl, Condition)
VALUES ('Keeping Time', 'Don, then katsu. Don, then katsu..', 1, 4, 'taiko-skill-fc-1',
        'beatmap.DifficultyRating >= 1 && score.Perfect');
INSERT INTO medal (Name, Description, GameMode, Category, FileUrl, Condition)
VALUES ('To Your Own Beat', 'Straight and steady.', 1, 4, 'taiko-skill-fc-2',
        'beatmap.DifficultyRating >= 2 && score.Perfect');
INSERT INTO medal (Name, Description, GameMode, Category, FileUrl, Condition)
VALUES ('Big Drums', 'Bigger scores to match.', 1, 4, 'taiko-skill-fc-3',
        'beatmap.DifficultyRating >= 3 && score.Perfect');
INSERT INTO medal (Name, Description, GameMode, Category, FileUrl, Condition)
VALUES ('Adversity Overcome', 'Difficult? Not for you.', 1, 4, 'taiko-skill-fc-4',
        'beatmap.DifficultyRating >= 4 && score.Perfect');
INSERT INTO medal (Name, Description, GameMode, Category, FileUrl, Condition)
VALUES ('Demonslayer', 'An Oni felled forevermore.', 1, 4, 'taiko-skill-fc-5',
        'beatmap.DifficultyRating >= 5 && score.Perfect');
INSERT INTO medal (Name, Description, GameMode, Category, FileUrl, Condition)
VALUES ('Rhythm''s Call', 'Heralding true skill.', 1, 4, 'taiko-skill-fc-6',
        'beatmap.DifficultyRating >= 6 && score.Perfect');
INSERT INTO medal (Name, Description, GameMode, Category, FileUrl, Condition)
VALUES ('Time Everlasting', 'Not a single beat escapes you.', 1, 4, 'taiko-skill-fc-7',
        'beatmap.DifficultyRating >= 7 && score.Perfect');
INSERT INTO medal (Name, Description, GameMode, Category, FileUrl, Condition)
VALUES ('The Drummer''s Throne', 'Percussive brilliance befitting royalty alone.', 1, 4, 'taiko-skill-fc-8',
        'beatmap.DifficultyRating >= 8 && score.Perfect');

-- TODO: Set condition
-- INSERT INTO medal (Name, Description, GameMode, Category, FileUrl, Condition) VALUES ('30,000 Drum Hits', 'Did that drum have a face?', 1, 4, 'taiko-hits-30000', '');
-- INSERT INTO medal (Name, Description, GameMode, Category, FileUrl, Condition) VALUES ('300,000 Drum Hits', 'The rhythm never stops.', 1, 4, 'taiko-hits-300000', '');
-- INSERT INTO medal (Name, Description, GameMode, Category, FileUrl, Condition) VALUES ('3,000,000 Drum Hits', 'Truly, the Don of dons.', 1, 4, 'taiko-hits-3000000', '');
-- INSERT INTO medal (Name, Description, GameMode, Category, FileUrl, Condition) VALUES ('30,000,000 Drum Hits', 'Your rhythm, eternal.', 1, 4, 'taiko-hits-30000000', '');

-- -- fruits
INSERT INTO medal (Name, Description, GameMode, Category, FileUrl, Condition)
VALUES ('A Slice Of Life', 'Hey, this fruit catching business isn''t bad.', 2, 4, 'fruits-skill-pass-1',
        'beatmap.DifficultyRating >= 1');
INSERT INTO medal (Name, Description, GameMode, Category, FileUrl, Condition)
VALUES ('Dashing Ever Forward', 'Fast is how you do it.', 2, 4, 'fruits-skill-pass-2', 'beatmap.DifficultyRating >= 2');
INSERT INTO medal (Name, Description, GameMode, Category, FileUrl, Condition)
VALUES ('Zesty Disposition', 'No scurvy for you, not with that much fruit.', 2, 4, 'fruits-skill-pass-3',
        'beatmap.DifficultyRating >= 3');
INSERT INTO medal (Name, Description, GameMode, Category, FileUrl, Condition)
VALUES ('Hyperdash ON!', 'Time and distance is no obstacle to you.', 2, 4, 'fruits-skill-pass-4',
        'beatmap.DifficultyRating >= 4');
INSERT INTO medal (Name, Description, GameMode, Category, FileUrl, Condition)
VALUES ('It''s Raining Fruit', 'And you can catch them all.', 2, 4, 'fruits-skill-pass-5',
        'beatmap.DifficultyRating >= 5');
INSERT INTO medal (Name, Description, GameMode, Category, FileUrl, Condition)
VALUES ('Fruit Ninja', 'Legendary techniques.', 2, 4, 'fruits-skill-pass-6', 'beatmap.DifficultyRating >= 6');
INSERT INTO medal (Name, Description, GameMode, Category, FileUrl, Condition)
VALUES ('Dreamcatcher', 'No fruit, only dreams now.', 2, 4, 'fruits-skill-pass-7', 'beatmap.DifficultyRating >= 7');
INSERT INTO medal (Name, Description, GameMode, Category, FileUrl, Condition)
VALUES ('Lord of the Catch', 'Your kingdom kneels before you.', 2, 4, 'fruits-skill-pass-8',
        'beatmap.DifficultyRating >= 8');

INSERT INTO medal (Name, Description, GameMode, Category, FileUrl, Condition)
VALUES ('Sweet And Sour', 'Apples and oranges, literally.', 2, 4, 'fruits-skill-fc-1',
        'beatmap.DifficultyRating >= 1 && score.Perfect');
INSERT INTO medal (Name, Description, GameMode, Category, FileUrl, Condition)
VALUES ('Reaching The Core', 'The seeds of future success.', 2, 4, 'fruits-skill-fc-2',
        'beatmap.DifficultyRating >= 2 && score.Perfect');
INSERT INTO medal (Name, Description, GameMode, Category, FileUrl, Condition)
VALUES ('Clean Platter', 'Clean only of failure. It is completely full, otherwise.', 2, 4, 'fruits-skill-fc-3',
        'beatmap.DifficultyRating >= 3 && score.Perfect');
INSERT INTO medal (Name, Description, GameMode, Category, FileUrl, Condition)
VALUES ('Between The Rain', 'No umbrella needed.', 2, 4, 'fruits-skill-fc-4',
        'beatmap.DifficultyRating >= 4 && score.Perfect');
INSERT INTO medal (Name, Description, GameMode, Category, FileUrl, Condition)
VALUES ('Addicted', 'That was an overdose?', 2, 4, 'fruits-skill-fc-5',
        'beatmap.DifficultyRating >= 5 && score.Perfect');
INSERT INTO medal (Name, Description, GameMode, Category, FileUrl, Condition)
VALUES ('Quickening', 'A dash above normal limits.', 2, 4, 'fruits-skill-fc-6',
        'beatmap.DifficultyRating >= 6 && score.Perfect');
INSERT INTO medal (Name, Description, GameMode, Category, FileUrl, Condition)
VALUES ('Supersonic', 'Faster than is reasonably necessary.', 2, 4, 'fruits-skill-fc-7',
        'beatmap.DifficultyRating >= 7 && score.Perfect');
INSERT INTO medal (Name, Description, GameMode, Category, FileUrl, Condition)
VALUES ('Dashing Scarlet', 'Speed beyond mortal reckoning.', 2, 4, 'fruits-skill-fc-8',
        'beatmap.DifficultyRating >= 8 && score.Perfect');

-- TODO: Set condition
-- INSERT INTO medal (Name, Description, GameMode, Category, FileUrl, Condition) VALUES ('Catch 20,000 fruits', 'That is a lot of dietary fiber.', 2, 4, 'fruits-hits-20000', '');
-- INSERT INTO medal (Name, Description, GameMode, Category, FileUrl, Condition) VALUES ('Catch 200,000 fruits', 'So, I heard you like fruit...', 2, 4, 'fruits-hits-200000', '');
-- INSERT INTO medal (Name, Description, GameMode, Category, FileUrl, Condition) VALUES ('Catch 2,000,000 fruits', 'Downright healthy.', 2, 4, 'fruits-hits-2000000', '');
-- INSERT INTO medal (Name, Description, GameMode, Category, FileUrl, Condition) VALUES ('Catch 20,000,000 fruits', 'Nothing left behind.', 2, 4, 'fruits-hits-20000000', '');

-- -- mania
INSERT INTO medal (Name, Description, GameMode, Category, FileUrl, Condition)
VALUES ('First Steps', 'It isn''t 9-to-4, but 1-to-9. Keys, that is.', 3, 4, 'mania-skill-pass-1',
        'beatmap.DifficultyRating >= 1');
INSERT INTO medal (Name, Description, GameMode, Category, FileUrl, Condition)
VALUES ('No Normal Player', 'Not anymore, at least.', 3, 4, 'mania-skill-pass-2', 'beatmap.DifficultyRating >= 2');
INSERT INTO medal (Name, Description, GameMode, Category, FileUrl, Condition)
VALUES ('Impulse Drive', 'Not quite hyperspeed, but getting close.', 3, 4, 'mania-skill-pass-3',
        'beatmap.DifficultyRating >= 3');
INSERT INTO medal (Name, Description, GameMode, Category, FileUrl, Condition)
VALUES ('Hyperspeed', 'Woah.', 3, 4, 'mania-skill-pass-4', 'beatmap.DifficultyRating >= 4');
INSERT INTO medal (Name, Description, GameMode, Category, FileUrl, Condition)
VALUES ('Ever Onwards', 'Another challenge is just around the corner.', 3, 4, 'mania-skill-pass-5',
        'beatmap.DifficultyRating >= 5');
INSERT INTO medal (Name, Description, GameMode, Category, FileUrl, Condition)
VALUES ('Another Surpassed', 'Is there no limit to your skills?', 3, 4, 'mania-skill-pass-6',
        'beatmap.DifficultyRating >= 6');
INSERT INTO medal (Name, Description, GameMode, Category, FileUrl, Condition)
VALUES ('Extra Credit', 'See me after class.', 3, 4, 'mania-skill-pass-7', 'beatmap.DifficultyRating >= 7');
INSERT INTO medal (Name, Description, GameMode, Category, FileUrl, Condition)
VALUES ('Maniac', 'There''s just no stopping you.', 3, 4, 'mania-skill-pass-8', 'beatmap.DifficultyRating >= 8');

INSERT INTO medal (Name, Description, GameMode, Category, FileUrl, Condition)
VALUES ('Keystruck', 'The beginning of a new story.', 3, 4, 'mania-skill-fc-1',
        'beatmap.DifficultyRating >= 1 && score.Perfect');
INSERT INTO medal (Name, Description, GameMode, Category, FileUrl, Condition)
VALUES ('Keying In', 'Finding your groove.', 3, 4, 'mania-skill-fc-2',
        'beatmap.DifficultyRating >= 2 && score.Perfect');
INSERT INTO medal (Name, Description, GameMode, Category, FileUrl, Condition)
VALUES ('Hyperflow', 'You can *feel* the rhythm.', 3, 4, 'mania-skill-fc-3',
        'beatmap.DifficultyRating >= 3 && score.Perfect');
INSERT INTO medal (Name, Description, GameMode, Category, FileUrl, Condition)
VALUES ('Breakthrough', 'Many skills mastered, rolled into one.', 3, 4, 'mania-skill-fc-4',
        'beatmap.DifficultyRating >= 4 && score.Perfect');
INSERT INTO medal (Name, Description, GameMode, Category, FileUrl, Condition)
VALUES ('Everything Extra', 'Giving your all is giving everything you have.', 3, 4, 'mania-skill-fc-5',
        'beatmap.DifficultyRating >= 5 && score.Perfect');
INSERT INTO medal (Name, Description, GameMode, Category, FileUrl, Condition)
VALUES ('Level Breaker', 'Finesse beyond reason.', 3, 4, 'mania-skill-fc-6',
        'beatmap.DifficultyRating >= 6 && score.Perfect');
INSERT INTO medal (Name, Description, GameMode, Category, FileUrl, Condition)
VALUES ('Step Up', 'A precipice rarely seen.', 3, 4, 'mania-skill-fc-7',
        'beatmap.DifficultyRating >= 7 && score.Perfect');
INSERT INTO medal (Name, Description, GameMode, Category, FileUrl, Condition)
VALUES ('Behind The Veil', 'Supernatural!', 3, 4, 'mania-skill-fc-8', 'beatmap.DifficultyRating >= 8 && score.Perfect');

-- TODO: Set condition
-- INSERT INTO medal (Name, Description, GameMode, Category, FileUrl, Condition) VALUES ('40,000 Keys', 'Just the start of the rainbow.', 3, 4, 'mania-hits-40000', '');
-- INSERT INTO medal (Name, Description, GameMode, Category, FileUrl, Condition) VALUES ('400,000 Keys', 'Four hundred thousand and still not even close.', 3, 4, 'mania-hits-400000', '');
-- INSERT INTO medal (Name, Description, GameMode, Category, FileUrl, Condition) VALUES ('4,000,000 Keys', 'Is this the end of the rainbow?', 3, 4, 'mania-hits-4000000', '');
-- INSERT INTO medal (Name, Description, GameMode, Category, FileUrl, Condition) VALUES ('40,000,000 Keys', 'The rainbow is eternal.', 3, 4, 'mania-hits-40000000', '');

-- mods intro
INSERT INTO medal (Name, Description, Category, FileUrl, Condition)
VALUES ('Finality', 'High stakes, no regrets.', 3, 'all-intro-suddendeath', '(score.Mods & 32) == 32');
INSERT INTO medal (Name, Description, Category, FileUrl, Condition)
VALUES ('Perfectionist', 'Accept nothing but the best.', 3, 'all-intro-perfect', '(score.Mods & 16384) == 16384');
INSERT INTO medal (Name, Description, Category, FileUrl, Condition)
VALUES ('Rock Around The Clock', 'You can''t stop the rock.', 3, 'all-intro-hardrock',
        '(score.Mods & 16) == 16');
INSERT INTO medal (Name, Description, Category, FileUrl, Condition)
VALUES ('Time And A Half', 'Having a right ol'' time. One and a half of them, almost.', 3, 'all-intro-doubletime',
        '(score.Mods & 64) == 64');
INSERT INTO medal (Name, Description, Category, FileUrl, Condition)
VALUES ('Sweet Rave Party', 'Founded in the fine tradition of changing things that were just fine as they were.', 3,
        'all-intro-nightcore', '(score.Mods & 512) == 512');
INSERT INTO medal (Name, Description, Category, FileUrl, Condition)
VALUES ('Blindsight', 'I can see just perfectly.', 3, 'all-intro-hidden', '(score.Mods & 8) == 8');
INSERT INTO medal (Name, Description, Category, FileUrl, Condition)
VALUES ('Are You Afraid Of The Dark?', 'Harder than it looks, probably because it''s hard to look.', 3,
        'all-intro-flashlight', '(score.Mods & 1024) == 1024');
INSERT INTO medal (Name, Description, Category, FileUrl, Condition)
VALUES ('Dial It Right Back', 'Sometimes you just want to take it easy.', 3, 'all-intro-easy',
        '(score.Mods & 2) == 2');
INSERT INTO medal (Name, Description, Category, FileUrl, Condition)
VALUES ('Risk Averse', 'Safety nets are fun!', 3, 'all-intro-nofail', '(score.Mods & 1) == 1');
INSERT INTO medal (Name, Description, Category, FileUrl, Condition)
VALUES ('Slowboat', 'You got there. Eventually.', 3, 'all-intro-halftime', '(score.Mods & 256) == 256');
INSERT INTO medal (Name, Description, Category, FileUrl, Condition)
VALUES ('Burned Out', 'One cannot always spin to win.', 3, 'all-intro-spunout', '(score.Mods & 4096) == 4096');

-- Custom medals
INSERT INTO medal_file (MedalId, Path, CreatedAt)
VALUES (last_insert_rowid() + 1, './Data/Files/Medals/all-secret-thisdjisfire.png',
        CURRENT_TIMESTAMP);

INSERT INTO medal (Name, Description, Category, FileId, Condition)
VALUES ('Man this DJ is fire', 'Just don''t listen to the original. It''s not as good.', 2, last_insert_rowid(),
        'beatmap.BeatmapsetId == 1357624');


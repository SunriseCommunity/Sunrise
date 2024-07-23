
CREATE TABLE IF NOT EXISTS "players" (
    "id" INTEGER PRIMARY KEY AUTOINCREMENT,
    "username" TEXT UNIQUE NOT NULL,
    "passhash" TEXT NOT NULL,
    "token" TEXT NOT NULL,
    "country" INTEGER NOT NULL,
    "privilege" INTEGER NOT NULL,
    "accuracy" REAL NOT NULL,
    "total_score" INTEGER NOT NULL,
    "ranked_score" INTEGER NOT NULL,
    "play_count" INTEGER NOT NULL,
    "performance_points" INTEGER NOT NULL,
    "play_time" INTEGER NOT NULL
);

CREATE TABLE IF NOT EXISTS "scores" (
    "id" INTEGER PRIMARY KEY AUTOINCREMENT,
    "user_name" INTEGER NOT NULL,
    "beatmap_id" INTEGER NOT NULL,
    "beatmap_hash" TEXT NOT NULL,
    "score" INTEGER NOT NULL,
    "max_combo" INTEGER NOT NULL,
    "accuracy" REAL NOT NULL,
    "mods" INTEGER NOT NULL,
    "rank" TEXT NOT NULL,
    "time" INTEGER NOT NULL,
    "play_mode" INTEGER NOT NULL,
    "score_checksum" UNIQUE NOT NULL,
    "replay" TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS "files" (
    "id" INTEGER PRIMARY KEY AUTOINCREMENT,
    "owner_id" INTEGER NOT NULL,
    "path" TEXT NOT NULL,
    "type" TEXT NOT NULL,
    "created_at" INTEGER NOT NULL
);
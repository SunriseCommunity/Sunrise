CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
      "MigrationId"	TEXT NOT NULL,
      "ProductVersion"	TEXT NOT NULL,
      CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY("MigrationId")
);

INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion)
VALUES ('20250226014320_MigrateFromWatsonORM', '9.0.2'); 
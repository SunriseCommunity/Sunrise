# 🌅 Sunrise - osu! private server

<p align="center">
  <img src="./readme.jpg" alt="Artwork made by torekka. We don't own the rights to this image.">
</p>

Sunrise is a private server for osu! written in C#. This repository has both endpoints for game-client and for the
website. The server is currently in development and is not yet ready for public use.

> [!NOTE]
> Why C#? :shipit: Well, because owner of this project has allergies to non-typed languages. Sad, I know.

## Features 🌟

### Core features

- [x] Login and registration system
- [x] Score submission and leaderboards
- [x] Chat implementation
- [x] Chat Bot (as a replacement for Bancho Bot)
- [x] Multiplayer
- [x] !mp commands (mostly)
- [x] Server website (located at [Sunset](https://github.com/SunriseCommunity/Sunset))
- [x] osu!Direct
- [x] Spectating
- [x] Achievements (Medals)
- [x] Rank snapshots

### Additional features

- [x] Prometheus metrics with Grafana dashboard
- [x] Rate limiter for both internal and external requests
- [x] Redis caching for faster response times
- [x] Docker support
- [x] Database migrations
- [x] Database backups

> [!IMPORTANT]
> The list of features is in priority order. The higher the feature is, the more important it is.

## Installation 📩

1. Clone the repository
2. Open the project in Visual Studio (or any other IDE)
3. To set up development environment run:
   ```bash
   docker compose -f docker-compose.dev.yml up -d
   ```
4. Set up the beatmap manager by following the instructions in
   the [Observatory repository](https://github.com/SunriseCommunity/Observatory). After setting up the beatmap manager,
   you need to set the `General:ObservatoryUrl` in the `appsettings.{Your Environment}.json` file to the address of the beatmap manager.
5. Run the project
6. (Optional) If you want to connect to the server locally, please refer to
   the [Local connection ⚙️](##local-connection)
   section.

## Local connection ⚙️

#### If you want to connect to the server locally, follow these steps:

1. Add a launch argument `-devserver sunrise.local` to your osu! shortcut.
2. Open the `hosts` file located in `C:\Windows\System32\drivers\etc\hosts` (C:\ is your system drive) with a text
   editor and add the following line:

   ```hosts
   ... (rest of the file)

   # Sunrise Web Section
   127.0.0.1 sunrise.local
   127.0.0.1 api.sunrise.local
   # Sunrise osu! Section
   127.0.0.1 osu.sunrise.local
   127.0.0.1 a.sunrise.local
   127.0.0.1 c.sunrise.local
   127.0.0.1 assets.sunrise.local
   127.0.0.1 cho.sunrise.local
   127.0.0.1 assets.sunrise.local
   127.0.0.1 c4.sunrise.local
   127.0.0.1 b.sunrise.local
   ```

> [!WARNING]
> Don't forget to save the file after editing.

3. Generate a self-signed certificate for the domain `sunrise.local` by running the following commands in the terminal:

   ```bash
   openssl req -x509 -newkey rsa:4096 -sha256 -days 3650 -nodes -keyout sunrise.local.key -out sunrise.local.crt -subj "/CN=sunrise.local" -addext "subjectAltName=DNS:sunrise.local,DNS:*.sunrise.local,IP:10.0.0.1"
   ```

4. Convert the certificate to the PKCS12 format (for ASP.Net) by running the following command in the terminal:

   ```bash
   openssl pkcs12 -export -out sunrise.local.pfx -inkey sunrise.local.key -in sunrise.local.crt -password pass:password
   ```

5. Import the certificate to the Trusted Root Certification Authorities store by running the following command in the
   terminal:

   ```bash
   certutil -addstore -f "ROOT" sunrise.local.crt
   ```

6. Run the server and navigate to `https://sunrise.local/swagger/index.html` to check if the server is running.

## Dependencies 📦

- [Observatory (beatmap manager)](https://github.com/SunriseCommunity/Observatory)
- [rosu-pp-ffi (rosu-pp bindings for C#)](https://github.com/fantasyzhjk/rosu-pp-ffi)

## Contributing 💖

If you want to contribute to the project, feel free to fork the repository and submit a pull request. We are open to any
suggestions and improvements.

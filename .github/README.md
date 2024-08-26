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
- [ ] !mp commands
- [ ] Server website (located at [Sunset](https://github.com/SunriseCommunity/Sunset))
- [x] osu!Direct
- [x] Spectating

### Additional features

- [x] Prometheus metrics with Grafana dashboard
- [x] Rate limiter for both internal and external requests
- [x] Redis caching for faster response times
- [ ] Docker support

> [!IMPORTANT]
> The list of features is in priority order. The higher the feature is, the more important it is.

## Installation 📩

1. Clone the repository
2. Open the project in Visual Studio (or any other IDE)
3. Run the project
4. (Optional) If you want to connect to the server locally, please refer to
   the [Local connection ⚙️](##local-connection)
   section.

## Local connection ⚙️

1. If you want to connect to the server locally, follow these steps: 0. Add a launch argument `-devserver sunrise.local`
   to your osu! shortcut.
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

## API Endpoints 🛜

### Locally

All endpoints can be checked after running the server locally by navigating
to `https://{your-domain}/swagger/index.html`.

### Server

In the near future, the server will be hosted on a domain and the API documentation will be available there.

## Dependencies 📦

- [rosu-pp-ffi (rosu-pp bindings for C#)](https://github.com/fantasyzhjk/rosu-pp-ffi)

## Contributing 💖

If you want to contribute to the project, feel free to fork the repository and submit a pull request. We are open to any
suggestions and improvements.

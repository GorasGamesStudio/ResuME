# Deployment

The game is a static WebGL build served by one nginx container behind the host nginx of the
studio server. Two names point to the same build and only change the default language:
`cv.gorasgames.fr` (French) and `resume.gorasgames.fr` (English).

| File | Role |
|---|---|
| `docker-compose.yml` | The `resume-web` container (nginx alpine) on `127.0.0.1:8090`, serving `current/` |
| `nginx/site.conf` | nginx inside the container: Brotli `.unityweb` files, caching |
| `nginx/host-site.conf` | Host nginx site: both names, TLS by certbot, prototype password, proxy to the container |
| `deploy.sh` | Ships `Builds/<version>/WebGL`, switches `current`, restarts the container |

First install, once, on the server, in this order:

1. DNS: two A records, `cv` and `resume`, on the `gorasgames.fr` zone, pointing to the server.
2. `deploy/deploy.sh <X.Y.Z.B>` from the project folder: it creates the folders, links `current`
   before starting the container (never start the container first: Docker would create `current/`
   as a root-owned folder) and starts `resume-web` on `127.0.0.1:8090`.
3. Copy `nginx/host-site.conf` to `/etc/nginx/sites-available/resume.conf`, link it into
   `sites-enabled`, create the password file (see the comment in the file), `nginx -t`, reload.
4. `sudo certbot --nginx -d cv.gorasgames.fr -d resume.gorasgames.fr` once the DNS answers.

Then every delivery is `deploy/deploy.sh <X.Y.Z.B>`. Server requirements: Docker with the compose
plugin, the deploying user in the `docker` group, `rsync`, port 8090 free.

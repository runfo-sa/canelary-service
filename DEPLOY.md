# Deploy de Canelary (Server + WebApp)

El workflow [`.github/workflows/ci.yml`](.github/workflows/ci.yml) despliega el stack completo — backend (`canelary-server`) y frontend (`canelary-webapp`) — como contenedores Docker orquestados con [`docker-compose.yml`](docker-compose.yml). Corre en una VM Linux dentro de la red privada, sobre un **self-hosted GitHub Actions runner** instalado en esa misma VM. El job `deploy` se dispara solo en push a `main` despues de que pasen los tres jobs de test (`build-and-test-linux`, `build-and-test-windows`, `build-webapp`).

Este documento describe los pasos **one-time** que hay que hacer en la VM para que el workflow funcione. Una vez configurada, cada push a `main` redeploya el stack automaticamente.

---

## Topologia

```
                 push a main
                      |
                      v
            GitHub Actions (cloud)
                      |
   build-and-test-linux + windows + webapp
                      |
                      v
        self-hosted runner (VM Linux)
                      |
        docker compose up -d --build
                      |
        +-------------+-------------+
        |                           |
   canelary-server            canelary-webapp
   (aspnet:10.0)              (nginx:alpine)
   host :5262                 (sin puerto en host)
        |                           |
        |                           v
        |                   Traefik (host :80)
        |                   Host=canelary.runfosa.local
        |                           |
        |   red interna docker      |   red traefik-public
        +-------- canelary ---------+
                      |
                      v
              SQL Server (rafa)
```

- **`canelary-server`**: API en .NET 10. Escucha en `:8080` dentro del contenedor, publicada al host en `:5262`. Conecta a SQL Server via la red privada de la VM.
- **`canelary-webapp`**: SPA React servida por nginx. Escucha en `:80` dentro del contenedor; **no publica puerto al host**. Se accede a traves de Traefik, que la rutea en el entrypoint `web` (host :80) cuando el `Host` header matchea `canelary.runfosa.local` (ver labels en [`docker-compose.yml`](docker-compose.yml)). Hace reverse proxy de los endpoints del backend al servicio `canelary-server` por la red docker interna `canelary` (ver [`WebApp/nginx.conf`](WebApp/nginx.conf)).
- **Traefik**: corre por fuera de este stack y es duenio de la red docker externa `traefik-public`. La WebApp se une a esa red para que Traefik la descubra via las labels.
- Los **Clients** Windows siguen llamando al Server directamente por `http://<vm>:5262`, en paralelo al acceso de la WebApp.

---

## 1. Preparar la VM Linux

Asumimos Ubuntu 22.04+ o Debian 12+. Para otras distros, adaptar los comandos de package manager.

### 1.1 Instalar Docker Engine + Compose plugin

```bash
# Repo oficial de Docker (no el de la distro, que suele estar viejo)
sudo apt-get update
sudo apt-get install -y ca-certificates curl
sudo install -m 0755 -d /etc/apt/keyrings
sudo curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
sudo chmod a+r /etc/apt/keyrings/docker.asc

echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] \
  https://download.docker.com/linux/ubuntu $(. /etc/os-release && echo "$VERSION_CODENAME") stable" | \
  sudo tee /etc/apt/sources.list.d/docker.list > /dev/null

sudo apt-get update
sudo apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
```

Verificar:

```bash
sudo docker run hello-world
sudo docker compose version    # debe imprimir Docker Compose version v2.x
```

### 1.2 Tener Traefik corriendo con la red `traefik-public`

La WebApp se publica a la red de la VM a traves de **Traefik**, que se despliega por fuera de este stack y es duenio de una red docker externa llamada `traefik-public`. El `docker-compose.yml` de este repo asume que esa red ya existe y que hay un Traefik escuchando en el entrypoint `web` (host :80).

Crear la red una sola vez si todavia no existe:

```bash
docker network create traefik-public
```

Verificar:

```bash
docker network inspect traefik-public >/dev/null && echo OK
```

El job `deploy` del workflow chequea esto en su primer step (`Verify traefik-public network exists`) y aborta con un mensaje claro si la red falta. Si tambien hace falta levantar Traefik desde cero, hacerlo antes del primer push a `main` — su configuracion concreta esta fuera del alcance de este documento.

### 1.3 Crear el usuario que corre el runner

Por seguridad, no usar `root` para el runner. Crear un usuario dedicado:

```bash
sudo useradd -m -s /bin/bash gh-runner
sudo usermod -aG docker gh-runner
```

El grupo `docker` le da permiso de usar el daemon sin `sudo`. Verificar:

```bash
sudo -iu gh-runner docker ps
sudo -iu gh-runner docker compose version
```

### 1.4 Crear el directorio de logs persistente

Los logs del Server (escritos via `CANELARY_SERVER_LOG_BASE`) se montan como volumen para sobrevivir redeploys. El `docker-compose.yml` usa un volumen named (`canelary-logs`) por defecto; si preferis bind-mount al filesystem del host:

```bash
sudo mkdir -p /var/log/canelary-server
sudo chown gh-runner:gh-runner /var/log/canelary-server
```

### 1.5 Montar el share SMB de Etiquetas

El Server lee la carpeta de Etiquetas desde `\\twinssrv\Twins\PiQuatro\Etiquetas`. Esa es una ruta UNC de Windows que Linux no entiende directamente: hay que **montar el share CIFS en el host** y bind-montearlo al contenedor. El [`docker-compose.yml`](docker-compose.yml) ya expone `/mnt/etiquetas` como `:ro` dentro del contenedor y setea `Etiquetas__Path=/mnt/etiquetas`.

Pasos one-time en la VM:

```bash
# 1. cifs-utils para soporte de mount.cifs
sudo apt-get install -y cifs-utils

# 2. Crear el mountpoint (vacio, el mount lo va a llenar)
sudo mkdir -p /mnt/etiquetas
```

Crear `/etc/cifs-credentials` con la cuenta AD de servicio que puede leer el share. Usar una cuenta dedicada (no la de un usuario humano) y darle solo permisos de lectura sobre `\\twinssrv\Twins\PiQuatro\Etiquetas`.

```bash
sudo tee /etc/cifs-credentials >/dev/null <<'EOF'
username=svc-canelary
password=<password-de-la-cuenta>
domain=<DOMINIO-AD>
EOF
sudo chmod 600 /etc/cifs-credentials
sudo chown root:root /etc/cifs-credentials
```

Resolver el UID/GID con el que va a leer el container. La imagen `aspnet:10.0` corre como root por default (UID 0), asi que `uid=0,gid=0` alcanza. Si en el futuro se cambia a un usuario no-root, ajustar estos valores al UID/GID del usuario dentro del contenedor.

Agregar a `/etc/fstab`:

```fstab
//twinssrv/Twins/PiQuatro/Etiquetas  /mnt/etiquetas  cifs  credentials=/etc/cifs-credentials,uid=0,gid=0,ro,nofail,_netdev,iocharset=utf8,vers=3.0  0  0
```

- `ro` — read-only desde la VM (el Server solo lee).
- `nofail` — no bloquear el boot si el share esta caido.
- `_netdev` — esperar la red antes de intentar montar.
- `vers=3.0` — forzar SMB 3 (mas seguro que 1.0; subir a `3.1.1` si el servidor lo soporta).

Montar y verificar:

```bash
sudo mount -a
ls /mnt/etiquetas        # debe listar los .e01
mount | grep etiquetas   # debe aparecer como cifs
```

Si el mount falla, las pistas mas comunes:
- `Permission denied` → credenciales o permisos NTFS en el share.
- `mount error(112): Host is down` → versiones SMB incompatibles; probar `vers=2.1`, `vers=3.1.1`.
- `mount error(13): Permission denied` y SElinux/AppArmor activo → revisar contexto de seguridad del mountpoint.

---

## 2. Instalar el GitHub Actions self-hosted runner

### 2.1 Generar el token de registro

En GitHub, ir a:

**Repo → Settings → Actions → Runners → New self-hosted runner → Linux**

Copiar el token de registro que aparece (es de un solo uso, vence en ~1 hora).

### 2.2 Instalar el runner en la VM

Como usuario `gh-runner`:

```bash
sudo -iu gh-runner bash

mkdir actions-runner && cd actions-runner

# La URL exacta del tarball cambia con cada release; copiar la que muestra GitHub
# en la pagina "New self-hosted runner".
curl -o actions-runner-linux-x64.tar.gz -L \
  https://github.com/actions/runner/releases/download/<VERSION>/actions-runner-linux-x64-<VERSION>.tar.gz
tar xzf actions-runner-linux-x64.tar.gz

# Registrar el runner con labels custom (canelary-prod es el que matchea el job)
./config.sh \
  --url https://github.com/<owner>/<repo> \
  --token <TOKEN-DE-GITHUB> \
  --labels canelary-prod \
  --unattended

exit
```

Los labels `self-hosted` y `linux` se agregan automaticamente; `canelary-prod` es el que permite que solo este runner tome el job de deploy.

### 2.3 Instalarlo como servicio systemd

```bash
cd /home/gh-runner/actions-runner
sudo ./svc.sh install gh-runner
sudo ./svc.sh start
sudo ./svc.sh status
```

Verificar en GitHub (**Settings → Actions → Runners**) que el runner aparece como `Idle`.

---

## 3. Preparar la base de datos SQL Server

El Server espera conectarse a SQL Server con **SQL auth** (no Windows auth como en dev).

### 3.1 Crear un usuario SQL dedicado

```sql
USE [master];
CREATE LOGIN canelary_app WITH PASSWORD = '<password-fuerte>';

USE [VisualTernera];
CREATE USER canelary_app FOR LOGIN canelary_app;

-- Permisos minimos: lectura/escritura sobre la tabla memory-optimized
ALTER ROLE db_datareader ADD MEMBER canelary_app;
ALTER ROLE db_datawriter ADD MEMBER canelary_app;
```

Si el deploy se hace desde cero, correr previamente [`deploy_VSTS.sql`](deploy_VSTS.sql) en la instancia.

### 3.2 Habilitar SQL auth (mixed mode) si no esta

Si el SQL Server actual esta en Windows auth pura, hay que pasar a **mixed mode**: en SSMS, click derecho en la instancia → Properties → Security → "SQL Server and Windows Authentication mode", y reiniciar el servicio.

### 3.3 Verificar conectividad desde la VM

```bash
nc -zv rafa 1433
```

Si el puerto no abre, revisar firewall en el servidor SQL y reglas de la red privada.

---

## 4. Configurar los GitHub Secrets

En el repo, **Settings → Secrets and variables → Actions → New repository secret**, crear:

| Secret                   | Valor                                                                                                                  |
| ------------------------ | ---------------------------------------------------------------------------------------------------------------------- |
| `CANELARY_DB_CONNECTION` | `Server=rafa;Database=VisualTernera;User Id=canelary_app;Password=<password>;Encrypt=True;TrustServerCertificate=True` |

**No** commitear este valor en el repo.

Otros valores no-sensibles del workflow estan hardcodeados en [`ci.yml`](.github/workflows/ci.yml):

- `SERVER_HOST_PORT=5262` — puerto publicado del Server al host (lo usan los Clients Windows directamente).
- `WEBAPP_HOST=canelary.runfosa.local` — Host header con el que el workflow chequea la WebApp via Traefik. Debe matchear la regla `traefik.http.routers.canelary.rule` en [`docker-compose.yml`](docker-compose.yml).

---

## 5. Primer deploy

Con todo lo anterior listo, basta con hacer push a `main`. El job `deploy` se va a tomar el runner `canelary-prod` y va a:

1. Validar que la red externa `traefik-public` exista en el host (si no, aborta con mensaje claro).
2. Buildear las imagenes `canelary-server:local` y `canelary-webapp:local` con `docker compose build`.
3. Taggear ambas con el SHA del commit para rollback posterior.
4. Levantar el stack con `docker compose up -d --no-build --remove-orphans`.
5. Pollear `http://localhost:5262/healthz` (Server, puerto directo) durante 60s.
6. Pollear `http://localhost/healthz` con header `Host: canelary.runfosa.local` (WebApp via Traefik) durante 60s.
7. Smoke test del reverse proxy: `curl -H "Host: canelary.runfosa.local" http://localhost/clients` debe devolver 200/401/403.

Si cualquier healthcheck falla, vuelca logs y aborta el deploy. Como `docker compose up` ya levanto el stack nuevo y bajo el viejo (los `container_name` chocan), un fallo de healthcheck deja la VM con el stack nuevo roto. **Hacer rollback manual** segun la seccion 6.

Verificar manualmente despues del primer deploy:

```bash
docker compose ps                                                          # ambos servicios "Up X seconds (healthy)"
curl http://localhost:5262/healthz                                         # Server: "Healthy"
curl -H "Host: canelary.runfosa.local" http://localhost/healthz            # WebApp via Traefik: "ok"
curl -H "Host: canelary.runfosa.local" http://localhost/                   # SPA HTML
curl -H "Host: canelary.runfosa.local" http://localhost/clients            # JSON o 401 (reverse proxy a Server)
docker compose logs --tail 50                                              # sin excepciones de DB ni de filesystem
```

Desde otra maquina de la red privada (asume DNS interno apuntando `canelary.runfosa.local` a la VM):

```bash
curl http://<vm-ip>:5262/healthz             # acceso directo al Server (Clients lo usan)
curl http://canelary.runfosa.local/          # WebApp via Traefik en el browser
```

---

## 6. Operacion dia-a-dia

### Ver logs

```bash
cd <repo>
docker compose logs -f                      # ambos servicios en stream
docker compose logs -f server               # solo backend
docker compose logs -f webapp               # solo nginx (acceso + errores)

# Logs por-cliente de Analysis (volumen montado)
docker run --rm -v canelary-logs:/logs alpine ls /logs
```

### Restart sin redeploy

```bash
docker compose restart                      # ambos
docker compose restart server               # solo uno
```

### Rollback manual a un SHA anterior

Las imagenes quedan taggeadas por SHA en el daemon local hasta que `docker image prune` las borre (7 dias por default). Para revertir sin tocar Git:

```bash
docker images canelary-server               # ver SHAs disponibles
docker images canelary-webapp

# Re-taggear el SHA viejo como :local (lo que compose usa)
docker tag canelary-server:<sha-viejo> canelary-server:local
docker tag canelary-webapp:<sha-viejo> canelary-webapp:local

# Re-levantar el stack con esas imagenes
cd <repo>
docker compose up -d --no-build --remove-orphans
```

Si necesitas pasarle env vars manualmente (ej. el runner las setea desde secrets pero vos estas en una sesion SSH), exportalas antes de `docker compose up`:

```bash
export CANELARY_DB_CONNECTION="..."
export SERVER_HOST_PORT=5262
docker compose up -d --no-build
```

O usar un archivo `.env` local (no commiteado, ver [`.env.example`](.env.example)).

### Build manual (sin pasar por GitHub Actions)

Util para debug en la VM:

```bash
cd <repo>
docker compose build                        # buildea ambos
docker compose build server                 # solo uno
docker compose up -d --build                # build + up en un solo paso
```

### Bajar el stack

```bash
docker compose down                         # frena y borra containers (volumes intactos)
docker compose down -v                      # ADEMAS borra el volumen de logs — destructivo
```

### Liberar espacio

El step `Prune old images` corre tras cada deploy y borra imagenes sin tags ni containers asociados de mas de 7 dias. Si hace falta forzar limpieza:

```bash
docker image prune -a -f                    # borra TODAS las imagenes no usadas (peligroso si querias rollback)
docker volume prune -f                      # borra volumenes huerfanos (no toca `canelary-logs` si esta en uso)
```

---

## 7. Cambios comunes

### Cambiar el Host de la WebApp en Traefik

La WebApp ya no expone puerto al host; se la matchea por `Host` header en Traefik. Para cambiar el dominio (ej. de `canelary.runfosa.local` a otro):

1. Editar la label `traefik.http.routers.canelary.rule` en [`docker-compose.yml`](docker-compose.yml).
2. Editar `server_name` en [`WebApp/nginx.conf`](WebApp/nginx.conf) para que coincida (no es estrictamente necesario para que funcione, pero evita confusion).
3. Editar `WEBAPP_HOST` en el bloque `env:` del job `deploy` en [`ci.yml`](.github/workflows/ci.yml) — los healthchecks usan ese header.
4. Actualizar el DNS interno para que el nuevo nombre resuelva a la VM.
5. Push a `main`.

### Agregar un nuevo endpoint al Server

El `location ~` de [`WebApp/nginx.conf`](WebApp/nginx.conf) lista los endpoints proxeados explicitamente. Al agregar uno nuevo en `Server.cs`, agregarlo tambien al regex de la location. De lo contrario, nginx va a intentar resolverlo como ruta de SPA y devolver `index.html`.

### Renombrar/agregar un servicio

`container_name` en [`docker-compose.yml`](docker-compose.yml) hace que los hostnames internos sean estables (`canelary-server`, `canelary-webapp`). El upstream de nginx (`upstream canelary_api { server canelary-server:8080; }` en [`WebApp/nginx.conf`](WebApp/nginx.conf#L10)) depende de ese nombre.

---

## 8. Checklist resumido

- [ ] Docker Engine + docker-compose-plugin instalados y funcionales
- [ ] Red docker externa `traefik-public` creada y Traefik corriendo, publicando el entrypoint `web` en host :80
- [ ] Usuario `gh-runner` en grupo `docker`
- [ ] Volumen `canelary-logs` o `/var/log/canelary-server` listo
- [ ] Share SMB de Etiquetas montado en `/mnt/etiquetas` (entrada en `/etc/fstab`, credenciales en `/etc/cifs-credentials`)
- [ ] Runner registrado con label `canelary-prod` y corriendo como servicio
- [ ] SQL auth habilitado en el servidor SQL
- [ ] Usuario `canelary_app` creado con permisos sobre `VisualTernera`
- [ ] Conectividad VM → SQL Server validada (puerto 1433)
- [ ] El secret `CANELARY_DB_CONNECTION` cargado en GitHub
- [ ] Puerto 5262 abierto en el firewall de la VM (Clients Windows); puerto 80 abierto si la WebApp se accede desde fuera de la VM (segun politica de red interna)
- [ ] DNS interno: `canelary.runfosa.local` resuelve a la IP de la VM
- [ ] Push de prueba a `main` corre exitoso de punta a punta
- [ ] `curl -H "Host: canelary.runfosa.local" http://<vm-ip>/` devuelve el HTML de la SPA
- [ ] `curl -H "Host: canelary.runfosa.local" http://<vm-ip>/clients` devuelve datos del backend via reverse proxy

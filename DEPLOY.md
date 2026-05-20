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
   host :5262                 host :8080
        |                           |
        |   red interna docker      |
        +-------- canelary ---------+
                      |
                      v
              SQL Server (rafa)
```

- **`canelary-server`**: API en .NET 10. Escucha en `:8080` dentro del contenedor, publicada al host en `:5262`. Conecta a SQL Server via la red privada de la VM.
- **`canelary-webapp`**: SPA React servida por nginx. Escucha en `:80` dentro del contenedor, publicada al host en `:8080`. Hace reverse proxy de los endpoints del backend al servicio `canelary-server` por la red docker interna (ver [`WebApp/nginx.conf`](WebApp/nginx.conf)).
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

### 1.2 Crear el usuario que corre el runner

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

### 1.3 Crear el directorio de logs persistente

Los logs del Server (escritos via `CANELARY_SERVER_LOG_BASE`) se montan como volumen para sobrevivir redeploys. El `docker-compose.yml` usa un volumen named (`canelary-logs`) por defecto; si preferis bind-mount al filesystem del host:

```bash
sudo mkdir -p /var/log/canelary-server
sudo chown gh-runner:gh-runner /var/log/canelary-server
```

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

| Secret                         | Valor                                                                                                                  |
| ------------------------------ | ---------------------------------------------------------------------------------------------------------------------- |
| `CANELARY_DB_CONNECTION`       | `Server=rafa;Database=VisualTernera;User Id=canelary_app;Password=<password>;Encrypt=True;TrustServerCertificate=True` |
| `CANELARY_AUTH_CLAVE_PUBLICA`  | Clave publica usada por el Client para firmar requests                                                                 |
| `CANELARY_AUTH_CLAVE_PRIVADA`  | Clave privada con la que el Server valida los requests                                                                 |
| `CANELARY_AUTH_CLAVE_DESCARGA` | Clave que protege los endpoints `/get-client` y `/installer`                                                           |

Los valores se pueden tomar del `appsettings.json` de produccion o del keystore que use el equipo. **No** commitear estos valores en el repo.

Los puertos publicados al host (`SERVER_HOST_PORT=5262`, `WEBAPP_HOST_PORT=8080`) estan hardcodeados en el workflow porque no son sensibles; si hay que cambiarlos, editar [`ci.yml`](.github/workflows/ci.yml).

---

## 5. Primer deploy

Con todo lo anterior listo, basta con hacer push a `main`. El job `deploy` se va a tomar el runner `canelary-prod` y va a:

1. Buildear las imagenes `canelary-server:local` y `canelary-webapp:local` con `docker compose build`.
2. Taggear ambas con el SHA del commit para rollback posterior.
3. Levantar el stack con `docker compose up -d --no-build --remove-orphans`.
4. Pollear `http://localhost:5262/healthz` y `http://localhost:8080/healthz` durante 60s cada uno.
5. Smoke test del reverse proxy: `curl http://localhost:8080/clients` debe devolver 200/401/403.

Si cualquier healthcheck falla, vuelca logs y aborta el deploy. Como `docker compose up` ya levanto el stack nuevo y bajo el viejo (los `container_name` chocan), un fallo de healthcheck deja la VM con el stack nuevo roto. **Hacer rollback manual** segun la seccion 6.

Verificar manualmente despues del primer deploy:

```bash
docker compose ps                           # ambos servicios "Up X seconds (healthy)"
curl http://localhost:5262/healthz          # Server: "Healthy"
curl http://localhost:8080/healthz          # WebApp: "ok"
curl http://localhost:8080/                 # debe devolver el HTML de la SPA
curl http://localhost:8080/clients          # via reverse proxy, debe devolver JSON o 401
docker compose logs --tail 50               # sin excepciones de DB ni de filesystem
```

Desde otra maquina de la red privada:

```bash
curl http://<vm-ip>:5262/healthz            # acceso directo al Server (Clients lo usan)
curl http://<vm-ip>:8080/                   # WebApp en el browser
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
export CANELARY_AUTH_CLAVE_PUBLICA="..."
export CANELARY_AUTH_CLAVE_PRIVADA="..."
export CANELARY_AUTH_CLAVE_DESCARGA="..."
export SERVER_HOST_PORT=5262
export WEBAPP_HOST_PORT=8080
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

### Cambiar el puerto publicado de la WebApp

1. Editar el bloque `env:` del job `deploy` en [`ci.yml`](.github/workflows/ci.yml), cambiar `WEBAPP_HOST_PORT`.
2. Si el firewall de la VM filtra puertos, abrir el nuevo y cerrar el viejo.
3. Push a `main`.

### Agregar un nuevo endpoint al Server

El `location ~` de [`WebApp/nginx.conf`](WebApp/nginx.conf) lista los endpoints proxeados explicitamente. Al agregar uno nuevo en `Server.cs`, agregarlo tambien al regex de la location. De lo contrario, nginx va a intentar resolverlo como ruta de SPA y devolver `index.html`.

### Renombrar/agregar un servicio

`container_name` en [`docker-compose.yml`](docker-compose.yml) hace que los hostnames internos sean estables (`canelary-server`, `canelary-webapp`). El upstream de nginx (`upstream canelary_api { server canelary-server:8080; }` en [`WebApp/nginx.conf`](WebApp/nginx.conf#L10)) depende de ese nombre.

---

## 8. Checklist resumido

- [ ] Docker Engine + docker-compose-plugin instalados y funcionales
- [ ] Usuario `gh-runner` en grupo `docker`
- [ ] Volumen `canelary-logs` o `/var/log/canelary-server` listo
- [ ] Runner registrado con label `canelary-prod` y corriendo como servicio
- [ ] SQL auth habilitado en el servidor SQL
- [ ] Usuario `canelary_app` creado con permisos sobre `VisualTernera`
- [ ] Conectividad VM → SQL Server validada (puerto 1433)
- [ ] Los 4 secrets cargados en GitHub
- [ ] Puertos 5262 y 8080 abiertos en el firewall de la VM (segun politica de red interna)
- [ ] Push de prueba a `main` corre exitoso de punta a punta
- [ ] `curl http://<vm-ip>:8080/` devuelve el HTML de la SPA
- [ ] `curl http://<vm-ip>:8080/clients` devuelve datos del backend via reverse proxy

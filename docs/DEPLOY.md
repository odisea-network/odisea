# Deploying Odisea for free (showcase)

This guide puts the whole stack on the public internet at zero cost, using a
single-origin setup that needs no CORS configuration:

| Piece | Host | Free? | Notes |
|---|---|---|---|
| API + Angular portal + Lit components | **Render** web service (Docker) | Yes | Sleeps after ~15 min idle; first request cold-starts in ~30–50s |
| PostgreSQL 17 | **Neon** | Yes | Serverless, scales to zero |

One Render service builds and serves everything together, so the browser sees a
single origin and the portal's relative `apiBase` (`/api/v1`) works unchanged.

---

## 1. Create the database (Neon)

1. Sign up at <https://neon.tech> and create a project (pick a region close to you, e.g. EU Central).
2. Create a database named `odisea` (or use the default and adjust the name below).
3. Open **Connection details** and copy the host, database, username and password.
4. Build an **Npgsql keyword** connection string (Neon shows a `postgresql://…` URL — convert it to this form):

   ```
   Host=ep-xxx-xxx.eu-central-1.aws.neon.tech;Database=odisea;Username=odisea;Password=YOUR_PASSWORD;SSL Mode=Require;Trust Server Certificate=true
   ```

   `SSL Mode=Require` is mandatory — Neon only accepts TLS connections.

> The API runs `MigrateAsync()` on every boot, so the empty Neon database
> provisions its own tables on first deploy. With `Seed__Demo=true` it also loads
> the demo agencies, offers, collections and accounts.

## 2. Deploy the app (Render)

1. Sign up at <https://render.com> and connect your GitHub account / this repo.
2. **New → Blueprint**, point it at the repository. Render reads [`render.yaml`](../render.yaml)
   and proposes one web service named `odisea`.
3. Before the first deploy, set the one secret value the blueprint leaves blank:
   - `ConnectionStrings__Default` → the Neon string from step 1.4.
   (`Jwt__Secret` is generated automatically; `ASPNETCORE_ENVIRONMENT`, `PORT`,
   `Seed__Demo` and `Swagger__Enabled` come from the blueprint.)
4. **Apply / Deploy.** The first build takes several minutes — it compiles the Lit
   components, the Angular portal and the .NET API into one image.

When the build finishes you get a public URL like `https://odisea.onrender.com`:

| Path | What |
|---|---|
| `/` | Landing + portal (Angular SPA) |
| `/swagger` | API explorer |
| `/health` | Health check (Render uses this) |
| `/demo/agency-blue.html` | Embedded-components demo page |

## 3. Log in

The demo seeder creates these accounts (development credentials — fine for a
showcase, rotate before any real use):

| Role | Email | Password |
|---|---|---|
| Agency | `blue@blue-horizon.com` | `Blue1234!` |
| Operator | `ops@sun-operators.com` | `Ops1234!` |
| Platform admin | `admin@odisea.net` | `Admin1234!` |

---

## Notes & gotchas

- **Cold starts.** On the free plan the service sleeps when idle. The first hit
  after a nap takes ~30–50s while it wakes; subsequent requests are fast. For a
  live demo, open the URL a minute before presenting to warm it up.
- **Background scheduler.** The connector scheduler only runs while the service
  is awake, so feed imports happen on demand rather than continuously. That is
  expected on a sleeping free tier and does not affect the portal demo.
- **Turning off demo seeding.** Set `Seed__Demo=false` (and remove the demo
  accounts) before using this for anything real. Migrations still run regardless.
- **Custom domain.** Render supports a free custom domain with automatic HTTPS if
  you want `app.yourdomain.com` instead of `*.onrender.com`.
- **Alternative split.** If you later want the portal to stay always-on (no cold
  start), host it as a separate Render **Static Site** (free, never sleeps) and
  keep the API as the web service. That reintroduces cross-origin calls, so you
  would add a CORS policy for the portal origin and switch the portal's
  `apiBase` to the absolute API URL. The single-origin setup above avoids all of
  that and is the recommended starting point.

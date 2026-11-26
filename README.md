# Support Desk

A responsive support-ticket tracker built using .NET Core 8, React 18, SQL Server. Sign in to create, search, filter, update, and delete tickets, and see total summary by status and priority.

## Run locally with Docker

1. Install Docker Desktop and start it.
2. From the repository root, run `docker compose up -d`. The first start downloads required images, installs dependencies, and applies the EF Core database migration.
3. Open [Support Desk](http://localhost:8080) and sign in with the local default credentials.

- Set `ADMIN_USERNAME` and `ADMIN_PASSWORD` in your shell or a local Compose `.env` file to override the demo login.
- Compose runs Vite, ASP.NET Core, and SQL Server as three services.
Vite publishes port 8080 and forwards `/api` and `/swagger` to the backend over the internal Docker network.
- Saving React/CSS/ any Frontend Files trigger Vite hot reload. C# edits are handled by `dotnet watch`, which restarts the app when an edit requires it.
- Run `docker compose logs -f frontend backend sqlserver` to follow startup. Run `docker compose down` to stop the stack and keep the `sql_data` volume. Adding `-v` deletes the volume and its data.

## View local database rows

Use DBeaver Community / any SQL Client like MS SQL Server for Windows

| Setting | Value |
| --- | --- |
| Host and port | `localhost`, `1433` |
| Database | `SupportTracker` |
| Authentication | SQL Server Authentication |
| Username | `sa` |
| Password | `LocalOnly_Strong123!`, or the password configured for the local SQL instance |
| Trust Server Certificate | Enabled for this local connection |

Click Test Connection, then browse Schemas → dbo → Tables → Tickets → Data. This read-only query works in both local SQL Server and Azure SQL.

```sql
SELECT TOP (20) Id, Title, Status, Priority, CreatedAt
FROM dbo.Tickets
ORDER BY CreatedAt DESC;
```

To open a SQL prompt without installing a client, run this from the repository root. Enter `GO` on its own line to execute a query and `QUIT` to exit.

```sh
docker compose exec sqlserver sh -c 'SQLCMDPASSWORD="$MSSQL_SA_PASSWORD" exec /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -d SupportTracker'
```

Compose reads `SQL_PASSWORD` when configuring the local SQL service. To change the password on a database volume that already exists, update the SQL Server login and the app configuration together.

## Authentication, rate limits, and Swagger

- `POST /api/auth/login` validates the account and sets a signed JWT in an `HttpOnly`, `Secure`, `SameSite=Lax` cookie valid for **15 minutes**. The browser sends it automatically, and the UI checks `GET /api/auth/session` after a refresh.
- Each protected request validates the cookie’s signature, issuer, audience, and expiry. There is no server-side session or token revocation list. **Sign out** clears the cookie. A stolen copy remains valid until expiry.
- Writes, including login and logout, require the `X-SupportDesk-CSRF: 1` header. The API grants no cross-origin CORS permissions. The UI and Swagger add the header automatically.
- The API allows **120 requests per minute** per authenticated username or per connection IP for anonymous requests. Login has an additional shared limit of **10 requests per minute** per app instance. A throttled request returns `429 Too Many Requests`.
- In [local Swagger](http://localhost:8080/swagger), sign in to Support Desk in the same browser or run **POST /api/auth/login** in Swagger. Protected calls use the cookie automatically.
- Use HTTPS on hosted deployments and `http://localhost:8080` for local development. For Azure replicas to validate the same cookies, set a shared, randomly generated `Auth__SigningKey` of at least 32 bytes as a secret. An unconfigured instance generates a key at startup, so a restart clears its existing sign-ins. Rate-limit counters remain per instance.

## Technology choices and assumptions

- React, TypeScript, and Vite provide typed API interactions, fast hot reload, and a responsive layout. The form uses native required and length checks plus a trimmed-title check before sending a request. The API validates input independently.
- ASP.NET Core 8 and Swagger provide REST controllers, standardized error responses, JWT validation, rate limiting, and an interactive API reference.
- EF Core and SQL Server provide migrations and async relational queries. Ticket status and priority totals are aggregated in SQL. DTOs limit which fields clients can write.
- Docker Compose makes local development reproducible and persists data in a Docker volume. The release Dockerfile builds the React assets into the ASP.NET Core image so the deployed UI and API share an origin.
- The assessment uses one configured account and one shared ticket queue. Titles are required and capped at 120 characters. Descriptions are optional and capped at 4,000 characters. Timestamps are UTC, and the API caps page size at 100.
- Statuses are `open`, `in_progress`, and `closed`. Priorities are `low`, `medium`, and `high`. Search matches title substrings using the database collation.

## Run and test without Docker

Use the .NET 8 SDK, Node.js 20+, and a reachable SQL Server. Set `ConnectionStrings__Tickets` to your database connection string if it differs from [appsettings.json](backend/SupportTracker/appsettings.json). Set `Auth__Password` to a local app password. `Auth__Username` defaults to `admin` when no account name is configured.

```sh
# Terminal 1, from the repository root
dotnet watch --project backend/SupportTracker run --urls http://localhost:8080

# Terminal 2, from the repository root
cd frontend
npm install
npm run dev
```

Open [localhost:5173](http://localhost:5173). If the full Compose stack is running, stop its frontend first to free port 8080. Run `dotnet test backend/SupportTracker.Tests` from the repository root for the backend tests. They use in-memory SQLite. Run `npm run build` from `frontend` to type-check and build the UI.

## Azure demo and deployment

- Open the [Azure demo](https://support-tracker.bluesky-bc9ab669.southeastasia.azurecontainerapps.io/) or [Azure Swagger](https://support-tracker.bluesky-bc9ab669.southeastasia.azurecontainerapps.io/swagger). Ask the project owner for reviewer credentials. The currently deployed revision determines whether you see the original browser login prompt or the new JWT login screen. For a cookie-auth revision, sign in with the reviewer credentials in the same browser before using Swagger.
- Azure Container Apps runs the release image over HTTPS and connects to a separate Azure SQL Database. The database connection string is supplied through Container Apps configuration backed by a secret. The local SQL volume contains only local data.
- The JWT revision uses `Auth__Username` and `Auth__Password` or falls back to the existing `DemoAccess__Username` and `DemoAccess__Password` settings. Keep hosted credentials in Azure secrets.
- GitHub Actions tests the API, publishes an AMD64 image to private Docker Hub, and deploys the commit-tagged image to Azure via OIDC. The workflow runs on pushes to `main` and supports manual dispatch. It checks revision health and confirms unauthenticated ticket requests are rejected. See the [workflow](.github/workflows/docker-publish.yml) and [Azure deployment notes](deploy/AZURE.md).

### View Azure data

- Reviewer access: Sign in to the Azure app and use the ticket queue. For API access, use Swagger to run `GET /api/tickets` or `GET /api/tickets/summary`.
- Direct database access: In Azure Portal, open SQL databases, select the database used by the Container App, and open Query editor. Sign in with an authorized database identity and run the read-only query above. You can also use DBeaver with the server name and database shown in Azure Portal, port `1433`, and encrypted certificate-validated access. Use a read-only identity for browsing. For public connections, an Azure administrator must allow your client’s outbound IP in the SQL firewall. The reviewer login is for app access. [Azure SQL Query Editor](https://learn.microsoft.com/en-us/azure/azure-sql/database/query-editor?view=azuresql)

## What I would improve with more time

- Replace the shared account with an identity provider and per-user roles. For multiple replicas, move rate limiting to a shared gateway and manage signing keys across replicas.
- Add SQL Server integration tests and browser tests for login, ticket operations, and mobile layouts.
- Move migrations into a dedicated deployment step and add health probes, structured monitoring, and restore testing.
- Add optimistic concurrency for ticket edits and revisit search indexes as ticket volume grows.

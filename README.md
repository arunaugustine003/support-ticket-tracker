# Support Desk

A small support ticket tracker built for the senior full-stack assessment. ASP.NET Core 8 and EF Core provide a REST API backed by SQL Server; a React/TypeScript interface is served by the API in the container. See [REVIEW.md](REVIEW.md) for the code review and optional LLM design.

## Run with Docker

Install and start Docker Desktop with Compose, then from this directory run:

```sh
docker compose up --build
```

Open http://localhost:8080 (Swagger: http://localhost:8080/swagger). The app retries the database connection during startup and applies the included EF migration. First startup may take a few minutes while images download and SQL Server initializes. Ticket data is stored in the `sql_data` Docker volume. `docker compose down` stops the services without deleting that volume.

**Apple Silicon:** SQL Server's image targets x86-64; Compose requests `linux/amd64` emulation. SQL Server on emulation is not an officially supported production configuration and can be slow or fail depending on Docker Desktop, available memory and emulation settings. Allocate sufficient Docker memory (at least 4 GB recommended) and enable the relevant x86 emulation option in Docker Desktop. If it does not work, use a SQL Server instance on an x86-64 machine or Azure SQL, run the app outside Compose, and set `ConnectionStrings__Tickets` to that instance. No local SQL Server installation is required in that case.

The Compose password is for local development only. Override it with `SQL_PASSWORD` for local use; never reuse it or the development connection string for a hosted environment. Use a secret manager, encrypted connections and restricted database access for deployment. Do not commit secrets.

## Local development

Requirements: .NET 8 SDK, Node.js 20+, and a reachable SQL Server. Set `ConnectionStrings__Tickets` in your environment if it differs from `backend/SupportTracker/appsettings.json`. From the repository root:

```sh
dotnet run --project backend/SupportTracker --urls http://localhost:8080
```

In another terminal:

```sh
cd frontend
npm install
npm run dev
```

Visit http://localhost:5173. Vite proxies `/api` to port 8080. `npm run build` checks TypeScript and builds the frontend. Run tests with `dotnet test backend/SupportTracker.Tests`.

## API and decisions

- `GET /api/tickets?status=open&search=login&page=1&pageSize=10` returns `{ items, total, page, pageSize }`; page size is capped at 100. Search matches substrings in titles; case sensitivity follows the database collation.
- `GET /api/tickets/{id}`, `POST /api/tickets`, `PUT /api/tickets/{id}`, and `DELETE /api/tickets/{id}` provide CRUD. The PUT body is a complete ticket input: `title`, `description`, `status`, `priority`.
- `GET /api/tickets/summary` returns `byStatus` and `byPriority` counts, including zeroes. Both aggregations execute in SQL.
- Status values are `open`, `in_progress`, `closed`; priority values are `low`, `medium`, `high`. Timestamps are set in UTC. Title is trimmed, required and limited to 120 characters; description is optional and capped at 4,000 characters as an implementation assumption.
- EF Core provides async database access and migrations. DTOs keep database-controlled fields out of writes. Tests use an in-memory SQLite connection for fast relational tests; SQL Server-specific behavior still requires integration testing against SQL Server.
- The UI uses relative API URLs so the built frontend and API can share an origin. Vite's proxy supports local development without embedding a deployment URL.

## Tradeoffs and next steps

A shared HTTP Basic login can be enabled for a hosted demo with the `DemoAccess__Username` and `DemoAccess__Password` environment variables. It must only be used behind HTTPS and is not a substitute for individual accounts or a production identity provider; the local Compose setup does not enable it. **Do not use real or sensitive data in the demo.** Before deployment, add authentication and authorization, secure secret management, HTTPS, rate limits, database backups, health checks, observability and a deliberate migration/deployment process. The startup migration is convenient for this assessment but should be replaced with a controlled deployment step for a multi-instance production system. Add SQL Server integration tests and concurrent-update protection; consider more efficient search indexing as data grows. The demo uses a single shared queue and assumes all users may edit all tickets. The live demo is at https://support-tracker.bluesky-bc9ab669.southeastasia.azurecontainerapps.io/ and requires the shared reviewer login. The demo password is stored in the owner’s macOS Keychain as `support-tracker-demo-login`; share it with the assessment reviewers through an appropriate secure channel. Azure SQL firewall access is limited to the observed Container App egress IP; Azure may change that IP during scaling or infrastructure changes, requiring the firewall rule to be updated. The database pauses when its monthly free allowance is exhausted. Azure Container Apps and supporting services may incur charges outside their free allowances.

## GitHub Actions and Docker Hub

The `Test and publish Docker image` workflow runs for every push to `main` (including a pull request merge) and can also be started manually. It runs the API tests, builds the React frontend and API in the Docker image, and pushes `linux/amd64` images to the private Docker Hub repository tagged `assessment` and with the full commit SHA. After the image is pushed, the workflow signs in to Azure with GitHub OIDC, deploys the commit-specific image to Azure Container Apps, and checks revision health and the demo login challenge.

Configure the Actions variables `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, and `AZURE_SUBSCRIPTION_ID` for the federated Azure identity, with trust restricted to this repository’s `main` branch and permissions scoped to the Container App. Set the repository Actions variable `DOCKERHUB_USERNAME` to the Docker Hub account name and the repository secret `DOCKERHUB_TOKEN` to a Docker Hub personal access token with **write** permission for the image. The read-only token used by Azure to pull the image cannot push it. Never commit these credentials. The image repository must already exist and remain private. Since the workflow runs on pushes to `main`, direct pushes trigger it too; protect `main` and require pull requests if every update should be reviewed before publication.

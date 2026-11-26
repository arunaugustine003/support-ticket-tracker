# Azure deployment plan

The app image serves both React and the API on port 8080. The production image must be built for `linux/amd64`, even when built on an Apple Silicon Mac. The container image must be pushed to a registry accessible to Azure Container Apps; Docker Compose and its SQL Server container are for local use only.

## Proposed resources

- A new resource group in a supported region, leaving existing resources untouched.
- Azure SQL logical server and a new SQL database using the General Purpose serverless free offer (`--use-free-limit true` and `--free-limit-exhaustion-behavior AutoPause`). Verify offer eligibility and the chosen region's availability before creating the database. If the free offer is unavailable, stop rather than silently create a paid database.
- Azure Container Apps consumption environment and one app with external HTTPS ingress, target port 8080 and a minimum of zero replicas. Configure appropriate startup time for a waking Azure SQL database. Review actual settings and supporting resource charges before creation.
- An image registry selected by the owner. Azure Container Registry is billed independently; an existing public image registry may avoid that charge, subject to its own terms and authentication.

## Security and operations

Ticket endpoints require a short-lived JWT in a secure cookie set by the login screen or Swagger. Configure the account with Container Apps secrets using `Auth__Password` and `Auth__Username`, or retain the existing `DemoAccess__Password` and `DemoAccess__Username` settings as fallback credentials. For multiple replicas, provide the same randomly generated `Auth__SigningKey` secret to each replica. Authentication requires no shared session store. Rate-limit counters are per instance. Logout clears the browser cookie, and any copied credential expires after 15 minutes. Use only non-sensitive demonstration data, and agree how reviewers should access the demo before exposing it publicly. Put the SQL connection string in Container Apps secrets, never in source control or an image. Use encrypted SQL connections with certificate validation and least-privilege database credentials; do not use the local Compose password. Configure SQL networking to permit only the intended application access where practical. The Azure SQL option to allow Azure services is broad and includes other customers' Azure subscriptions; it is not equivalent to a private network.

Migrations currently run on app startup using the configured database identity. For an assessment demo this means that identity requires schema permissions. A production deployment should apply migrations separately with a restricted deployment identity and give the running app only the permissions it needs.

The SQL free offer can pause the database until the next month when its free limit is reached. Container Apps' consumption grant does not cap total subscription costs: configure budget alerts, review logging, networking and registry charges, and remove the resource group when the demo is no longer needed. A budget alert is not a hard spending limit. Cold starts can affect the first request.

## Verification after deployment

1. Confirm the database reports the free offer and `AutoPause` at free-limit exhaustion.
2. Check app logs, database migration and the externally served frontend and Swagger.
3. Exercise creation, filtering, status changes, summary and deletion using a uniquely named temporary ticket, then remove it.
4. Record the public URL and known limitations in the README. Do not publish secrets.

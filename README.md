 CoffeeNChill Canteen Management System — Part 1

 Local Setup
1. Clone the repo: `git clone https://github.com/modestsnake/Coffee.git`
2. Ensure .NET 8 SDK and Azure Functions Core Tools are installed
3. Confirm `local.settings.json` has `"AzureWebJobsStorage": "UseDevelopmentStorage=true"`
4. Run `func start` from the project root
5. Test endpoints using the Postman collection found in `/docs`

 Docker — Standalone Execution (No Compose)

Prerequisites
- A real Azure Storage Account connection string for StaffDocsStorage

 1. Create the network
docker network create coffeenchill-net

 2. Run Azurite
docker run -d --name azurite --network coffeenchill-net -p 10000:10000 -p 10001:10001 -p 10002:10002 mcr.microsoft.com/azure-storage/azurite

3. Build the Functions image
docker build -t modestsnake/coffeenchill-functions:v1.0 .

4. Run the Functions container
docker run -d --name functions --network coffeenchill-net -p 7120:80 -e AzureWebJobsStorage="<connection string>" -e StorageConnection="<same>" -e StaffDocsStorage="<real Azure connection string>" modestsnake/coffeenchill-functions:v1.0

 Docker Hub
- https://hub.docker.com/r/modestsnake/coffeenchill-functions
- https://hub.docker.com/r/modestsnake/coffeenchill-azurite

Team Contributions
- [Asande Ngubane]: Standalone Dockerfile, Docker Hub image publishing (Functions + Azurite), container-to-Azurite networking setup, Postman collection creation and endpoint testing
- [ThatGuy-wandz]:Azure Files Integration (Staff Documents)
Implemented the staff-docs Azure File Share integration, including:
	•	Registered a ShareDirectoryClient in Program.cs, creating the staff-docs share on startup
	•	Built UploadStaffDocument (POST /api/documents/upload) — parses multipart/form-data and streams files into the file share
	•	Built ListStaffDocuments (GET /api/documents) — returns file name, size, and last modified date for all stored documents
	•	Built DownloadStaffDocument (GET /api/documents/download/{fileName}) — streams a requested file back to the client, with 404 handling for missing files
- [Tristyn ST10468574] : Azure Tables and HTTP Functions ( Menu management ) 
  [Lihlithemba Sithole ST10485223]: Testing Suite & Postman Collection
Postman collection completeness
Environment variable usage
(l(baseUrl)
Test coverage across all endpoint. 

Part 2: Docker Compose Orchestration
Contributor:ST10477821
 What it does
A single `docker-compose.yml` in the repo root starts the full stack:

 Service - Image (Docker Hub) -Purpose 
 azurite  `modestsnake/coffeenchill-azurite:v1.0` | Emulates Azure Tables, Blobs and Queues locally |
 functions  `st10477821/coffeenchill-functions:v2.0` | Azure Functions host with the HTTP and Queue triggers |

Both images are pulled from Docker Hub. The file has no `build:` paths.

Configuration
- Custom bridge network (`coffeenchill-net`) lets the Functions container reach Azurite by its service name.
- Named volume(`azurite-data`) keeps Azurite data when containers restart.
- Health checkon Azurite, with the Functions service set to wait for it before it starts.
- Connection string is passed through the `AzureWebJobsStorage` environment variable and points to the `azurite` host, not `127.0.0.1`.
- - Ports:Functions are exposed on `7120`, and Azurite on `10000`, `10001` and `10002`.
  
 How to run
Make sure Docker Desktop is running, then from the repo root:
docker compose up -d
The API is then available at `http://localhost:7120/api`.
To stop the stack:
docker compose down
Add `-v` to also delete the stored Azurite data.  
 

  How to test
1.GET http://localhost:7120/api/menu returns the menu as JSON.
2. POST http://localhost:7120/api/orders/queue with a JSON order body places the order on the queue.
3.GET http://localhost:7120/api/orders/status/{orderId} shows the status moving Received, Preparing, Ready, Collected.

The Postman collections in the repo can be used for these tests.

Contributer Part 2
ST10485223 Lihlithemba Sithole:
"Azure Storage Queue
Placement (Order
Producer)
order-processing-
queue setup
Queue producer
endpoint/logic
JSON payload
serialization
Validation of order
fields"

POE P3
JWT Authentication & User Profiles (Azure Tables)

The Gateway (CoffeeNChill.Gateway) authenticates users with JSON Web Tokens (JWT). User profiles are stored in an Azure Table called Users. Passwords are never stored in plain text, only as BCrypt hashes.

How it works

1. A user registers with POST /api/auth/register. The Gateway hashes the password with BCrypt and saves the profile in the Users table (PartitionKey Users, RowKey = lowercase username).
2. The user logs in with POST /api/auth/login. The Gateway checks the password and returns a signed JWT that is valid for 60 minutes.
3. The client sends the token on every protected request as Authorization: Bearer &lt;token>.
4. The Gateway validates the token's signature, issuer, audience and expiry, then checks the user's role.

Token claims:sub (username), email, role, jti (unique token id).



A request is rejected in one of two ways:
• 401 Unauthorized: no token, or an invalid or expired token.
• 403 Forbidden: a valid token whose role is not allowed.

2. Run the Gateway.

On startup the Gateway creates the Users table if needed and seeds the Admin and Staff accounts. Swagger is available at /swagger.

3. Docker / VPS deployment. Use environment variables instead of user secrets. A double underscore replaces the colon


Security design decisions

-Passwords are hashed with BCrypt, which salts and slows down brute-force attacks. BCrypt only uses the first 72 bytes, so the maximum password length is 72 characters.
-Self-registration always creates a Public user, so nobody can register themselves as Admin or Staff.
-A fallback authorization policy requires a logged-in user on every endpoint. Public endpoints must opt out explicitly with [AllowAnonymous], so a forgotten attribute fails closed rather than open.
-Login returns the same message ("Invalid username or password.") for a wrong username and a wrong password, so attackers cannot discover which usernames exist.
-Token clock skew is set to zero, so tokens expire exactly on time.

Testing

Test with Swagger or Postman. Use your own base URL (for example https://localhost:PORT) and your own usernames and passwords.

- Register a Public user.
POST /api/auth/register
json
{ "username": "jane.smith", "email": "jane@example.com", "password": "Passw0rd123" }

Expected: 201 Created.

- Duplicate username. Send the same request again. Expected: 409 Conflict.

- Weak password. Register with the password short. Expected: 400 Bad Request.

- Wrong password. Log in with a wrong password. Expected: 401 Unauthorized.

-Log in and get a token.
POST /api/auth/login

Expected: 200 OK with accessToken, tokenType: "Bearer" and expiresAtUtc.

-Read the profile.GET /api/auth/me with the Bearer token. Expected: 200 OK showing the username, email and role Public. Without a token, expected: 401.

- Public user blocked from staff actions.POST /api/menu with the Public user's token. Expected: 403 Forbidden.

-Staff or Admin allowed. Log in as the seeded Staff or Admin account and repeat Test 7. Expected: a success response (200/201).

Test 9: Anonymous access.GET /api/menu with no token. Expected: 200 OK.

- No token on a protected route.GET /api/orders (the order list) with no token. Expected: 401 Unauthorized.

 Part 3: Live VPS Deployment

**Live URL:** http://172.209.216.242:5000/swagger
**Host:** Azure VM (Ubuntu 24.04 LTS), Docker Engine and Docker Compose
**Video walkthrough:**https://youtu.be/vcO4DU6F5ko?si=K5sAtkE08LZJ0HCc

 Architecture
```mermaid
flowchart LR
  C[Client: Swagger / Postman] -->|HTTP :5000| G[Gateway :8080]
  G |http://functions/api| F[Azure Functions :80]
  G |Users table, documents| A[(Azurite)]
  F|Orders, menu, queues| A
```
Only port 5000 the gateway is public. Azurite and the Functions are bound to localhost.

 Docker Hub images
- `st10477821/coffeenchill-gateway:v3.0`
- `st10477821/coffeenchill-functions:v2.0`
- `modestsnake/coffeenchill-azurite:v1.0`

 Deploy on a server
1. Install Docker Engine and Docker Compose.
2. Copy `docker-compose.yml` and `.env.example` to the server, then `cp .env.example .env`.
3. Edit `.env`: set `JWT_KEY` (32+ characters) and the admin and staff passwords.
4. Run `docker compose up -d`.
5. Open `http://172.209.216.242:5000/swagger`.

The  Roles
- Public: `GET /api/menu`, `POST /api/orders/queue`, register and login.
- Admin or Staff: menu changes, documents, order status updates.

Some  known limitations
- The site runs over plain HTTP (no domain or TLS certificate).
- Azurite stands in for Azure Storage on the server.
- Secrets are kept in a `.env` file that is not committed. Test credentials are shown in the video, not stored in the repo.

Note that we have also noted why the GitHub Actions did not work and we have noted what our current situation is with the Azure/VPS.

Here is a concise section of my README which covers the 10 mark rubric for the automated CI/CD Pipeline:
This has been put in points and paragraphs to clearly highlight the steps taken for the 5 requirements.

# 4. [OPTIONAL BONUS - 10 MARKS] Automated CI/CD Pipeline

## Overview
A GitHub Actions workflow has been created and stored in the '.github/workflows/deploy.yml' file. This will automatically run whenever any changes are made to the main/master branch.

## Pipeline

1. Check out code and .NET SDK
Using actions/checkout@v4 the latest code is pulled down. Using actions/setup-dotnet@v4, the .NET 10.0 SDK has been installed.

2. Build and run unit tests
The solution CoffeeNChill.Functions.slnx was restored and built using dotnet restore and dotnet build. The unit tests were run on the CoffeeNChill.Gateway.Tests project using dotnet test. All 13 tests ran successfully.

3. Build Docker container Images
The docker environment was set up using docker/setup-buildx-action. The Docker images for both Gateway and Azure Functions were built using the respective Dockerfiles.

4. Login to Docker Hub and push version tagged images
The pipeline was logged into Docker Hub using secure GitHub repository secrets DOCKERHUB_USERNAME and DOCKERHUB_TOKEN. The images were automatically versioned with a unique version tag (e.g. v1.). Both images were pushed onto Docker Hub (coffeenchill-gateway and coffeenchill-functions).

5. Connect to remote VPS and deploy
The pipeline connected to the remote Ubuntu VPS using the secure GitHub Secrets containing the SSH keys (VPS_HOST, VPS_USER and VPS_SSH_KEY). The docker-compose.yml file was securely copied to the server. The command 'docker-compose pull && docker-compose up -d' was run on the VPS. This pulls down the new images and restarts the live stack. Finally a smoke test was run to confirm the Gateway is responding through HTTP on port 5000.

## Current Pipeline Status
Steps 1-4 have been successfully included in the workflow file. The VPS deployment (step 5) has been set up, however I was unable to successfully run it. This was due to not having any Azure subscriptions to provision the VPS. 

# YouTuebe Link

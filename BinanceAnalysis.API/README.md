Getting Started — Clone and Run

The easiest way to run the application is with Docker Compose. The API and SQL Server run in containers, so you do not need to install SQL Server, Visual Studio or the .NET SDK to start the backend.

1. Prerequisites

Git

Docker Desktop with the Linux container engine enabled

Internet access

Make sure Docker Desktop is running before continuing.

2. Clone the repository

Open PowerShell and run:

git clone https://github.com/radoslavBachurov/BinanceAnalysis.git
cd BinanceAnalysis

3. Configure environment variables

Docker Compose uses a .env file in the repository root to configure SQL Server, JWT authentication and the administrator account.

3.1. Create your .env file

From the repository root, run:

Copy-Item .env.example .env

3.2. Generate a secure JWT signing key

The application requires a Base64-encoded random key containing at least 32 bytes (256 bits).

Run the following commands in PowerShell to generate one:

$bytes = New-Object byte[] 32
$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
$rng.GetBytes($bytes)
$JWT_SIGNING_KEY = [Convert]::ToBase64String($bytes)
$rng.Dispose()

Write-Host $JWT_SIGNING_KEY

Copy the generated value. You will use it as the JWT_SIGNING_KEY value in your .env file.

3.3. Edit your .env file

Open .env in a text editor and configure the following variables:

Variable

Description

SA_PASSWORD

Strong password for the SQL Server container. Must meet SQL Server password complexity requirements.

JWT_ISSUER

JWT token issuer. Keep the value provided in .env.example.

JWT_AUDIENCE

JWT token audience. Keep the value provided in .env.example.

JWT_SIGNING_KEY

Replace the placeholder with the Base64 key generated in the previous step.

ADMIN_USERNAME

Username used to authenticate as the administrator. Replace the example value with your preferred username.

ADMIN_PASSWORD

Strong password for the administrator account. Replace the example value with your own password.

For example, your .env file should follow this structure:

SA_PASSWORD=YourStrongSqlPassword123!
JWT_ISSUER=BinanceAnalysis.API
JWT_AUDIENCE=BinanceAnalysis.Client
JWT_SIGNING_KEY=PASTE_YOUR_GENERATED_BASE64_KEY_HERE
ADMIN_USERNAME=admin
ADMIN_PASSWORD=YourStrongAdminPassword123!

The example values above are placeholders. Use your own strong passwords and the generated signing key.

Important:

Do not use the placeholder signing key.

Do not leave the example administrator password unchanged.

Do not commit .env to GitHub. It contains private credentials and should remain local to your machine.

The .env.example file is a template and can safely be committed because it should contain placeholders rather than real secrets.

4. Build and start the application

Make sure Docker Desktop is running and configured to use the Linux container engine.

Open PowerShell in the repository root (the directory containing docker-compose.yml) and execute:

docker compose up --build -d

This command:

Builds the API Docker image from the source code in the repository.

Starts the SQL Server container.

Waits for SQL Server to become healthy.

Starts the API container.

Allows the API to initialize the database, apply EF Core migrations and seed the initial monitored symbols.

The first startup may take several minutes because Docker needs to download the SQL Server and .NET images.

5. Verify that the application is running

Check container status:

docker compose ps

Check API logs:

docker compose logs --tail 50 api

The API should be available at:

API: http://localhost:8080

Swagger UI: http://localhost:8080/swagger

Swagger is enabled in the Docker Compose Development environment.

6. Test the API

Example JSON request:

curl.exe -i http://localhost:8080/api/BTCUSDT/24hAvgPrice

Example XML request:

curl.exe -i -H "Accept: application/xml" `
  http://localhost:8080/api/BTCUSDT/24hAvgPrice

The initial monitored symbols are BTCUSDT, ADAUSDT and ETHUSDT.

Allow some time for Binance trade events to be received and persisted before requesting price calculations.

7. Run the Console client

The Console application communicates with the API over HTTP.

To publish a self-contained Windows executable, install the .NET 10 SDK and run:

dotnet publish .\BinanceAnalysis.Console\BinanceAnalysis.Console.csproj `
  --configuration Release `
  --runtime win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  --output .\publish\Console

In publish\Console\appsettings.json, configure the API base URL:

{
  "ApiSettings": {
    "BaseUrl": "http://localhost:8080/"
  }
}

Run the published executable:

.\publish\Console\BinanceAnalysis.Console.exe

Use the administrator username and password configured in .env to authenticate.

The Console supports price analysis and monitored-symbol administration. It also handles access-token acquisition and refresh automatically.

8. Stop the application

To stop the containers:

docker compose down

The SQL Server data volume is preserved, so the database remains available the next time the application is started.

To remove the database volume and its data as well, use:

docker compose down -v

Warning: This permanently deletes the persisted local database data.

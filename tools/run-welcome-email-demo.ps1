param(
    [ValidateRange(1, 65535)]
    [int]$ApiPort = 5051
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$apiProject = Join-Path $repoRoot "backend\src\CulinaryBlog.API\CulinaryBlog.API.csproj"
$postgresContainer = "culinaryblog-postgres"
$mailHogContainer = "frjob001-mailhog-demo"
$demoDatabase = "frjob001_welcome_demo"

Push-Location $repoRoot
try {
    if (-not (Test-Path ".env")) {
        throw "Missing .env. Copy .env.example to .env and configure local credentials first."
    }

    if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
        throw "Docker CLI was not found. Start Docker Desktop and ensure 'docker' is on PATH."
    }
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw ".NET SDK was not found. Install the SDK required by this repository."
    }

    $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, $ApiPort)
    try {
        $listener.Start()
    }
    catch {
        throw "Port $ApiPort is already in use. Stop the other API process or choose another port with -ApiPort."
    }
    finally {
        $listener.Stop()
    }

    Write-Host "Starting PostgreSQL..."
    docker compose up -d postgres
    if ($LASTEXITCODE -ne 0) {
        throw "Could not start the PostgreSQL Compose service."
    }

    $postgresUser = docker exec $postgresContainer printenv POSTGRES_USER
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($postgresUser)) {
        throw "Could not read the PostgreSQL user from the running container."
    }
    $postgresDatabase = docker exec $postgresContainer printenv POSTGRES_DB
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($postgresDatabase)) {
        throw "Could not read the PostgreSQL database from the running container."
    }
    $postgresPassword = docker exec $postgresContainer printenv POSTGRES_PASSWORD
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($postgresPassword)) {
        throw "Could not read the PostgreSQL password from the running container."
    }

    Write-Host "Waiting for PostgreSQL to become ready..."
    $readyDeadline = [DateTime]::UtcNow.AddSeconds(60)
    do {
        $null = docker exec $postgresContainer pg_isready -U $postgresUser -d $postgresDatabase 2>$null
        $postgresReady = $LASTEXITCODE -eq 0
        if (-not $postgresReady) {
            Start-Sleep -Seconds 2
        }
    } while (-not $postgresReady -and [DateTime]::UtcNow -lt $readyDeadline)
    if (-not $postgresReady) {
        throw "PostgreSQL did not become ready within 60 seconds."
    }

    $databaseExists = docker exec $postgresContainer psql `
        -U $postgresUser `
        -d $postgresDatabase `
        -tAc "SELECT 1 FROM pg_database WHERE datname = '$demoDatabase'"
    if ($LASTEXITCODE -ne 0) {
        throw "Could not check whether the demo database exists."
    }
    if ([string]::IsNullOrWhiteSpace($databaseExists) -or $databaseExists.Trim() -ne "1") {
        Write-Host "Creating demo database '$demoDatabase'..."
        docker exec $postgresContainer psql `
            -U $postgresUser `
            -d $postgresDatabase `
            -v ON_ERROR_STOP=1 `
            -c "CREATE DATABASE $demoDatabase"
        if ($LASTEXITCODE -ne 0) {
            throw "Could not create the demo database."
        }
    }

    $existingMailHog = docker container ls --all `
        --filter "name=^/$mailHogContainer$" `
        --format "{{.Names}}"
    if ($LASTEXITCODE -ne 0) {
        throw "Could not check whether the MailHog container exists."
    }

    if (-not [string]::IsNullOrWhiteSpace($existingMailHog) -and
        $existingMailHog.Trim() -eq $mailHogContainer) {
        $mailHogInfo = docker inspect --format '{{.Config.Image}}|{{.State.Running}}' $mailHogContainer
        if ($LASTEXITCODE -ne 0) {
            throw "Could not inspect the existing MailHog container."
        }
        if (-not $mailHogInfo.StartsWith("mailhog/mailhog:v1.0.1|")) {
            throw "Container '$mailHogContainer' exists but is not the expected MailHog container."
        }
        if ($mailHogInfo.EndsWith("|false")) {
            Write-Host "Starting existing MailHog..."
            docker start $mailHogContainer | Out-Null
            if ($LASTEXITCODE -ne 0) {
                throw "Could not start the existing MailHog container."
            }
        }
    }
    else {
        Write-Host "Starting MailHog..."
        docker run --detach `
            --name $mailHogContainer `
            --publish 127.0.0.1:1025:1025 `
            --publish 127.0.0.1:8025:8025 `
            mailhog/mailhog:v1.0.1 | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw "Could not start MailHog. Check whether ports 1025 and 8025 are available."
        }
    }

    foreach ($port in @(1025, 8025)) {
        $mapping = docker port $mailHogContainer "$port/tcp"
        if ($LASTEXITCODE -ne 0 -or $mapping -notmatch "^127\.0\.0\.1:$port$") {
            throw "MailHog must bind 127.0.0.1:$port. Recreate '$mailHogContainer' with the documented local-only port mapping."
        }
    }

    $escapedPassword = $postgresPassword.Replace("'", "''")
    $previousEnvironment = @{
        ConnectionStrings__Postgres = [Environment]::GetEnvironmentVariable("ConnectionStrings__Postgres", "Process")
        Jwt__Key = [Environment]::GetEnvironmentVariable("Jwt__Key", "Process")
        ASPNETCORE_ENVIRONMENT = [Environment]::GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Process")
        ASPNETCORE_URLS = [Environment]::GetEnvironmentVariable("ASPNETCORE_URLS", "Process")
    }

    $keyBytes = New-Object byte[] 48
    $random = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try {
        $random.GetBytes($keyBytes)
    }
    finally {
        $random.Dispose()
    }

    $env:ConnectionStrings__Postgres = "Host=localhost;Port=5432;Database=$demoDatabase;Username=$postgresUser;Password='$escapedPassword'"
    $env:Jwt__Key = [Convert]::ToBase64String($keyBytes)
    $env:ASPNETCORE_ENVIRONMENT = "Development"
    $env:ASPNETCORE_URLS = "http://localhost:$ApiPort"

    Write-Host "Starting API at http://localhost:$ApiPort. MailHog inbox: http://127.0.0.1:8025"
    Write-Host "Press Ctrl+C to stop the API; PostgreSQL, MailHog, and demo data will be kept."
    try {
        dotnet run --project $apiProject
        if ($LASTEXITCODE -ne 0 -and [uint32]$LASTEXITCODE -ne 3221225786) {
            throw "The API exited with code $LASTEXITCODE."
        }
    }
    finally {
        foreach ($name in $previousEnvironment.Keys) {
            [Environment]::SetEnvironmentVariable($name, $previousEnvironment[$name], "Process")
        }
    }
}
finally {
    Pop-Location
}

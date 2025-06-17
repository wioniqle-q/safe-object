#!/usr/bin/env pwsh

param(
    [string]$Configuration = "Release",
    [string]$Verbosity = "normal",
    [switch]$SkipTests,
    [switch]$SkipPublish
)

$ErrorActionPreference = "Stop"

Write-Host "Starting build process..." -ForegroundColor Green

$SolutionFile = "Acl.Fs.sln"
$SampleProjects = @(
    "samples\Acl.Fs.AesGcm.Sample\Acl.Fs.AesGcm.Sample.csproj"
)
$SourceProjects = @(
    "src\Acl.Fs.Abstractions\Acl.Fs.Abstractions.csproj",
    "src\Acl.Fs.Core\Acl.Fs.Core.csproj",
    "src\Acl.Fs.Stream\Acl.Fs.Stream.csproj",
    "src\Acl.Fs.Native\Acl.Fs.Native.csproj",
)
$TestProjects = @(
    "tests\Acl.Fs.Core.UnitTests\Acl.Fs.Core.UnitTests.csproj",
    "tests\Acl.Fs.Stream.UnitTests\Acl.Fs.Stream.UnitTests.csproj",
    "tests\Acl.Fs.Native.UnitTests\Acl.Fs.Native.UnitTests.csproj"
)

Write-Host "Cleaning solution..." -ForegroundColor Yellow
try {
    dotnet clean $SolutionFile --verbosity $Verbosity
    if ($LASTEXITCODE -ne 0) { throw "Clean failed" }
    Write-Host "Clean completed successfully" -ForegroundColor Green
}
catch {
    Write-Error "Clean failed - $_"
    exit 1
}

Write-Host "Restoring NuGet packages..." -ForegroundColor Yellow
try {
    dotnet restore $SolutionFile --verbosity $Verbosity
    if ($LASTEXITCODE -ne 0) { throw "Restore failed" }
    Write-Host "Package restore completed successfully" -ForegroundColor Green
}
catch {
    Write-Error "Package restore failed - $_"
    exit 1
}

Write-Host "Building source projects in Release mode..." -ForegroundColor Yellow
foreach ($project in $SourceProjects) {
    if (Test-Path $project) {
        try {
            Write-Host "  Building $project..." -ForegroundColor Cyan
            dotnet build $project --configuration Release --verbosity $Verbosity --no-restore
            if ($LASTEXITCODE -ne 0) { throw "Build failed for $project" }
        }
        catch {
            Write-Error "Build failed for $project - $_"
            exit 1
        }
    }
    else {
        Write-Warning "Source project not found: $project"
    }
}
Write-Host "Source projects built successfully" -ForegroundColor Green

Write-Host "Building sample projects in Debug mode..." -ForegroundColor Yellow
foreach ($project in $SampleProjects) {
    if (Test-Path $project) {
        try {
            Write-Host "  Building $project..." -ForegroundColor Cyan
            dotnet build $project --configuration Debug --verbosity $Verbosity --no-restore
            if ($LASTEXITCODE -ne 0) { throw "Build failed for $project" }
        }
        catch {
            Write-Error "Build failed for $project - $_"
            exit 1
        }
    }
    else {
        Write-Warning "Sample project not found: $project"
    }
}
Write-Host "Sample projects built successfully" -ForegroundColor Green

Write-Host "Building test projects in Release mode..." -ForegroundColor Yellow
foreach ($project in $TestProjects) {
    if (Test-Path $project) {
        try {
            Write-Host "Building $project..." -ForegroundColor Cyan
            dotnet build $project --configuration Release --verbosity $Verbosity --no-restore
            if ($LASTEXITCODE -ne 0) { throw "Build failed for $project" }
        }
        catch {
            Write-Error "Build failed for $project - $_"
            exit 1
        }
    }
    else {
        Write-Warning "Test project not found: $project"
    }
}
Write-Host "Test projects built successfully" -ForegroundColor Green

if (-not $SkipTests) {
    Write-Host "Running tests..." -ForegroundColor Yellow
    foreach ($project in $TestProjects) {
        if (Test-Path $project) {
            try {
                Write-Host "Running tests for $project..." -ForegroundColor Cyan
                dotnet test $project --configuration Release --no-build --verbosity $Verbosity --logger "console;verbosity=normal"
                if ($LASTEXITCODE -ne 0) { throw "Tests failed for $project" }
            }
            catch {
                Write-Error "Tests failed for $project - $_"
                exit 1
            }
        }
    }
    Write-Host "All tests passed successfully" -ForegroundColor Green
}
else {
    Write-Host "Skipping tests" -ForegroundColor Yellow
}

if (-not $SkipPublish) {
    Write-Host "Publishing projects for current platform..." -ForegroundColor Yellow
    
    $runtime = ""
    if ($IsWindows -or $env:OS -eq "Windows_NT") {
        $runtime = "win-x64"
    }
    elseif ($IsLinux) {
        $runtime = "linux-x64"
    }
    elseif ($IsMacOS) {
        $runtime = "osx-x64"
    }
    else {
        Write-Warning "Unknown platform, using portable publish"
        $runtime = "portable"
    }
    
    Write-Host "Detected runtime: $runtime" -ForegroundColor Cyan
    
    Write-Host "Restoring packages with runtime identifier..." -ForegroundColor Cyan
    try {
        dotnet restore $SolutionFile --runtime $runtime --verbosity $Verbosity
        if ($LASTEXITCODE -ne 0) { throw "Restore failed" }
    }
    catch {
        Write-Error "Restore failed - $_"
        exit 1
    }
    
    foreach ($project in $SourceProjects) {
        if (Test-Path $project) {
            try {
                Write-Host "Publishing $project..." -ForegroundColor Cyan
                if ($runtime -eq "portable") {
                    dotnet publish $project --configuration Release --verbosity $Verbosity --output "artifacts\$runtime"
                }
                else {
                    dotnet publish $project --configuration Release --runtime $runtime --self-contained false --verbosity $Verbosity --output "artifacts\$runtime"
                }
                if ($LASTEXITCODE -ne 0) { throw "Publish failed for $project" }
            }
            catch {
                Write-Error "Publish failed for $project - $_"
                exit 1
            }
        }
    }
    Write-Host "All projects published successfully" -ForegroundColor Green
    Write-Host "Binaries available in 'artifacts\$runtime' folder" -ForegroundColor Cyan
}
else {
    Write-Host "Skipping publish" -ForegroundColor Yellow
}

Write-Host "Build process completed successfully!" -ForegroundColor Green

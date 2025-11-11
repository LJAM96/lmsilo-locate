# Deployment and Continuous Integration

## Overview

This document covers the build, release, and deployment process for GeoLens, including GitHub Actions CI/CD pipeline, versioning strategy, and installer creation.

---

## 1. Version Management

### Semantic Versioning

GeoLens follows [Semantic Versioning 2.0.0](https://semver.org/):

```
MAJOR.MINOR.PATCH

Examples:
1.0.0  - Initial release
1.1.0  - New feature (heatmap visualization)
1.1.1  - Bug fix (cache corruption)
2.0.0  - Breaking change (new cache format)
```

### Version File

```xml
<!-- Directory.Build.props (project root) -->
<Project>
  <PropertyGroup>
    <Version>1.0.0</Version>
    <AssemblyVersion>1.0.0.0</AssemblyVersion>
    <FileVersion>1.0.0.0</FileVersion>
    <InformationalVersion>1.0.0</InformationalVersion>
  </PropertyGroup>
</Project>
```

### Automated Version Bumping

```yaml
# .github/workflows/version-bump.yml
name: Version Bump

on:
  workflow_dispatch:
    inputs:
      bump_type:
        description: 'Version bump type'
        required: true
        type: choice
        options:
          - patch
          - minor
          - major

jobs:
  bump:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3

      - name: Bump version
        run: |
          # Read current version
          CURRENT=$(grep -oP '<Version>\K[^<]+' Directory.Build.props)

          # Calculate new version
          IFS='.' read -r MAJOR MINOR PATCH <<< "$CURRENT"

          if [ "${{ github.event.inputs.bump_type }}" == "major" ]; then
            MAJOR=$((MAJOR + 1))
            MINOR=0
            PATCH=0
          elif [ "${{ github.event.inputs.bump_type }}" == "minor" ]; then
            MINOR=$((MINOR + 1))
            PATCH=0
          else
            PATCH=$((PATCH + 1))
          fi

          NEW_VERSION="$MAJOR.$MINOR.$PATCH"

          # Update file
          sed -i "s/<Version>.*<\/Version>/<Version>$NEW_VERSION<\/Version>/" Directory.Build.props
          sed -i "s/<AssemblyVersion>.*<\/AssemblyVersion>/<AssemblyVersion>$NEW_VERSION.0<\/AssemblyVersion>/" Directory.Build.props

          # Commit and tag
          git config user.name "GitHub Actions"
          git config user.email "actions@github.com"
          git add Directory.Build.props
          git commit -m "chore: bump version to $NEW_VERSION"
          git tag "v$NEW_VERSION"
          git push origin main --tags
```

---

## 2. GitHub Actions CI Pipeline

### Build and Test Workflow

```yaml
# .github/workflows/build.yml
name: Build and Test

on:
  push:
    branches: [ main, develop ]
  pull_request:
    branches: [ main ]

env:
  DOTNET_VERSION: '9.0.x'
  BUILD_CONFIGURATION: 'Release'

jobs:
  build:
    runs-on: windows-latest

    steps:
    - name: Checkout repository
      uses: actions/checkout@v3
      with:
        fetch-depth: 0 # Full history for version info

    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: ${{ env.DOTNET_VERSION }}

    - name: Restore dependencies
      run: dotnet restore GeoLens.sln

    - name: Build solution
      run: dotnet build GeoLens.sln --configuration ${{ env.BUILD_CONFIGURATION }} --no-restore

    - name: Run unit tests
      run: dotnet test GeoLens.Tests/GeoLens.Tests.csproj --configuration ${{ env.BUILD_CONFIGURATION }} --no-build --filter "Category=Unit" --logger "trx;LogFileName=unit-test-results.trx"

    - name: Publish test results
      uses: dorny/test-reporter@v1
      if: always()
      with:
        name: Unit Test Results
        path: '**/unit-test-results.trx'
        reporter: dotnet-trx

    - name: Upload build artifacts
      uses: actions/upload-artifact@v3
      with:
        name: geolens-build-${{ github.sha }}
        path: |
          **/bin/Release/**
          !**/bin/Release/**/ref/**
          !**/bin/Release/**/*.pdb
        retention-days: 7

  integration-test:
    runs-on: windows-latest
    needs: build

    steps:
    - name: Checkout repository
      uses: actions/checkout@v3

    - name: Setup Python
      uses: actions/setup-python@v4
      with:
        python-version: '3.11'

    - name: Install Python dependencies
      run: |
        pip install -r Core/requirements-cpu.txt

    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: ${{ env.DOTNET_VERSION }}

    - name: Run integration tests
      run: dotnet test GeoLens.Tests/GeoLens.Tests.csproj --configuration ${{ env.BUILD_CONFIGURATION }} --filter "Category=Integration" --logger "trx;LogFileName=integration-test-results.trx"

    - name: Publish integration test results
      uses: dorny/test-reporter@v1
      if: always()
      with:
        name: Integration Test Results
        path: '**/integration-test-results.trx'
        reporter: dotnet-trx

  code-quality:
    runs-on: windows-latest

    steps:
    - name: Checkout repository
      uses: actions/checkout@v3

    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: ${{ env.DOTNET_VERSION }}

    - name: Run code formatting check
      run: dotnet format --verify-no-changes --verbosity diagnostic

    - name: Run code analysis
      run: dotnet build GeoLens.sln /p:EnforceCodeStyleInBuild=true /p:TreatWarningsAsErrors=true
```

---

## 3. Release Pipeline

### Create Release Workflow

```yaml
# .github/workflows/release.yml
name: Create Release

on:
  push:
    tags:
      - 'v*.*.*'

jobs:
  build-installer:
    runs-on: windows-latest

    steps:
    - name: Checkout repository
      uses: actions/checkout@v3

    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: '9.0.x'

    - name: Extract version from tag
      id: version
      run: |
        $VERSION = "${{ github.ref_name }}".TrimStart('v')
        echo "version=$VERSION" >> $env:GITHUB_OUTPUT

    - name: Update version in project
      run: |
        $VERSION = "${{ steps.version.outputs.version }}"
        (Get-Content Directory.Build.props) -replace '<Version>.*</Version>', "<Version>$VERSION</Version>" | Set-Content Directory.Build.props

    - name: Build application
      run: dotnet publish GeoLens.csproj --configuration Release --runtime win-x64 --self-contained false --output publish/

    - name: Download Python runtimes (placeholder)
      run: |
        # In production, download pre-built Python runtimes
        # For now, create placeholder directories
        New-Item -ItemType Directory -Path publish/runtime/python_cpu -Force
        New-Item -ItemType Directory -Path publish/runtime/python_cuda -Force
        New-Item -ItemType Directory -Path publish/runtime/python_rocm -Force

    - name: Download GeoCLIP models (placeholder)
      run: |
        # In production, download pre-cached models
        New-Item -ItemType Directory -Path publish/models/geoclip_cache -Force

    - name: Download map assets (placeholder)
      run: |
        # In production, download offline map tiles
        New-Item -ItemType Directory -Path publish/Assets/Maps -Force

    - name: Install Inno Setup
      run: |
        choco install innosetup -y

    - name: Create installer script
      run: |
        # Copy installer script template
        Copy-Item installer/setup.iss publish/setup.iss

        # Replace version placeholder
        $VERSION = "${{ steps.version.outputs.version }}"
        (Get-Content publish/setup.iss) -replace '{{VERSION}}', $VERSION | Set-Content publish/setup.iss

    - name: Build installer
      run: |
        & "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" publish/setup.iss

    - name: Calculate installer hash
      id: hash
      run: |
        $HASH = (Get-FileHash Output/GeoLensSetup.exe -Algorithm SHA256).Hash
        echo "sha256=$HASH" >> $env:GITHUB_OUTPUT

    - name: Create release notes
      run: |
        $VERSION = "${{ steps.version.outputs.version }}"
        @"
        # GeoLens v$VERSION

        ## Changes
        See [CHANGELOG.md](https://github.com/${{ github.repository }}/blob/main/CHANGELOG.md) for details.

        ## Installation
        1. Download GeoLensSetup.exe
        2. Run the installer
        3. Follow the setup wizard

        ## System Requirements
        - Windows 10 (21H2) or Windows 11
        - 8GB RAM minimum (16GB recommended)
        - 5GB free disk space
        - Optional: NVIDIA GPU (for faster predictions)

        ## Checksums
        SHA256: ${{ steps.hash.outputs.sha256 }}
        "@ | Out-File -FilePath release-notes.md

    - name: Create GitHub Release
      uses: softprops/action-gh-release@v1
      with:
        files: |
          Output/GeoLensSetup.exe
        body_path: release-notes.md
        draft: false
        prerelease: false
      env:
        GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}

    - name: Upload installer artifact
      uses: actions/upload-artifact@v3
      with:
        name: GeoLensSetup-v${{ steps.version.outputs.version }}
        path: Output/GeoLensSetup.exe
        retention-days: 90
```

---

## 4. Installer Creation (Inno Setup)

### Installer Script

```iss
; installer/setup.iss
#define MyAppName "GeoLens"
#define MyAppVersion "{{VERSION}}"
#define MyAppPublisher "Luke Mulvaney"
#define MyAppURL "https://github.com/yourusername/geolens"
#define MyAppExeName "GeoLens.exe"

[Setup]
AppId={{8F9A2C3E-4B1D-4E7A-9F2C-8D3A1B4C5E6F}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
AppUpdatesURL={#MyAppURL}/releases
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
LicenseFile=LICENSE.txt
OutputDir=Output
OutputBaseFilename=GeoLensSetup
SetupIconFile=Assets\icon_white.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
MinVersion=10.0.17763

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "quicklaunchicon"; Description: "{cm:CreateQuickLaunchIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked; OnlyBelowVersion: 6.1; Check: not IsAdminInstallMode

[Files]
; Main application
Source: "{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "*.dll"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs
Source: "*.json"; DestDir: "{app}"; Flags: ignoreversion
Source: "Assets\*"; DestDir: "{app}\Assets"; Flags: ignoreversion recursesubdirs

; Python runtimes (large!)
Source: "runtime\python_cpu\*"; DestDir: "{app}\runtime\python_cpu"; Flags: ignoreversion recursesubdirs; Components: runtime_cpu
Source: "runtime\python_cuda\*"; DestDir: "{app}\runtime\python_cuda"; Flags: ignoreversion recursesubdirs; Components: runtime_cuda
Source: "runtime\python_rocm\*"; DestDir: "{app}\runtime\python_rocm"; Flags: ignoreversion recursesubdirs; Components: runtime_rocm

; Core Python scripts
Source: "Core\*"; DestDir: "{app}\Core"; Flags: ignoreversion recursesubdirs

; GeoCLIP models
Source: "models\*"; DestDir: "{app}\models"; Flags: ignoreversion recursesubdirs

[Components]
Name: "runtime_cpu"; Description: "CPU-only runtime (800MB)"; Types: full compact custom; Flags: fixed
Name: "runtime_cuda"; Description: "NVIDIA GPU runtime (3GB)"; Types: full; Check: HasNvidiaGPU
Name: "runtime_rocm"; Description: "AMD GPU runtime (2.5GB)"; Types: full; Check: HasAMDGPU

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
function HasNvidiaGPU: Boolean;
var
  ResultCode: Integer;
begin
  Result := Exec('nvidia-smi', '', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0);
end;

function HasAMDGPU: Boolean;
var
  Output: AnsiString;
  ResultCode: Integer;
begin
  Result := False;
  if Exec(ExpandConstant('{sys}\wbem\wmic.exe'), 'path win32_VideoController get name', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    if ResultCode = 0 then
    begin
      // Check for AMD or Radeon in output
      Result := (Pos('AMD', Output) > 0) or (Pos('Radeon', Output) > 0);
    end;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    // Create cache directory
    CreateDir(ExpandConstant('{app}\cache'));

    // Set permissions (optional)
  end;
end;
```

---

## 5. Release Preparation Checklist

### Pre-Release Tasks

```markdown
## Release Checklist for v{VERSION}

### Code Freeze
- [ ] All features for this release merged to `main`
- [ ] No known critical bugs
- [ ] Code formatted (`dotnet format`)
- [ ] All tests passing

### Version Update
- [ ] Update version in `Directory.Build.props`
- [ ] Update CHANGELOG.md with release notes
- [ ] Update README.md if needed
- [ ] Git tag created: `v{VERSION}`

### Build Preparation
- [ ] Python CPU runtime prepared (800MB)
- [ ] Python CUDA runtime prepared (3GB)
- [ ] Python ROCm runtime prepared (2.5GB)
- [ ] GeoCLIP models downloaded (~500MB)
- [ ] Offline map assets prepared (~500MB)

### Testing
- [ ] Fresh install test on Windows 10
- [ ] Fresh install test on Windows 11
- [ ] Test on NVIDIA GPU machine
- [ ] Test on AMD GPU machine
- [ ] Test on CPU-only machine
- [ ] Test offline mode (no internet)
- [ ] Test all export formats
- [ ] Uninstaller test

### Documentation
- [ ] User guide updated
- [ ] API documentation generated
- [ ] Known issues documented
- [ ] Migration guide (if breaking changes)

### Release
- [ ] Push git tag to trigger release workflow
- [ ] Monitor GitHub Actions build
- [ ] Download and verify installer
- [ ] Publish GitHub Release
- [ ] Update website/download links
- [ ] Announce on social media/forums
```

---

## 6. Changelog Management

### CHANGELOG.md Format

```markdown
# Changelog

All notable changes to GeoLens will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]
### Added
- Feature currently in development

### Changed
- Changes to existing functionality

### Deprecated
- Features to be removed in future versions

### Removed
- Removed features

### Fixed
- Bug fixes

### Security
- Security fixes

## [1.1.0] - 2025-02-15
### Added
- Multi-image heatmap visualization
- Batch export to PDF with thumbnails
- Performance dashboard

### Changed
- Improved cache hit rate by 30%
- Updated GeoCLIP model to v1.2.0

### Fixed
- Memory leak in map provider
- EXIF parsing for HEIC images

## [1.0.1] - 2025-01-20
### Fixed
- Python service startup on Windows 10
- Cache corruption on unexpected shutdown

## [1.0.0] - 2025-01-10
### Added
- Initial release
- AI-powered image geolocation
- 3D globe visualization
- EXIF GPS extraction
- Prediction caching
- CSV/PDF/KML export
```

---

## 7. Hotfix Process

### Emergency Bug Fix Workflow

```bash
# 1. Create hotfix branch from tag
git checkout -b hotfix/1.0.1 v1.0.0

# 2. Fix the bug
# ... make changes ...

# 3. Commit fix
git add .
git commit -m "fix: critical bug in cache service"

# 4. Update version (patch bump)
# Edit Directory.Build.props: 1.0.0 -> 1.0.1

# 5. Create tag
git tag v1.0.1

# 6. Push branch and tag
git push origin hotfix/1.0.1
git push origin v1.0.1

# 7. Merge back to main
git checkout main
git merge hotfix/1.0.1
git push origin main

# 8. GitHub Actions will automatically build and release v1.0.1
```

---

## 8. Dependency Management

### Keeping Dependencies Updated

```yaml
# .github/workflows/dependency-update.yml
name: Update Dependencies

on:
  schedule:
    - cron: '0 0 * * 1' # Weekly on Monday
  workflow_dispatch:

jobs:
  update-dotnet:
    runs-on: windows-latest

    steps:
    - uses: actions/checkout@v3

    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: '9.0.x'

    - name: Update NuGet packages
      run: |
        dotnet list package --outdated
        dotnet outdated -u # Using dotnet-outdated-tool

    - name: Create pull request
      uses: peter-evans/create-pull-request@v5
      with:
        commit-message: 'chore: update NuGet dependencies'
        title: 'Update NuGet Dependencies'
        body: 'Automated dependency update'
        branch: deps/nuget-update
```

---

## 9. Build Artifacts Retention

### Artifact Storage Strategy

| Artifact Type | Retention Period | Storage Location |
|---------------|------------------|------------------|
| Development builds | 7 days | GitHub Actions |
| PR builds | 7 days | GitHub Actions |
| Release candidates | 30 days | GitHub Actions |
| Official releases | Indefinite | GitHub Releases |
| Nightly builds | 14 days | GitHub Actions |

---

## 10. Post-Release Monitoring

### Release Health Metrics

```markdown
## Metrics to Track (Manual)

### Installation Success Rate
- Track via GitHub download count
- Monitor issues related to installation

### Crash Reports
- Encourage users to report crashes via GitHub Issues
- Tag issues with `bug` and `crash` labels

### Performance Metrics
- Community-reported performance issues
- Hardware-specific problems

### User Feedback
- GitHub Discussions
- Issue tracker
- Community forums
```

---

This deployment guide ensures reliable, repeatable releases and smooth CI/CD processes.

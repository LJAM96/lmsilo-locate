# Troubleshooting Error 0x80670016 - Windows App SDK Initialization Failure

## Error Description

**Exit Code**: 2154233878 (0x80670016)
**Meaning**: "Unable to resolve package dependency conditions"
**Cause**: Windows App SDK runtime not installed or version mismatch

## Root Cause

GeoLens is configured as an **unpackaged WinUI3 application** with:
- `WindowsPackageType = None`
- `WindowsAppSDKSelfContained = false`

This configuration requires the **Windows App SDK runtime to be installed separately** on the machine where the app runs.

## Solution 1: Install Windows App SDK Runtime (RECOMMENDED)

### Steps:

1. **Download the Windows App SDK 1.8.3 Runtime Installer**:
   - **x64** (most common): https://aka.ms/windowsappsdk/1.8/1.8.251106002/windowsappruntimeinstall-x64.exe
   - **x86**: https://aka.ms/windowsappsdk/1.8/1.8.251106002/windowsappruntimeinstall-x86.exe
   - **ARM64**: https://aka.ms/windowsappsdk/1.8/1.8.251106002/windowsappruntimeinstall-arm64.exe

2. **Run the installer** as Administrator

3. **Restore NuGet packages** (updated to SDK 1.8.3):
   ```bash
   dotnet restore GeoLens.sln
   ```

4. **Rebuild the application**:
   ```bash
   dotnet build GeoLens.sln
   ```

5. **Run GeoLens**:
   ```bash
   dotnet run --project GeoLens.csproj
   ```

### Why This Is Recommended:
- ✅ Smaller application size (runtime shared across apps)
- ✅ Automatic runtime updates via Windows Update
- ✅ Maintains COM activation fix (as per CLAUDE.md)
- ✅ Standard deployment model for production apps

## Solution 2: Switch to Self-Contained Mode (NOT RECOMMENDED)

⚠️ **WARNING**: CLAUDE.md explicitly states `WindowsAppSDKSelfContained=false` is required to fix WinUI3 COM activation issues in Release builds.

If you still want to try self-contained mode:

1. Edit `GeoLens.csproj` and change:
   ```xml
   <WindowsAppSDKSelfContained>true</WindowsAppSDKSelfContained>
   ```

2. Restore and rebuild:
   ```bash
   dotnet restore GeoLens.sln
   dotnet build GeoLens.sln
   ```

### Trade-offs:
- ⚠️ May reintroduce COM activation crashes in Release builds
- ⚠️ Larger application size (~50MB extra)
- ⚠️ No automatic runtime updates
- ✅ No separate runtime installation needed

## What Was Changed

The following update was made to fix the version mismatch:

**File**: `GeoLens.csproj`

```xml
<!-- OLD (Windows App SDK 1.6) -->
<PackageReference Include="Microsoft.WindowsAppSDK" Version="1.6.240923002" />

<!-- NEW (Windows App SDK 1.8.3 - Latest Stable) -->
<PackageReference Include="Microsoft.WindowsAppSDK" Version="1.8.251106002" />
```

## Verification Steps

After installing the runtime and rebuilding:

1. The app should launch without exit code 0x80670016
2. You should see the **LoadingPage** with progress indicators
3. The Python service should start and health check should pass
4. The **MainPage** should display with the map interface

## Additional Notes

- The Windows App SDK runtime is a **system-wide installation**
- It's required for all unpackaged WinUI3 apps on the machine
- Once installed, it benefits all WinUI3 development
- The runtime installer can be distributed with your application for end-users

## Related Documentation

- [Windows App SDK Downloads](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/downloads)
- [Use Windows App SDK Runtime (Unpackaged Apps)](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/use-windows-app-sdk-run-time)
- [Check Installed Runtime Versions](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/check-windows-app-sdk-versions)

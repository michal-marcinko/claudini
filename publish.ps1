param([string]$Rid = "win-x64")
dotnet publish src/CcLauncher.App/CcLauncher.App.csproj `
  -c Release `
  -r $Rid `
  -o publish/$Rid

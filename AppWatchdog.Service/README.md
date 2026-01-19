dotnet publish AppWatchdog.Service.csproj ^
  -c Release ^
  -r win-x64 ^
  --self-contained false ^
  /p:PublishSingleFile=true ^
  /p:PublishTrimmed=false ^
  -o publish\Service
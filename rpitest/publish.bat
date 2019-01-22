dotnet publish -r linux-arm /p:ShowLinkerSizeComparison=true 
pushd .\bin\Debug\netcoreapp2.1\linux-arm\publish
REM Publish the .NET Core Framework ONCE, then just move the EXE
REM pscp -pw raspberry -v -r .\* pi@crowpi.lan:/home/pi/Desktop/rpitest
pscp -pw raspberry -v -r .\rpitest pi@crowpi.lan:/home/pi/Desktop/rpitest
popd
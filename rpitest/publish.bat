dotnet publish -r linux-arm /p:ShowLinkerSizeComparison=true 
pushd .\bin\Debug\netcoreapp2.2\linux-arm\publish

REM Publish the .NET Core Framework ONCE, then just move the EXE

REM Putty 
REM pscp -pw raspberry -v -r .\* pi@crowpi.lan:/home/pi/Desktop/rpitest
REM pscp -pw raspberry -v -r .\rpitest pi@crowpi.lan:/home/pi/Desktop/rpitest

REM Built-in OpenSSH
REM scp -r .\* pi@crowpi.lan:/home/pi/Desktop/rpitest
scp -r .\rpitest* pi@crowpi.lan:/home/pi/Desktop/rpitest

popd
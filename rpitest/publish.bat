dotnet publish -r linux-arm /p:ShowLinkerSizeComparison=true 
pushd .\bin\Debug\netcoreapp2.1\linux-arm\publish
pscp -pw raspberry -v -r .\* pi@crowpi:/home/pi/Desktop/rpitest
popd
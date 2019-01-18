dotnet publish -r linux-arm /p:ShowLinkerSizeComparison=true 
pushd .\bin\Debug\netcoreapp2.2\linux-arm\publish
pscp -pw raspberry -v -r .\* pi@crowpi:/home/pi/Desktop/LCDDisplay 
popd
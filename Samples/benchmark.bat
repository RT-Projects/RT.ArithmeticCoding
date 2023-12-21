rem 212,279 bytes, 93ms:
C:\Apps\7Zip\7z a -mx9 prejudice.ultra.7z prejudice.txt

rem 164,086 bytes, 61ms:
C:\Apps\7Zip\7z a -m0=PPMd prejudice.ppmd.7z prejudice.txt

dotnet publish --configuration Release Beat7zip\Beat7zip.csproj
dotnet publish --configuration Release Beat7zipStandalone\Beat7zipStandalone.csproj

rem 171,553 bytes, 5100ms:
..\Builds\Release-publish\Beat7zip.exe c prejudice.txt prejudice.beat.bin
..\Builds\Release-publish\Beat7zipStandalone.exe c prejudice.txt prejudice.beatsa.bin

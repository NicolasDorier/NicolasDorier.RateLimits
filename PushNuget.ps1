Remove-Item "lib\bin\release\" -Recurse -Force
dotnet pack lib --configuration Release
dotnet nuget push "lib\bin\Release\*.nupkg" --source "https://api.nuget.org/v3/index.json"
$ver = ((Get-ChildItem .\lib\bin\release\*.nupkg)[0].Name -replace '[^\d]*\.(\d+(\.\d+){1,4}).*', '$1')
git tag -a "v$ver" -m "$ver"
git push --tags

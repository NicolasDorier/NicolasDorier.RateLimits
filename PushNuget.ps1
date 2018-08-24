Remove-Item "src\bin\release\" -Recurse -Force
dotnet pack --configuration Release
dotnet nuget push "src\bin\Release\" --source "https://api.nuget.org/v3/index.json"
$ver = ((Get-ChildItem .src\bin\release\*.nupkg)[0].Name -replace '[^\d]*\.(\d+(\.\d+){1,4}).*', '$1')
git tag -a "v$ver" -m "$ver"
git push --tags

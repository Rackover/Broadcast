cd BroadcastClient
dotnet publish -c Release
copy bin\Release\netcoreapp3.0\publish\BroadcastShared.dll ..\Release\.
copy bin\Release\netcoreapp3.0\publish\BroadcastClient.dll ..\Release\.
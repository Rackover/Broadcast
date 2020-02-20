cd BroadcastClient
dotnet publish -c Release -f netcoreapp3.0
copy bin\Release\netcoreapp3.0\publish\BroadcastShared.dll ..\Release\.
copy bin\Release\netcoreapp3.0\publish\BroadcastClient.dll ..\Release\.
dotnet publish -c Release -f net471
copy bin\Release\net471\publish\BroadcastShared.dll ..\Release\.
copy bin\Release\net471\publish\BroadcastClient.dll ..\Release\.
PAUSE
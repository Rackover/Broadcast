cd BroadcastClient
dotnet publish -c Release -f netcoreapp3.1
mkdir ..\Release\netcoreapp3.1
copy bin\Release\netcoreapp3.1\publish\BroadcastShared.dll ..\Release\netcoreapp3.1\.
copy bin\Release\netcoreapp3.1\publish\BroadcastClient.dll ..\Release\netcoreapp3.1\.
dotnet publish -c Release -f net471
mkdir ..\Release\net471
copy bin\Release\net471\publish\BroadcastShared.dll ..\Release\net471\.
copy bin\Release\net471\publish\BroadcastClient.dll ..\Release\net471\.
PAUSE
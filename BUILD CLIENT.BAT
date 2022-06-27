@echo off
cd BroadcastClient
dotnet publish -c Release -f net6.0
mkdir ..\Release\net6.0
copy bin\Release\net6.0\publish\BroadcastShared.dll ..\Release\net6.0\.
copy bin\Release\net6.0\publish\BroadcastClient.dll ..\Release\net6.0\.
dotnet publish -c Release -f net471
mkdir ..\Release\net471
copy bin\Release\net471\publish\BroadcastShared.dll ..\Release\net471\.
copy bin\Release\net471\publish\BroadcastClient.dll ..\Release\net471\.
cd ..
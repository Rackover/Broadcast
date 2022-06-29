@echo off
setlocal enabledelayedexpansion 
cd BroadcastServer

set list[0]=win-x64
set list[1]=win-x86
set list[2]=linux-arm
set list[3]=linux-arm64
set list[4]=linux-x64

for /l %%n in (0,1,4) do ( 
   rd /s /q "bin\Release\net6.0\!list[%%n]!\publish"
   dotnet publish -c Release --framework net6.0 --self-contained -r !list[%%n]! 
   rd /s /q "..\Release\!list[%%n]!"
   mkdir ..\Release\!list[%%n]! 
   copy bin\Release\net6.0\!list[%%n]!\publish ..\Release\!list[%%n]!\.
   del "..\Release\!list[%%n]!.zip"
   "C:\Program Files\7-Zip\7z.exe" a "..\Release\!list[%%n]!.zip" "..\Release\!list[%%n]!"
)
cd ..
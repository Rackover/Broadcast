@echo off
setlocal enabledelayedexpansion 
cd Broadcast

set list[0]=win-x64
set list[1]=win-x86
set list[2]=linux-arm
set list[3]=linux-x64

for /l %%n in (0,1,3) do ( 
   rd /s /q "bin\Release\netcoreapp3.0\!list[%%n]!\publish"
   dotnet publish -c Release -r !list[%%n]! 
   rd /s /q "..\Release\!list[%%n]!"
   mkdir ..\Release\!list[%%n]! 
   copy bin\Release\netcoreapp3.0\!list[%%n]!\publish ..\Release\!list[%%n]!\.
   del "..\Release\!list[%%n]!.zip"
   "C:\Program Files\7-Zip\7z.exe" a "..\Release\!list[%%n]!.zip" "..\Release\!list[%%n]!"
)

pause
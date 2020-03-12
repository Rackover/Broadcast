@echo off
setlocal enabledelayedexpansion 
cd Broadcast

set list[0]=win-x64
set list[1]=win-x86
set list[2]=linux-arm
set list[3]=linux-x64

for /l %%n in (0,1,3) do ( 
   dotnet publish -c Release -r !list[%%n]! 
   mkdir ..\Release\!list[%%n]! 
   copy bin\Release\netcoreapp3.0\!list[%%n]!\publish ..\Release\!list[%%n]!\.
)


pause
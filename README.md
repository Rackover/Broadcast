# Broadcast
Master server broadcasting for any game

# How to use

## Client
To connect to Broadcast with your application, check the Releases page and download either netcoreapp3.0 (if your application is .NET Core compatible) or net471 (if your application is .NET Framework 4.X compatible, like Unity is).
Put the two libraries (BroadcastClient and BroadcastShared) anywhere in your project to use them.

### Usage 
| I want to        | Function           | Returns  |
| ------------- |:-------------:| -----:|
| Start the client (mandatory)      | BroadcastClient.Start(string broadcastServerAddress, string nameOfYourGame) | Nothing |
| Get the list of lobbies for my game   | BroadcastClient.GetLobbyList() | Read-only list of <Lobby> object 
| Fetch the list of lobbies from the server   | BroadcastClient.UpdateLobbyList(Query customQuery=null) | Nothing |
| Create a new lobby      | BroadcastClient.CreateLobby(...) | The lobby you just created, but with an ID delivered by the server |
| Update information for my lobby | BroadcastClient.UpdateLobby(<Lobby> object) | Nothing |
| Kill my lobby and remove it from Broadcast | BroadcastClient.DestroyLobby(uint lobbyID) | Nothing |
 
## Server
Download a binary from the /Releases section according to what you have
- winx86 for Windows 32 bit
- winx64 for Windows 64 bit
- linux-x64 for Linux 64 bit, any distro
- linux-arm for Linux ARM, like Raspbian

Unzip and run `Broadcast`(.exe). This should work out of the box.

# Notes
- Broadcast runs on port 4004
- Lobby that hasn't sent trace of life in the past 30 seconds are cleaned up and destroyed from the server
- Broadcast uses the major version number (X) to signal compatibility break. Minor version number and revision number (Y and Z) are usually quality of life improvements or bugfixes, but no protocol change.
- Broadcast returns maximum 200 lobbies when queried

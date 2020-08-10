# Broadcast
Master server broadcasting for any game

# How to use

## Client
To connect to Broadcast with your application, check the Releases page and download **either**:
- netcoreapp3.0 (if your application is .NET Core compatible) 
- net471 (if your application is .NET Framework 4.X compatible, like Unity is).

Put the two libraries (BroadcastClient and BroadcastShared) anywhere in your project to use them.

### Usage 
Before using the client, you have to instantiate it.

```
var client = new BroadcastClient(
        string broadcastServerAddress, 
        string nameOfYourGame, 
        bool allowOnlyIPV4
)
```

Notes : 
- If you choose to allow IPV6 (by setting `allowOnlyIPV4` to `false`), DNS resolution **may be extremly slow** due to a Microsoft bug. 
- The client does **not** connect to the server upon construction. It will try establishing a connection when contacting the server. That connection is managed by the BroadcastClient - in case of connection loss, it will reconnect automatically when needed.

| I want to        | Function           | Returns  | Info |
| ------------- |:-------------:| -----:| -----:|
| Get the list of lobbies for my game   | client.GetLobbyList() | Read-only list of <Lobby> object | Returns the local list, does not connect to the server. Use `UpdateLobbyList` to update that list. 
| Fetch the list of lobbies from the server   | client.UpdateLobbyList(Query customQuery=null) | Nothing | |
| Create a new lobby      | client.CreateLobby(...) | The lobby you just created, but with an ID delivered by the server | |
| Update information for my lobby | client.UpdateLobby(<Lobby> object) | Nothing | |
| Kill my lobby and remove it from Broadcast | client.DestroyLobby(uint lobbyID) | Nothing | |
| Hole-punch the host to allow myself through the host's NAT | client.PunchLobby(uint lobbyID) | Nothing | Only works if the lobby uses `ETransportProtocol.UDP` |
 
## Server
Download a binary from the /Releases section according to what you have
- winx86 for Windows 32 bit
- winx64 for Windows 64 bit
- linux-x64 for Linux 64 bit, any distro
- linux-arm for Linux ARM, like Raspbian

Unzip and run `Broadcast`(.exe). This should work out of the box.

# Notes
- Broadcast runs on port 1000{`a`} where `a` is the Broadcast version. *Example: Broadcast v6 runs on port 10006*.
- Lobby that hasn't sent trace of life in the past 30 seconds are cleaned up and destroyed from the server
- Broadcast uses the major version number (X) to signal compatibility break. Minor version number and revision number (Y and Z) are usually quality of life improvements or bugfixes, but no protocol change.
- Broadcast returns maximum 200 lobbies when queried

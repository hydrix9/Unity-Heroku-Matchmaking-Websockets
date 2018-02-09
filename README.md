# Unity-Heroku-Matchmaking-Websockets
Example of Matchmaking in Unity3d with Heroku and Websockets, where the server is a .NET Core 2.0 console app.


## Server

Server is deployed directly to Heroku [with this buildpack set](https://github.com/hydrix9/dotnet-buildpack-vs2017) with the files all in the top directory themselves (MMServer.csproj and .deployment should be on the top directory, for example).

## Client

Client is the file used in Unity3d. Plugins used are [More Effective Coroutines [FREE]](https://assetstore.unity.com/packages/tools/animation/more-effective-coroutines-free-54975) and that's it. 
This does everything up to receiving the games, and it then calls the onReceiveGames with a dictionary of games as the parameter. In my own implementation, the key is a GUID and the client connects to this using WebRTC signaling.

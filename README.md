# Augmenta Client C# SDK
The goal of this SDK is to make consuming the stream output of an Augmenta server as easy as possible. As of right now this only refers to the data emitted through websocket by the `Websocket Output`.

## Include the SDK in your project
### As a NuGet package
The SDK is available as a NuGet package.  
You can also use it locally by downloading the latest `.nupkg` file from the release page, and setting it up in your [NuGet local feed](https://learn.microsoft.com/en-us/nuget/hosting-packages/local-feeds).

### As a submodule
You can alternatively add this repository as a submodule into your project directly. Note that in that case you should also manually download and add the ZstdNet assemblies to your project.

## How to use
- Specialize Augmenta generic object classes with your own type for 3-floats vectors. You can also override some of their behavior by inheriting them. 
- You'll need to provide your own websocket implementation. Maybe your environment already provides one ?
- Instanciate your Client class.
- Use its method to parse incoming websocket messages.
- Use the received data to update whatever you need for your use-case.

For a full example usage, see the [Augmenta Unity Websocket Package](https://github.com/Augmenta-tech/AugmentaUnityWebsocket).

## Dependencies
- [JSONObject](https://github.com/mtschoen/JSONObject/tree/master)
- [ZstdNet](https://www.nuget.org/packages/ZstdNet)
# Building

This project is written in [F#](https://fsharp.org) and [.NET](https://dot.net).

- Install [.NET 8.0](https://dotnet.microsoft.com/en-us/download/dotnet)
- `cd` into this repository
- To build for Windows(x64) run this command:

```bash
dotnet build src/udpproxy/udpproxy.fsproj -c Release --no-incremental --runtime win-x64 --self-contained
dotnet publish src/udpproxy/udpproxy.fsproj -c Release --runtime win-x64 --self-contained -p:PublishSingleFile=true -o publish
```

- To build for Linux(x64) run this command:

```bash
dotnet build src/udpproxy/udpproxy.fsproj -c Release --no-incremental --runtime linux-x64 --self-contained
dotnet publish src/udpproxy/udpproxy.fsproj -c Release --runtime linux-x64 --self-contained -p:PublishSingleFile=true -o publish
```

- Resulting exes will be located in `publish` directory.

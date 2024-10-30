# uecho

`uecho` is small client/server utility that sends UDP packets and
receives them back. On receive it checks packet checksum to ensure
packet is not corrupted.


## Usage

```
USAGE: uecho [--help] [<subcommand> [<options>]]

SUBCOMMANDS:

    server <options>      Start in server mode.
    client <options>      Start in client mode.

    Use 'uecho <subcommand> --help' for additional information.

OPTIONS:

    --help                display this list of options.
```

- Run server with `.\uecho server --port 50000`;
- Run client with `.\uecho client --host 127.0.0.1 --port 50000`;

Now in client you can type pattern of packet to be sent to server. Pattern can be text or hex.

To send text pattern type: `text "Hello World " 123`. This will send `Hello World ` string multiplied to have exactly byte length of 123.

To send hex pattern type: `hex 0xaa 0xbb 0xcc 0xee 0xff 0x22 125`. This will send bytes `aabbcceeff22` multiplied to have byte length of 125.

To send random pattern type: `rnd 70`. This will send random bytes of length 70.

- Client sends packet;
- Server receives packet and checks checksum:
    - On bad checksum server does nothing;
    - On good checksum server sends packet back to client;
- Client receives packet back;
- Checks checksum:
- Logs checksum verification result to console;

# Pipelines reference

Pipeline defines *Forward* and *Reverse* action on UDP packet.

**Forward** is direction from client to some upstream. From your application to proxy to server.

**Reverse** is direction from upstream back to client, i.e., response packets. From server back to client.

Pipeline configuration is JSON object with properties.

## Common props

Each pipeline share basic properties.

| Key | Format | Default | Description |
|-----|--------|---------|-------------|
| `$type` | string | | Defines type of pipeline. |
| `$inverse` | bool | false | Set to `true` to inverse pipeline. *Forward* becomes *Reverse*, *Reverse* becomes *Forward*. |


## rndpad

`"$type": "rndpad"`

**Forward**: Pads packet with random bytes.

**Backward**: Removes padded bytes from packet.

| Key | Format | Description |
|-----|--------|-------------|
| `min` | integer | Minimum amount of bytes to pad packet with. |
| `max` | integer | Maximum amount of bytes to pad packet with. |

Values of `min`, `max` must be the same among two different hosts, otherwise, hosts won't be able to
pad/unpad each others packets.


## mailman

`"$type": "mailman"`

**Forward**: Send out packet to destination. If there are several endpoints configured --
random endpoint is choosen on each packet.

**Reverse**: Does nothing.

| Key | Format | Description |
|-----|--------|-------------|
| `output` | array of endpoints | Array of endpoints to send packet to. If more than one is specified then on each packet endpoint is randomly selected. |

See section **Endpoints** in [Configuration](configuration.md) for format of endpoint.

If you don't add this pipeline all incoming packets will be silently dropped.

You usually put this pipeline last.


## packetreturn

`"$type": "packetreturn"`

**Forward**: Does nothing.

**Reverse**: Sends packet back to client.

No configuration options.

If you don't add this pipeline then all response packets will be silently dropped.

You usually put this pipeline first.


## lz4

`"$type": "lz4"`

**Forward**: Compresses packet with LZ4 algorithm.

**Reverse**: Uncompresses packet.

| Key | Format | Description |
|-----|--------|-------------|
| `level` | integer | Compression level between 0 and and 12. Default 3. |


## aligner

`"$type": "aligner"`

**Forward**: Aligns packet length to specified alignment.

**Reverse**: Restores original packet length.

| Key | Format | Description |
|-----|--------|-------------|
| `alignBy` | interger | Alignement value in bytes. |

For example, if you set `"alignBy": 123` then packet length will be aligned to 123 bytes.


## aes

`"$type": "aes"`

**Forward**: Encrypts packet using AES algorithm and authenticates packet with HMAC-SHA256.

**Reverse**: Ensures packet is authenticated and decrypts it.

| Key | Format | Description |
|-----|--------|-------------|
| `aesKey` | base64 string | AES encryption key. Size must be 16 or 32 bytes. You must keep this key in secret. |
| `hmacKey` | base64 string | HMAC-SHA256 key. Size can be any. 16 or 32 bytes is good enough. You must keep this key in secret. |

You can run `udpproxy genkey` to generate key. Or use your favorite tool to generate symmetric keys.

Encryption and HMAC keys must be the same between two hosts in order for hosts to be able to encrypt/decrypts each others packets.


## Sample configuration

Client:

```json
{
  "inputPorts": [
    50000
  ],

  "pipeline": [
    { "$type": "packetreturn" },

    { "$type": "rndpad", "min": 10, "max": 513 },

    { "$type": "aes", "aesKey": "HTIZP1v+g0ddqsjdRGYS33vEfRdLdhnbXFBz2XfZV2g=", "hmacKey": "HLm2g61Wyobp56FZ/fejvXDGjc3m0y7zMEBIu3qyLsE=" },

    { "$type": "mailman", "output": [ "example.com:50001" ] }
  ]
}
```

Server:

```json
{
  "inputPorts": [
    50001
  ],

  "pipeline": [
    { "$type": "packetreturn" },

    { "$type": "aes", "$inverse": true, "aesKey": "HTIZP1v+g0ddqsjdRGYS33vEfRdLdhnbXFBz2XfZV2g=", "hmacKey": "HLm2g61Wyobp56FZ/fejvXDGjc3m0y7zMEBIu3qyLsE=" },

    { "$type": "rndpad", "$inverse": true, "min": 10, "max": 513 },

    { "$type": "mailman", "output": [ "second.example.com:8976" ] }
  ]
}
```

In this example,

Client pads packet, encrypts it, sends to example.com:50001.

Server decrypts packet, unpads it, sends to second.example.com:8976.

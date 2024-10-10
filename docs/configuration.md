# Configuration

Configuration is supplied via JSON file. File consists of JSON object.

```json
{
  "log": {},
  "cache": {},
  "conntrack": {},
  "dns": {},
  "inputPorts": [],
  "pipeline": []
}
```


### log

JSON object.

Configures log options.

| Key | Format | Default value | Description | Optional? |
|-----|--------|---------------|-------------|-----------|
| `level` | string of `"Verbose"`, `"Debug"`, `"Information"`, `"Warning"`, `"Error"`, `"Fatal"` | `"Information"` | Log level | Yes |
| `logFile` | string | null | Path to a log file | Yes |
| `consoleLog` | bool | true | Is log to stdout enabled | Yes


### cache

JSON object.

Configures cache options. Cache is used for connection tracking and output sockets.

| Key | Format | Default value | Description | Optional? |
|-----|--------|---------------|-------------|-----------|
| `ttl` | string in `HH:mm:ss.fff` format | `"00:10:00"` | Default time-to-live value for cache entries. | Yes |
| `timer` | string in `HH:mm:ss.fff` format | `"00:01:00"` | How often cache checks for expired entries | Yes |


### dns

JSON object.

Configures DNS resolving options.

| Key | Format | Default value | Description | Optional? |
|-----|--------|---------------|-------------|-----------|
| `timeout` | string in `HH:mm:ss.fff` format | `"00:00:05"` | DNS resolve timeout | Yes |


###  conntrack

JSON object.

Configures connection tracking options.

| Key | Format | Default value | Description | Optional? |
|-----|--------|---------------|-------------|-----------|
| `ttl` | string in `HH:mm:ss.fff` format | `"00:10:00"` | Connection time-to-live time | Yes |


### inputPorts

Array of endpoints.

Configures listening addresses & ports.


### pipeline

Array of JSON objects each configuring particular pipeline. See [Pipeline reference](docs/pipelinesref.md).

Configures pipeline, i.e, what and in what order to do with incoming UDP packets. No need
to configure reverse pipeline.


## Endpoints

Endpoint is either:

- Port number, e.g., 50000, 4949. In this case every port number will be resolved into IPv4 `0.0.0.0:port` and IPv6 `::/1 port`;
- String in `ipv4:port` format, e.g., 10.0.0.2:4949, 129.49.98.45:50000.
- String in `ipv6@port` format, e.g., `2345:0425:2CA1::0567:5673:23b5@4949`
- String in `domain:port` format, e.g., domain.com:4589. All IPs will be used from DNS query result.

module TestUdpProxy.LoggerMock

open Moq
open Serilog


let logger : ILogger =
    let mock = Mock<ILogger> ()
    mock.Object


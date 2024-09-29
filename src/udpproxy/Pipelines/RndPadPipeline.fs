namespace UdpProxy.Pipelines

open Serilog
open System
open UdpProxy
open UdpProxy.Exceptions
open UdpProxy.Services

type RndPadPipeline (minBytes: int, maxBytes: int, logger: ILogger, cryptoRnd: ICryptoRnd) =

    let logger = logger.ForContext<RndPadPipeline> ()

    do
        if minBytes < 0 then
            failwithf "Min bytes is less than 0 (%d)" minBytes
        if maxBytes < 0 then
            failwithf "Max bytes is less than 0 (%d)" maxBytes
        if minBytes > maxBytes then
            failwithf "Min bytes (%d) is greater than max bytes (%d)" minBytes maxBytes

        if maxBytes - minBytes = 0 then
            logger.Warning ("RndPadPipeline: Will not pad packets with random bytes because maxBytes({MaxBytes}) - minBytes({MinBytes}) is 0.",
                            maxBytes, minBytes)

    let prefixLengthBytes =
        // Calculate byte length of packet prefix
        // which is amount of bytes padded to packet.
        // Don't forget to round up to multiple of 8 bits (1 byte)
        if maxBytes = 0 then
            0
        else
            let prefixBits = int <| Math.Ceiling (Math.Log2(float maxBytes))
            let prefixBits = prefixBits + (8 - (prefixBits % 8))
            prefixBits / 8

    let shouldPad = prefixLengthBytes <> 0 && (maxBytes - minBytes) > 0

    let writePrefix (value: int) (buffer: Span<byte>) =
        for i in 0..(prefixLengthBytes - 1) do
            buffer[i] <- ( byte ( (value >>> (i * 8)) &&& 0xff ) )

    let readPrefix (buffer: Span<byte>) =
        let mutable result = 0
        for i in 0..(prefixLengthBytes - 1) do
            result <- result ||| ( (int buffer[i]) <<< (i * 8) )

        result


    interface IPipeline with

        member this.Name = sprintf "Random padding. Min %d bytes, Max %d bytes." minBytes maxBytes

        member this.Forward udpPacket next =
            async {
                match shouldPad with
                | false -> return! next udpPacket
                | true ->
                    let rndPadLength = cryptoRnd.NextInt minBytes maxBytes
                    let newPayload : byte array = Array.zeroCreate ( prefixLengthBytes + udpPacket.Length + rndPadLength )

                    writePrefix rndPadLength (newPayload.AsSpan(0, prefixLengthBytes))
                    Array.Copy (udpPacket.Payload, 0, newPayload, prefixLengthBytes, udpPacket.Length)
                    cryptoRnd.Fill (newPayload.AsSpan(prefixLengthBytes + udpPacket.Length))

                    return! next { udpPacket with Payload = newPayload }
            }

        member this.Reverse udpPacket next =
            async {
                match shouldPad with
                | false -> return! next udpPacket
                | true ->
                    let packetLength = udpPacket.Length
                    let packetLengthNoPrefix = packetLength - prefixLengthBytes

                    if packetLength < prefixLengthBytes then
                        raiseMsg "Packet size is too small to be unpadded."

                    let paddedBytesLength = readPrefix (udpPacket.Payload.AsSpan(0, prefixLengthBytes))
                    if paddedBytesLength < 0 || paddedBytesLength > packetLengthNoPrefix then
                        raiseMsg "Padded packet prefix has an invalid value (%d)" paddedBytesLength

                    let unpaddedPayload : byte array = Array.zeroCreate (packetLengthNoPrefix - paddedBytesLength)
                    Array.Copy(udpPacket.Payload, prefixLengthBytes, unpaddedPayload, 0, unpaddedPayload.Length)

                    return! next { udpPacket with Payload = unpaddedPayload }
            }

namespace UdpProxy.Pipelines

open Serilog
open Serilog.Events
open System
open UdpProxy
open UdpProxy.Exceptions
open UdpProxy.Services


type AlignerPipeline (alignBy: int, cryptoRnd: ICryptoRnd, logger: ILogger) =

    let headerLength = 4

    do
        if alignBy <= 0 then
            failwithf "Align value (%d) is less than zero." alignBy

    interface IPipeline with

        member this.Name = sprintf "Aligner (%d bytes)" alignBy

        member this.Forward udpPacket next =
            async {
                let lengthWithHeader = udpPacket.Length + headerLength
                let paddingLength = alignBy - (lengthWithHeader % alignBy)
                let newLength = lengthWithHeader + paddingLength

                let newPayload : byte array = Array.zeroCreate newLength

                Bits.write<int> paddingLength (Span<byte> (newPayload, 0, headerLength))
                Array.Copy (udpPacket.Payload, 0, newPayload, headerLength, udpPacket.Length)
                cryptoRnd.Fill (Span<byte> (newPayload, lengthWithHeader, paddingLength))

                if logger.IsEnabled LogEventLevel.Debug then
                    logger.Debug ("Aligner: Aligned {Size} bytes to {NewSize} bytes.", udpPacket.Length, newLength)

                do! next { udpPacket with Payload = newPayload }
            }

        member this.Reverse udpPacket next =
            async {
                if udpPacket.Length < headerLength then
                    raiseMsg "Packet size is too small to be unaligned."

                let paddingLength = Bits.read<int> (Span<byte> (udpPacket.Payload, 0, headerLength))
                if paddingLength < 0 || paddingLength > (udpPacket.Length - headerLength) then
                    raiseMsg "Padding length has invalid value (%d)" paddingLength

                let originalLength = udpPacket.Length - headerLength - paddingLength
                let newPayload : byte array = Array.zeroCreate originalLength
                Array.Copy (udpPacket.Payload, headerLength, newPayload, 0, originalLength)

                if logger.IsEnabled LogEventLevel.Debug then
                    logger.Debug ("Aligner: Unaligned {Size} bytes to {NewSize} bytes.", udpPacket.Length, originalLength)

                do! next { udpPacket with Payload = newPayload }
            }


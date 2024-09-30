namespace UdpProxy.Pipelines

open K4os.Compression.LZ4
open Serilog
open Serilog.Events
open System
open System.IO.Hashing
open UdpProxy
open UdpProxy.Exceptions


type LZ4Pipeline (level: int, logger: ILogger) =

    let lz4Level =
        match level with
        | value when value < 3 -> LZ4Level.L00_FAST
        | 3 -> LZ4Level.L03_HC
        | 4 -> LZ4Level.L04_HC
        | 5 -> LZ4Level.L05_HC
        | 6 -> LZ4Level.L06_HC
        | 7 -> LZ4Level.L07_HC
        | 8 -> LZ4Level.L08_HC
        | 9 -> LZ4Level.L09_HC
        | 10 -> LZ4Level.L10_OPT
        | 11 -> LZ4Level.L11_OPT
        | 12 -> LZ4Level.L12_MAX
        | _ -> LZ4Level.L12_MAX

    let headerSize = 8
    let maximumUdpSize = 1024 * 1024 * 10 // 10 MB. Upper limit. Just in case.

    interface IPipeline with

        member this.Name = sprintf "LZ4 (level: %d, %O)" level lz4Level

        member this.Forward udpPacket next =
            async {
                let crc32 = Crc32.HashToUInt32 udpPacket.Payload
                let originalLength = udpPacket.Length

                let compressedPayload : byte array = Array.zeroCreate ((originalLength * 2) + headerSize)
                Bits.write<uint32> crc32 (Span<byte> (compressedPayload, 0, 4))
                Bits.write<int> originalLength (Span<byte> (compressedPayload, 4, 4))
                let compressedSize =
                    LZ4Codec.Encode (ReadOnlySpan<byte> udpPacket.Payload,
                                     Span<byte> (compressedPayload, headerSize, (Array.length compressedPayload) - headerSize),
                                     lz4Level)

                if compressedSize <= 0 then
                    failwithf "LZ4Codec.Encode(..) returned bad compressed size (%d)" compressedSize

                if logger.IsEnabled LogEventLevel.Debug then
                    logger.Debug ("LZ4: uncompressed {Uncompressed} bytes, compressed {Compressed} bytes, ratio {Ratio}",
                                  originalLength, compressedSize, Math.Round((float compressedSize) / (float originalLength), 2))

                do! next { udpPacket with Payload = compressedPayload[0 .. headerSize + compressedSize - 1] }
            }

        member this.Reverse udpPacket next =
            async {
                if udpPacket.Length < headerSize then
                    raiseMsg "Packet size is too small to be uncompressed."

                let expectedCrc32 = Bits.read<uint32> (Span<byte> (udpPacket.Payload, 0, 4))
                let originalLength = Bits.read<int> (Span<byte> (udpPacket.Payload, 4, 4))
                if originalLength < 0 || originalLength > maximumUdpSize then
                    raiseMsg "Uncompressed packet size has an invalid value (%d)" originalLength

                let uncompressedPayload : byte array = Array.zeroCreate originalLength
                let uncompressedSize =
                    LZ4Codec.Decode (ReadOnlySpan<byte> (udpPacket.Payload, headerSize, udpPacket.Length - headerSize),
                                     Span<byte> uncompressedPayload)

                if uncompressedSize <= 0 then
                    failwithf "LZ4Codec.Decode(..) returned bad uncompressed size (%d)" uncompressedSize

                let actualCrc32 = Crc32.HashToUInt32 uncompressedPayload
                if actualCrc32 <> expectedCrc32 then
                    failwithf "CRC32 mismatch (0x%x not equal 0x%x)" actualCrc32 expectedCrc32

                do! next { udpPacket with Payload = uncompressedPayload }
            }

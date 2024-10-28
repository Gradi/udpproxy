namespace UdpProxy.Pipelines

open System
open System.Security.Cryptography
open UdpProxy
open UdpProxy.Exceptions


type AesPipeline (aesKey: byte array, hmacKey: byte array) =

    do
        match Array.length aesKey with
        | 16 -> ()
        | 32 -> ()
        | size -> failwithf "AES key must have size 16 or 32 bytes (current %d bytes)" size

        if Array.length hmacKey = 0 then
            failwithf "HMAC key size must be greater than 0."
#if EventSourceProviders
        PipelineEventSource.Instance.AesCreated ()
#endif

    let ivLength = 16
    let headerSize = HMACSHA256.HashSizeInBytes + ivLength
    let padMode = PaddingMode.PKCS7

    interface IPipeline with

        member _.Name = sprintf "AES (%d bytes key, %d bytes HMAC key)" (Array.length aesKey) (Array.length hmacKey)

        member _.Forward udpPacket next =
            async {
#if EventSourceProviders
                PipelineEventSource.Instance.ForwardAes ()
#endif
                use aes = Aes.Create ()
                aes.Key <- aesKey
#if EventSourceProviders
                PipelineEventSource.Instance.AesAesCreated ()
#endif

                let encLen = headerSize + aes.GetCiphertextLengthCbc (udpPacket.Length, padMode)
                let encryptedPayload : byte array = Array.zeroCreate encLen
                let mutable bytesWritten = 0

#if EventSourceProviders
                PipelineEventSource.Instance.AesEncrypt ()
#endif
                match aes.TryEncryptCbc (ReadOnlySpan<byte> udpPacket.Payload,
                                         ReadOnlySpan<byte> aes.IV,
                                         Span<byte> (encryptedPayload, headerSize, encLen - headerSize),
                                         &bytesWritten,
                                         padMode) with
                | false -> failwithf "Could not encrypt packet. Should not happen."
                | true -> ()

                Array.Copy(aes.IV, 0, encryptedPayload, HMACSHA256.HashSizeInBytes, ivLength)
#if EventSourceProviders
                PipelineEventSource.Instance.AesHmacHash ()
#endif
                let written = HMACSHA256.HashData (ReadOnlySpan<byte> hmacKey,
                                                   ReadOnlySpan<byte>(encryptedPayload, HMACSHA256.HashSizeInBytes, encLen - HMACSHA256.HashSizeInBytes),
                                                   Span<byte> (encryptedPayload, 0, HMACSHA256.HashSizeInBytes))

                if written <> HMACSHA256.HashSizeInBytes then
                    failwithf "Written %d bytes of HMACSHA256. Expected %d bytes" written HMACSHA256.HashSizeInBytes

                do! next { udpPacket with Payload = encryptedPayload }
            }

        member this.Reverse udpPacket next =
            async {
#if EventSourceProviders
                PipelineEventSource.Instance.ReverseAes ()
#endif
                if udpPacket.Length < headerSize then
                    raiseMsg "Packet length is too small."

                let expectedMac = ReadOnlyMemory<byte> (udpPacket.Payload, 0, HMACSHA256.HashSizeInBytes)
                let iv = ReadOnlyMemory<byte> (udpPacket.Payload, HMACSHA256.HashSizeInBytes, ivLength)
                let encryptedPayload = ReadOnlyMemory<byte> (udpPacket.Payload, headerSize, udpPacket.Length - headerSize)

#if EventSourceProviders
                PipelineEventSource.Instance.AesHmacHash ()
#endif
                let actualMac =
                    ReadOnlyMemory<byte> (HMACSHA256.HashData (ReadOnlySpan<byte> hmacKey,
                                                               ReadOnlySpan<byte> (udpPacket.Payload, HMACSHA256.HashSizeInBytes, udpPacket.Length - HMACSHA256.HashSizeInBytes)))

                if not (Span.equalsMemRo actualMac expectedMac) then
                    raiseMsg "MAC doesn't match."

                use aes = Aes.Create ()
                aes.Key <- aesKey
                aes.IV <- iv.ToArray ()
#if EventSourceProviders
                PipelineEventSource.Instance.AesAesCreated ()
#endif

#if EventSourceProviders
                PipelineEventSource.Instance.AesDecrypt ()
#endif
                let decryptedPayload =
                    aes.DecryptCbc (encryptedPayload.Span, ReadOnlySpan<byte> aes.IV, padMode)

                do! next { udpPacket with Payload = decryptedPayload }
            }

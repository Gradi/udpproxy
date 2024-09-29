module UEcho.EchoRequest

open System
open System.IO.Hashing


type EchoRequest =
    { Checksum: uint32
      Length: uint32
      Payload: byte array }


let make (payload: byte array) =
    let length : uint32 = 4u + 4u + (uint32 (Array.length payload))

    let xxhash32 = XxHash32 ()
    xxhash32.Append (BitConverter.GetBytes length)
    xxhash32.Append payload

    let checksum = xxhash32.GetCurrentHash ()
    assert ((Array.length checksum) = sizeof<uint32>)
    let checksum : uint32 = BitConverter.ToUInt32 checksum

    { Checksum = checksum; Length = length; Payload = payload }


let serialize (request: EchoRequest) =
    let result : byte array = Array.zeroCreate (int request.Length)

    Array.Copy(BitConverter.GetBytes request.Checksum, 0, result, 0, 4)
    Array.Copy(BitConverter.GetBytes request.Length, 0, result, 4, 4)
    Array.Copy(request.Payload, 0, result, 8, Array.length request.Payload)

    result


let tryParse (bytes: byte array) =
    if isNull bytes then
        Error "Array is null."
    else if Array.length bytes < 8 then
        Error "Payload is too small."
    else

        let checksum: uint32 = BitConverter.ToUInt32 (ReadOnlySpan<byte> (bytes, 0, 4))
        let length: uint32 = BitConverter.ToUInt32 (ReadOnlySpan<byte> (bytes, 4, 4))
        let payload = Span<byte>(bytes, 8, Array.length bytes - 8).ToArray()
        Ok { Checksum = checksum; Length = length; Payload = payload }


let verify (request: EchoRequest) =
    let errors =
        seq {
            let xxhash32 = XxHash32 ()
            xxhash32.Append (BitConverter.GetBytes request.Length)
            xxhash32.Append request.Payload

            let checksum = xxhash32.GetCurrentHash ()
            assert (Array.length checksum = sizeof<uint32>)
            let checksum : uint32 = BitConverter.ToUInt32 checksum

            if checksum <> request.Checksum then
                yield sprintf "Request checksum (%x) not equal to actual checksum (%x)" request.Checksum checksum

            let expectedLength = 4u + 4u + (uint32 (Array.length request.Payload))
            if expectedLength <> request.Length then
                yield sprintf "Request length (%d) not equal to actual expected length (%d)" request.Length expectedLength
        }
        |> List.ofSeq

    if List.isEmpty errors then
        Ok ()
    else
        Error errors

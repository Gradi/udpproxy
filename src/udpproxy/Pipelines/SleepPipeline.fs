namespace UdpProxy.Pipelines

open System


type SleepPipeline (forwardMin: int, forwardMax: int, reverseMin: int, reverseMax: int) =

    let sleep ms =
        if ms = 0 then
            async { return () }
        else
            Async.Sleep ms

    interface IPipeline with

        member this.Name = sprintf "Sleep (forward min %d, forward max %d, reverse min %d, reverse max %d)"
                                   forwardMin forwardMax reverseMin reverseMax

        member this.Forward udpPacket next =
            async {
                if forwardMin = forwardMax then
                    do! sleep forwardMin
                else
                    do! sleep (Random.Shared.Next (forwardMin, forwardMax))

                do! next udpPacket
            }

        member this.Reverse udpPacket next =
            async {
                if reverseMin = reverseMax then
                    do! sleep reverseMin
                else
                    do! sleep (Random.Shared.Next (reverseMin,  reverseMax))

                do! next udpPacket
            }

module UdpProxy.Span

open System


let equals<'a when 'a : equality> (left: Span<'a>) (right: Span<'a>) : bool =
    if left.Length <> right.Length then
        false
    else
        let mutable equal = true
        for i in 0 .. (left.Length - 1) do
            equal <- equal && (left[i] = right[i])

        equal


let equalsRo<'a when 'a : equality> (left: ReadOnlySpan<'a>) (right: ReadOnlySpan<'a>) : bool =
    if left.Length <> right.Length then
        false
    else
        let mutable equal = true
        for i in 0 .. (left.Length - 1) do
            equal <- equal && (left[i] = right[i])

        equal


let equalsMemRo<'a when 'a : equality> (left: ReadOnlyMemory<'a>) (right: ReadOnlyMemory<'a>) : bool =
    equalsRo left.Span right.Span

module CsvTests

open Djehuti.Core
open Xunit

[<Fact>]
let ``csv parser preserves quoted commas and multiline fields`` () =
    let csv =
        "name,notes,score\r\n" +
        "Alice,\"hello, world\",3\r\n" +
        "Bob,\"line one\nline two\",4\r\n"

    let parsed = CsvText.parse csv

    Assert.Equal<string[]>([| "name"; "notes"; "score" |], parsed.Headers |> List.toArray)
    Assert.Equal(2, parsed.Rows.Length)
    Assert.Equal<string[]>([| "Alice"; "hello, world"; "3" |], parsed.Rows[0] |> List.toArray)
    Assert.Equal<string[]>([| "Bob"; "line one\nline two"; "4" |], parsed.Rows[1] |> List.toArray)

[<Fact>]
let ``csv parser does not invent a trailing blank row`` () =
    let parsed = CsvText.parse "a,b,c\r\n1,2,3\r\n"

    Assert.Equal<string[]>([| "a"; "b"; "c" |], parsed.Headers |> List.toArray)
    Assert.Equal(1, parsed.Rows.Length)
    Assert.Equal<string[]>([| "1"; "2"; "3" |], parsed.Rows[0] |> List.toArray)

[<Fact>]
let ``csv parser keeps a partial trailing row for preview slices`` () =
    let parsed = CsvText.parse "a,b,c\r\n1,2,3\r\n4,5"

    Assert.Equal<string[]>([| "a"; "b"; "c" |], parsed.Headers |> List.toArray)
    Assert.Equal(2, parsed.Rows.Length)
    Assert.Equal<string[]>([| "4"; "5" |], parsed.Rows[1] |> List.toArray)

module HierarchicalDataTests

open Djehuti.Core
open Xunit

[<Fact>]
let ``csv hierarchy summary counts table nodes and leaves`` () =
    let document =
        HierarchicalData.fromCsv
            "sample.csv"
            [ "name"; "value" ]
            [ [ "alpha"; "1" ]
              [ "beta"; "2" ] ]

    let summary = HierarchicalData.summarize document.Root

    Assert.Equal(9, summary.NodeCount)
    Assert.Equal(6, summary.LeafCount)
    Assert.Equal(2, summary.MaxDepth)

[<Fact>]
let ``json hierarchy summary tracks nested objects and arrays`` () =
    let document =
        HierarchicalData.fromJsonText
            "sample.json"
            """{"outer":{"inner":[1,2],"flag":true}}"""

    let summary = HierarchicalData.summarize document.Root

    Assert.Equal(6, summary.NodeCount)
    Assert.Equal(3, summary.LeafCount)
    Assert.Equal(3, summary.MaxDepth)

[<Fact>]
let ``root manifest hierarchy uses the manifest json shape`` () =
    let document =
        use parsed = System.Text.Json.JsonDocument.Parse("""{"tree":{"branches":[{"name":"run-1"},{"name":"run-2"}]}}""")
        HierarchicalData.fromRootManifest "sample.root" parsed.RootElement

    let summary = HierarchicalData.summarize document.Root

    Assert.Equal(7, summary.NodeCount)
    Assert.Equal(2, summary.LeafCount)
    Assert.Equal(4, summary.MaxDepth)

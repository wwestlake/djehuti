namespace Djehuti.Core

open System
open System.Text.Json

type HierarchicalNodeKind =
    | Root
    | Folder
    | Object
    | Array
    | Table
    | Row
    | Scalar
    | Branch
    | Leaf
    | Unknown of string

type HierarchicalNode =
    { Name: string
      Kind: HierarchicalNodeKind
      Value: string option
      Children: HierarchicalNode list
      Metadata: Map<string, string> }

type HierarchicalDocument =
    { Name: string
      SourceKind: string
      Root: HierarchicalNode }

type HierarchicalSummary =
    { NodeCount: int
      LeafCount: int
      MaxDepth: int }

module HierarchicalData =
    let private kindText kind =
        match kind with
        | Root -> "root"
        | Folder -> "folder"
        | Object -> "object"
        | Array -> "array"
        | Table -> "table"
        | Row -> "row"
        | Scalar -> "scalar"
        | Branch -> "branch"
        | Leaf -> "leaf"
        | Unknown value -> value

    let private kindFromText (value: string) =
        match value.ToLowerInvariant() with
        | "root" -> Root
        | "folder" -> Folder
        | "object" -> Object
        | "array" -> Array
        | "table" -> Table
        | "row" -> Row
        | "scalar" -> Scalar
        | "branch" -> Branch
        | "leaf" -> Leaf
        | other -> Unknown other

    let leaf (name: string) (value: string) (metadata: Map<string, string>) : HierarchicalNode =
        { Name = name
          Kind = Leaf
          Value = Some value
          Children = []
          Metadata = metadata }

    let folder (name: string) (children: HierarchicalNode list) (metadata: Map<string, string>) : HierarchicalNode =
        { Name = name
          Kind = Folder
          Value = None
          Children = children
          Metadata = metadata }

    let table (name: string) (headers: string list) (rows: string list list) (metadata: Map<string, string>) : HierarchicalNode =
        let headerNodes =
            headers
            |> List.map (fun header -> leaf header header Map.empty)

        let rowNodes =
            rows
            |> List.mapi (fun index row ->
                let cells =
                    row
                    |> List.mapi (fun cellIndex cell ->
                        let headerName = headers |> List.tryItem cellIndex |> Option.defaultValue $"col{cellIndex + 1}"
                        leaf headerName cell Map.empty)

                { Name = $"row-{index}"
                  Kind = Row
                  Value = None
                  Children = cells
                  Metadata = Map.ofList [ "index", string index ] })

        { Name = name
          Kind = Table
          Value = None
          Children = headerNodes @ rowNodes
          Metadata = metadata }

    let rec fromJsonElement (name: string) (element: JsonElement) : HierarchicalNode =
        let metadata = Map.empty
        match element.ValueKind with
        | JsonValueKind.Object ->
            let children =
                element.EnumerateObject()
                |> Seq.map (fun prop -> fromJsonElement prop.Name prop.Value)
                |> Seq.toList
            { Name = name
              Kind = Object
              Value = None
              Children = children
              Metadata = metadata }
        | JsonValueKind.Array ->
            let children =
                element.EnumerateArray()
                |> Seq.mapi (fun index item -> fromJsonElement $"item-{index}" item)
                |> Seq.toList
            { Name = name
              Kind = Array
              Value = None
              Children = children
              Metadata = metadata }
        | JsonValueKind.String ->
            let text = element.GetString()
            leaf name (if isNull text then "" else text) metadata
        | JsonValueKind.Number ->
            leaf name (element.GetRawText()) metadata
        | JsonValueKind.True ->
            leaf name "true" metadata
        | JsonValueKind.False ->
            leaf name "false" metadata
        | JsonValueKind.Null
        | JsonValueKind.Undefined ->
            { Name = name
              Kind = Scalar
              Value = None
              Children = []
              Metadata = Map.ofList [ "jsonKind", "null" ] }
        | _ ->
            { Name = name
              Kind = Unknown (element.ValueKind.ToString().ToLowerInvariant())
              Value = Some (element.GetRawText())
              Children = []
              Metadata = metadata }

    let fromJsonText (name: string) (json: string) : HierarchicalDocument =
        use document = JsonDocument.Parse(json)
        let root = fromJsonElement name document.RootElement
        { Name = name; SourceKind = "json"; Root = root }

    let fromCsv (name: string) (headers: seq<string>) (rows: seq<seq<string>>) : HierarchicalDocument =
        let headerList = headers |> Seq.toList
        let rowList = rows |> Seq.map Seq.toList |> Seq.toList
        { Name = name
          SourceKind = "csv"
          Root = table name headerList rowList Map.empty }

    let fromRootManifest (name: string) (manifest: JsonElement) : HierarchicalDocument =
        let root = fromJsonElement name manifest
        { Name = name; SourceKind = "root-manifest"; Root = root }

    let rec tryFindPath (segments: string list) (node: HierarchicalNode) : HierarchicalNode option =
        match segments with
        | [] -> Some node
        | head :: tail ->
            node.Children
            |> List.tryFind (fun child -> String.Equals(child.Name, head, StringComparison.OrdinalIgnoreCase))
            |> Option.bind (tryFindPath tail)

    let rec flattenPaths (prefix: string) (node: HierarchicalNode) : (string * HierarchicalNode) list =
        let currentPath =
            if String.IsNullOrWhiteSpace prefix then node.Name
            else $"{prefix}/{node.Name}"

        let here = [ currentPath, node ]
        let children =
            node.Children
            |> List.collect (flattenPaths currentPath)
        here @ children

    let toSerializable (node: HierarchicalNode) : obj =
        let rec convert (node: HierarchicalNode) : obj =
            dict
                [ "name", box node.Name
                  "kind", box (kindText node.Kind)
                  "value", box node.Value
                  "metadata", box node.Metadata
                  "children", box (node.Children |> List.map convert) ]
            :> obj

        convert node

    let summarize (node: HierarchicalNode) : HierarchicalSummary =
        let rec loop depth (current: HierarchicalNode) =
            let mutable nodeCount = 1
            let mutable leafCount = if current.Children.IsEmpty then 1 else 0
            let mutable maxDepth = depth

            for child in current.Children do
                let childSummary = loop (depth + 1) child
                nodeCount <- nodeCount + childSummary.NodeCount
                leafCount <- leafCount + childSummary.LeafCount
                maxDepth <- max maxDepth childSummary.MaxDepth

            { NodeCount = nodeCount
              LeafCount = leafCount
              MaxDepth = maxDepth }

        loop 0 node

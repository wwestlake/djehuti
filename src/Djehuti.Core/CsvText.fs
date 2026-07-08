module CsvText

open System
open System.Collections.Generic
open System.Text

type ParsedCsv =
    { Headers: string list
      Rows: string list list }

let private finalizeField (field: StringBuilder) =
    let value = field.ToString()
    field.Clear() |> ignore
    value

let parse (csv: string) : ParsedCsv =
    if String.IsNullOrWhiteSpace csv then
        { Headers = []; Rows = [] }
    else
        let headers = ResizeArray<string>()
        let rows = ResizeArray<string list>()
        let currentRow = ResizeArray<string>()
        let currentField = StringBuilder()
        let mutable inQuotes = false
        let mutable endedWithRowSeparator = false
        let mutable index = 0

        let addField () =
            currentRow.Add(finalizeField currentField)

        let addRow () =
            rows.Add(List.ofSeq currentRow)
            currentRow.Clear()

        while index < csv.Length do
            let character = csv[index]
            match character with
            | '"' ->
                endedWithRowSeparator <- false
                if inQuotes && index + 1 < csv.Length && csv[index + 1] = '"' then
                    currentField.Append('"') |> ignore
                    index <- index + 1
                else
                    inQuotes <- not inQuotes
            | ',' when not inQuotes ->
                endedWithRowSeparator <- false
                addField ()
            | '\r' when not inQuotes ->
                addField ()
                if currentRow.Count > 0 || currentField.Length > 0 then
                    addRow ()
                endedWithRowSeparator <- true
                if index + 1 < csv.Length && csv[index + 1] = '\n' then
                    index <- index + 1
            | '\n' when not inQuotes ->
                addField ()
                if currentRow.Count > 0 || currentField.Length > 0 then
                    addRow ()
                endedWithRowSeparator <- true
            | _ ->
                endedWithRowSeparator <- false
                currentField.Append(character) |> ignore
            index <- index + 1

        if not endedWithRowSeparator then
            addField ()
        if (currentRow.Count > 0 || currentField.Length > 0 || csv.EndsWith(",")) && not endedWithRowSeparator then
            addRow ()

        let rowList = rows |> Seq.toList
        let headerList, dataRows =
            match rowList with
            | [] -> [], []
            | head :: tail -> head, tail

        { Headers = headerList
          Rows = dataRows }

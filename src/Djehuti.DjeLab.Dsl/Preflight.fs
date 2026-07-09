/// Lightweight validation and safety hints for generated Spinoza source.
module Djehuti.DjeLab.Dsl.Preflight

open System

type PreflightSeverity =
    | SeverityError
    | SeverityWarning

type PreflightIssue =
    { Severity: PreflightSeverity
      Message: string }

type PreflightReport =
    { CanRun: bool
      Parsed: bool
      Issues: PreflightIssue list
      Summary: string }

let private issue severity message =
    { Severity = severity
      Message = message }

let private containsIgnoreCase (needle: string) (haystack: string) =
    haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0

let validate (source: string) : PreflightReport =
    let mutable issues = ResizeArray<PreflightIssue>()

    if String.IsNullOrWhiteSpace source then
        issues.Add(issue SeverityError "No Spinoza source was provided.")

    if containsIgnoreCase "readcsv" source || containsIgnoreCase "readCsv" source || containsIgnoreCase "readCSV" source then
        issues.Add(issue SeverityError "Spinoza cannot read files. Use manage_file_data first, then feed the previewed data into Spinoza.")

    if source.Contains(";") then
        issues.Add(issue SeverityWarning "Spinoza is expression-based; semicolons usually mean the program was written like statement code.")

    if source.Contains("//") then
        issues.Add(issue SeverityWarning "Inline comments are not part of the Spinoza grammar and often cause parse failures.")

    match Parser.parse source with
    | Result.Error message ->
        issues.Add(issue SeverityError message)
        { CanRun = false
          Parsed = false
          Issues = List.ofSeq issues
          Summary = "Spinoza source failed preflight validation." }
    | Result.Ok _ ->
        let hasErrors = issues |> Seq.exists (fun item -> item.Severity = SeverityError)
        { CanRun = not hasErrors
          Parsed = true
          Issues = List.ofSeq issues
          Summary =
              if hasErrors then "Spinoza source has blocking preflight errors."
              elif issues.Count > 0 then "Spinoza source parsed, with warnings."
              else "Spinoza source parsed cleanly." }

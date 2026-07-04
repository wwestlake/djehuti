module Djehuti.Api.WorkerResilience

open System
open Npgsql

let rec private hasDbException (ex: exn) =
    match ex with
    | null -> false
    | :? NpgsqlException -> true
    | :? PostgresException -> true
    | _ -> hasDbException ex.InnerException

let isDatabaseException (ex: exn) =
    hasDbException ex

let backoffDelay (attempt: int) =
    let normalizedAttempt = Math.Max(attempt, 1)
    let seconds =
        Math.Min(15.0 * Math.Pow(2.0, float (normalizedAttempt - 1)), 300.0)
    TimeSpan.FromSeconds(seconds)

let shortRecoveryDelay =
    TimeSpan.FromSeconds(30.0)

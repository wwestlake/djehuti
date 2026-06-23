module Djehuti.Api.RateLimiter

open System
open System.Collections.Concurrent

type RateLimitConfig = {
  MaxAttempts: int
  WindowSeconds: int
}

type private AttemptRecord = {
  Count: int
  ResetTime: DateTime
}

let private attempts = ConcurrentDictionary<string, AttemptRecord>()

let private cleanup () =
  let now = DateTime.UtcNow
  let staleKeys =
    attempts
    |> Seq.filter (fun kvp -> kvp.Value.ResetTime < now)
    |> Seq.map (fun kvp -> kvp.Key)
    |> List.ofSeq

  staleKeys |> List.iter (fun key -> attempts.TryRemove(key) |> ignore)

let checkRateLimit (key: string) (config: RateLimitConfig) : bool =
  cleanup ()
  let now = DateTime.UtcNow

  match attempts.TryGetValue(key) with
  | true, record when record.ResetTime > now ->
      if record.Count >= config.MaxAttempts then
        false
      else
        let updated = { record with Count = record.Count + 1 }
        attempts.[key] <- updated
        true
  | _ ->
      let newRecord = { Count = 1; ResetTime = now.AddSeconds(float config.WindowSeconds) }
      attempts.[key] <- newRecord
      true

let getRemainingAttempts (key: string) (config: RateLimitConfig) : int =
  cleanup ()
  let now = DateTime.UtcNow

  match attempts.TryGetValue(key) with
  | true, record when record.ResetTime > now ->
      max 0 (config.MaxAttempts - record.Count)
  | _ ->
      config.MaxAttempts

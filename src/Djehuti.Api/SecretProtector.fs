module Djehuti.Api.SecretProtector

open System
open Microsoft.AspNetCore.DataProtection

let mutable private protector : IDataProtector option = None

let initialize (provider: IDataProtectionProvider) =
    protector <- Some (provider.CreateProtector("Djehuti.Api.MudCompanionKeys.v1"))

let protect (plaintext: string) =
    match protector with
    | Some value -> value.Protect(plaintext)
    | None -> failwith "Secret protector is not initialized"

let unprotect (ciphertext: string) =
    match protector with
    | Some value -> value.Unprotect(ciphertext)
    | None -> failwith "Secret protector is not initialized"

let isConfigured () = protector.IsSome

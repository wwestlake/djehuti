/// Tree-walking evaluator for the DjeLab DSL AST.
///
/// This is a plain, pure function over the AST -- no file/network access is
/// possible because nothing in `Value` or `eval` ever touches them. The one
/// safety guarantee the host still needs is process-level sandboxing
/// (isolated process, wall-clock/CPU/memory ceiling) for genuinely
/// non-terminating programs. The evaluator itself does not impose an
/// artificial reduction cap here; long tail-recursive programs are allowed
/// to run until they finish or the host stops them.
module Djehuti.DjeLab.Dsl.Evaluator

open System
open System.Text.Json
open Djehuti.DjeLab.Dsl.Ast

type Value =
    | VNumber of float
    | VBool of bool
    | VVector of Value[]
    /// The environment is a `ref` (not a plain `Env`) so `LetRec` can tie the
    /// recursive knot: it creates the closure, then mutates this same cell
    /// to include the closure's own name. `Map` itself is immutable --
    /// reassigning a `Map`-typed field after construction would not be
    /// visible to a closure holding the old value, hence the indirection.
    /// This is the only mutation in the whole module, and it is invisible
    /// to and unreachable from DSL code -- it only wires recursion.
    | VClosure of parameters: string list * body: Expr * env: Env ref
    | VBuiltin of name: string * arity: int * fn: (Value list -> Result<Value, string>)

and Env = Map<string, Value>

let rec private describe (value: Value) : string =
    match value with
    | VNumber n -> string n
    | VBool b -> string b
    | VVector items -> "[" + String.Join(", ", items |> Array.map describe) + "]"
    | VClosure(parameters, _, _) -> $"""<function/{parameters.Length}>"""
    | VBuiltin(name, _, _) -> $"<builtin:{name}>"

let private asNumber (context: string) (value: Value) : Result<float, string> =
    match value with
    | VNumber n -> Ok n
    | other -> Error $"{context}: expected a number, got {describe other}"

let private asBool (context: string) (value: Value) : Result<bool, string> =
    match value with
    | VBool b -> Ok b
    | other -> Error $"{context}: expected a bool, got {describe other}"

let private asVector (context: string) (value: Value) : Result<Value[], string> =
    match value with
    | VVector items -> Ok items
    | other -> Error $"{context}: expected a vector, got {describe other}"

/// Built-in math functions available to every DjeLab program. This is the
/// entire surface of "library calls" a program can make -- adding a new
/// capability means adding an entry here, not opening up the language.
///
/// `onEmit` wires Spinoza's one deliberate side channel: calling
/// `emit(point)` reports `point` to the host (used to stream simulation/plot
/// points out to a live chart while a longer-running program is still
/// executing) and returns `point` unchanged, so it composes inline without
/// needing a sequencing operator. It is write-only -- a program can never
/// read anything back through it -- so the language stays otherwise pure;
/// this is the same category of thing as a trace/log effect, not a general
/// I/O escape hatch. When no host is listening (e.g. normal `run`), `emit`
/// is simply a no-op pass-through.
let makeBuiltinEnv (onEmit: (Value -> unit) option) : Env =
    let numeric1 name (f: float -> float) =
        name, VBuiltin(name, 1, function
            | [ v ] -> asNumber name v |> Result.map (f >> VNumber)
            | args -> Error $"{name}: expected 1 argument, got {args.Length}")

    let numeric2 name (f: float -> float -> float) =
        name, VBuiltin(name, 2, function
            | [ a; b ] ->
                match asNumber name a, asNumber name b with
                | Ok x, Ok y -> Ok(VNumber(f x y))
                | Error e, _ | _, Error e -> Error e
            | args -> Error $"{name}: expected 2 arguments, got {args.Length}")

    [ numeric1 "sin" sin
      numeric1 "cos" cos
      numeric1 "tan" tan
      numeric1 "sqrt" sqrt
      numeric1 "abs" abs
      numeric1 "exp" exp
      numeric1 "ln" log
      numeric1 "floor" floor
      numeric1 "ceil" ceil
      numeric2 "min" min
      numeric2 "max" max
      numeric2 "atan2" atan2
      "len", VBuiltin("len", 1, function
          | [ v ] -> asVector "len" v |> Result.map (fun items -> VNumber(float items.Length))
          | args -> Error $"len: expected 1 argument, got {args.Length}")
      "emit", VBuiltin("emit", 1, function
          | [ v ] ->
              onEmit |> Option.iter (fun f -> f v)
              Ok v
          | args -> Error $"emit: expected 1 argument, got {args.Length}")
      "pi", VNumber Math.PI
      "e", VNumber Math.E ]
    |> Map.ofList

let builtinEnv : Env = makeBuiltinEnv None

type private Step =
    | Done of Result<Value, string>
    | More of (unit -> Step)

let rec private bindStep (step: Step) (next: Value -> Step) : Step =
    match step with
    | Done (Ok value) -> More(fun () -> next value)
    | Done (Error error) -> Done(Error error)
    | More thunk -> More(fun () -> bindStep (thunk()) next)

let rec private evalList (env: Env) (items: Expr list) (acc: Value list) (next: Value list -> Step) : Step =
    match items with
    | [] -> next (List.rev acc)
    | head :: tail ->
        bindStep (evalWith env head) (fun value -> evalList env tail (value :: acc) next)

and private evalWith (env: Env) (expr: Expr) : Step =
    match expr with
    | Number n -> Done(Ok(VNumber n))
    | Bool b -> Done(Ok(VBool b))
    | Var name ->
        match Map.tryFind name env with
        | Some v -> Done(Ok v)
        | None -> Done(Error $"Unbound variable '{name}'")

    | VectorLit items ->
        evalList env items [] (fun values -> Done(Ok(VVector(List.toArray values))))

    | Index(vecExpr, idxExpr) ->
        bindStep (evalWith env vecExpr) (fun vecVal ->
            bindStep (evalWith env idxExpr) (fun idxVal ->
                match asVector "index" vecVal, asNumber "index" idxVal with
                | Ok items, Ok idx ->
                    let i = int idx
                    if i >= 0 && i < items.Length then Done(Ok items.[i])
                    else Done(Error $"Index {i} out of range (vector has {items.Length} elements)")
                | Error e, _ | _, Error e -> Done(Error e)))

    | UnaryOp(Neg, e) ->
        bindStep (evalWith env e) (fun value ->
            match asNumber "negation" value with
            | Ok n -> Done(Ok(VNumber(-n)))
            | Error error -> Done(Error error))

    | UnaryOp(Not, e) ->
        bindStep (evalWith env e) (fun value ->
            match asBool "not" value with
            | Ok b -> Done(Ok(VBool(not b)))
            | Error error -> Done(Error error))

    | BinaryOp(op, left, right) ->
        match op with
        | And ->
            bindStep (evalWith env left) (fun leftValue ->
                match asBool "&&" leftValue with
                | Error error -> Done(Error error)
                | Ok leftBool ->
                    if not leftBool then Done(Ok(VBool false))
                    else
                        bindStep (evalWith env right) (fun rightValue ->
                            match asBool "&&" rightValue with
                            | Ok rightBool -> Done(Ok(VBool rightBool))
                            | Error error -> Done(Error error)))
        | Or ->
            bindStep (evalWith env left) (fun leftValue ->
                match asBool "||" leftValue with
                | Error error -> Done(Error error)
                | Ok leftBool ->
                    if leftBool then Done(Ok(VBool true))
                    else
                        bindStep (evalWith env right) (fun rightValue ->
                            match asBool "||" rightValue with
                            | Ok rightBool -> Done(Ok(VBool rightBool))
                            | Error error -> Done(Error error)))
        | _ ->
            bindStep (evalWith env left) (fun leftValue ->
                bindStep (evalWith env right) (fun rightValue ->
                    evalBinaryOp op leftValue rightValue |> Done))

    | If(cond, thenB, elseB) ->
        bindStep (evalWith env cond) (fun condValue ->
            match asBool "if condition" condValue with
            | Error error -> Done(Error error)
            | Ok condition -> evalWith env (if condition then thenB else elseB))

    | Let(name, value, body) ->
        bindStep (evalWith env value) (fun boundValue ->
            evalWith (Map.add name boundValue env) body)

    | LetRec(name, parameters, funcBody, body) ->
        // Tie the recursive knot via the ref cell: create the closure first
        // (holding a reference to `envCell`), then mutate that same cell to
        // include the closure's own name. When the closure is later called,
        // `applyCallable` dereferences the cell -- by then it contains the
        // self-binding, so a recursive call inside `funcBody` resolves.
        let envCell = ref env
        let closure = VClosure(parameters, funcBody, envCell)
        envCell.Value <- Map.add name closure env
        evalWith (Map.add name closure env) body

    | Lambda(parameters, body) -> Done(Ok(VClosure(parameters, body, ref env)))

    | Call(calleeExpr, argExprs) ->
        bindStep (evalWith env calleeExpr) (fun callee ->
            evalList env argExprs [] (fun args -> applyCallable callee args))

and private applyCallable (callee: Value) (args: Value list) : Step =
    match callee with
    | VBuiltin(name, arity, fn) ->
        if args.Length <> arity then
            Done(Error $"{name}: expected {arity} argument(s), got {args.Length}")
        else
            Done(fn args)
    | VClosure(parameters, body, closureEnvCell) ->
        if args.Length <> parameters.Length then
            Done(Error $"function expected {parameters.Length} argument(s), got {args.Length}")
        else
            let callEnv = List.zip parameters args |> List.fold (fun e (p, v) -> Map.add p v e) closureEnvCell.Value
            evalWith callEnv body
    | other -> Done(Error $"{describe other} is not callable")

/// `Value` can't derive structural equality (it embeds functions via
/// `VClosure`/`VBuiltin`), so `==`/`!=` get their own recursive comparison:
/// numbers, bools, and vectors compare structurally; comparing a function
/// is a DSL-level error rather than silently `false`.
and private valueEquals (a: Value) (b: Value) : Result<bool, string> =
    match a, b with
    | VNumber x, VNumber y -> Ok(x = y)
    | VBool x, VBool y -> Ok(x = y)
    | VVector xs, VVector ys ->
        if xs.Length <> ys.Length then
            Ok false
        else
            let rec loop i =
                if i >= xs.Length then
                    Ok true
                else
                    match valueEquals xs.[i] ys.[i] with
                    | Ok true -> loop (i + 1)
                    | Ok false -> Ok false
                    | Error e -> Error e
            loop 0
    | (VClosure _ | VBuiltin _), _
    | _, (VClosure _ | VBuiltin _) -> Error "functions cannot be compared for equality"
    | _ -> Ok false

and private evalBinaryOp (op: BinOp) (left: Value) (right: Value) : Result<Value, string> =
    let numOp f =
        match asNumber "arithmetic" left, asNumber "arithmetic" right with
        | Ok l, Ok r -> Ok(f l r)
        | Error e, _ | _, Error e -> Error e
    match op with
    | Add -> numOp (fun l r -> VNumber(l + r))
    | Sub -> numOp (fun l r -> VNumber(l - r))
    | Mul -> numOp (fun l r -> VNumber(l * r))
    | Div -> numOp (fun l r -> VNumber(l / r))
    | Mod -> numOp (fun l r -> VNumber(l % r))
    | Pow -> numOp (fun l r -> VNumber(l ** r))
    | Lt -> numOp (fun l r -> VBool(l < r))
    | Lte -> numOp (fun l r -> VBool(l <= r))
    | Gt -> numOp (fun l r -> VBool(l > r))
    | Gte -> numOp (fun l r -> VBool(l >= r))
    | Eq -> valueEquals left right |> Result.map VBool
    | Neq -> valueEquals left right |> Result.map (fun b -> VBool(not b))
    | And | Or -> Error "unreachable: short-circuit ops handled before evalBinaryOp"

let rec fromJson (element: JsonElement) : Result<Value, string> =
    match element.ValueKind with
    | JsonValueKind.Number -> Ok(VNumber(element.GetDouble()))
    | JsonValueKind.True -> Ok(VBool true)
    | JsonValueKind.False -> Ok(VBool false)
    | JsonValueKind.Array ->
        let rec collect acc (items: JsonElement list) =
            match items with
            | [] -> Ok(List.rev acc |> List.toArray |> VVector)
            | head :: tail ->
                match fromJson head with
                | Ok value -> collect (value :: acc) tail
                | Error e -> Error e

        element.EnumerateArray() |> Seq.toList |> collect []
    | JsonValueKind.Null
    | JsonValueKind.Undefined -> Error "null values cannot be used as Spinoza data bindings"
    | JsonValueKind.String -> Error "string values cannot be used as Spinoza data bindings"
    | JsonValueKind.Object -> Error "object values cannot be used as Spinoza data bindings"
    | _ -> Error $"unsupported JSON value kind: {element.ValueKind}"

/// Evaluates an expression.
let eval (env: Env) (expr: Expr) : Result<Value, string> =
    let rec run current =
        match current with
        | Done result -> result
        | More thunk -> run (thunk())

    run (evalWith env expr)

/// Evaluates against the standard builtin environment.
let run (expr: Expr) : Result<Value, string> =
    eval builtinEnv expr

/// Evaluates with `emit(...)` wired to `onEmit`, called synchronously the
/// instant each `emit` call is reached during the walk -- not batched or
/// deferred -- so a host streaming these out (e.g. over a Web Worker's
/// postMessage) sees them as the program actually computes them.
let runWithEmit (onEmit: Value -> unit) (expr: Expr) : Result<Value, string> =
    eval (makeBuiltinEnv (Some onEmit)) expr

/// Evaluates with `emit(...)` wired to `onEmit` and a host-supplied `data`
/// binding available to the program.
let runWithEmitAndData (onEmit: Value -> unit) (dataValue: Value option) (expr: Expr) : Result<Value, string> =
    let env =
        match dataValue with
        | Some value -> Map.add "data" value (makeBuiltinEnv (Some onEmit))
        | None -> makeBuiltinEnv (Some onEmit)

    eval env expr

/// Renders a Value as JSON, for transport across a worker postMessage
/// boundary or similar host interop. Numbers that aren't finite (NaN,
/// +/-Infinity -- both reachable from ordinary DSL arithmetic like `1/0` or
/// `sqrt(-1)`... no, sqrt(-1) errors, but `1/0` and `0/0` do not) serialize
/// as JSON `null` rather than throwing, since raw NaN/Infinity have no JSON
/// representation; the host is expected to treat a null point as "skip it."
/// A function value (VClosure/VBuiltin) has no meaningful JSON form and is
/// a hard error here, same as any other DSL type mismatch.
let rec toJson (value: Value) : Result<string, string> =
    match value with
    | VNumber n ->
        if Double.IsFinite n then Ok(n.ToString("R", Globalization.CultureInfo.InvariantCulture))
        else Ok "null"
    | VBool b -> Ok(if b then "true" else "false")
    | VVector items ->
        let rec collect acc =
            function
            | [] -> Ok(List.rev acc)
            | x :: rest ->
                match toJson x with
                | Ok j -> collect (j :: acc) rest
                | Error e -> Error e
        items |> Array.toList |> collect [] |> Result.map (fun parts -> "[" + String.Join(",", parts) + "]")
    | VClosure _ | VBuiltin _ -> Error $"cannot serialize {describe value} to JSON"

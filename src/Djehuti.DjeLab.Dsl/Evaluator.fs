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
open System.Security.Cryptography
open Djehuti.DjeLab.Dsl.Ast

type Value =
    | VNumber of float
    | VBool of bool
    | VString of string
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
    /// Render descriptor: type and properties for the rendering engine
    | VRender of renderType: string * props: Map<string, Value>

and Env = Map<string, Value>

let rec private describe (value: Value) : string =
    match value with
    | VNumber n -> string n
    | VBool b -> string b
    | VString s -> $"\"{s}\""
    | VVector items -> "[" + String.Join(", ", items |> Array.map describe) + "]"
    | VClosure(parameters, _, _) -> $"""<function/{parameters.Length}>"""
    | VBuiltin(name, _, _) -> $"<builtin:{name}>"
    | VRender(renderType, _) -> $"<render:{renderType}>"

and private valueToString (value: Value) : string =
    match value with
    | VNumber n -> string n
    | VBool b -> string b
    | VString s -> s
    | VVector items -> "[" + String.Join(", ", items |> Array.map valueToString) + "]"
    | VClosure(parameters, _, _) -> $"""<function/{parameters.Length}>"""
    | VBuiltin(name, _, _) -> $"<builtin:{name}>"
    | VRender(renderType, _) -> $"<render:{renderType}>"

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

let private asString (context: string) (value: Value) : Result<string, string> =
    match value with
    | VString s -> Ok s
    | other -> Error $"{context}: expected a string, got {describe other}"

let private randomSoftware = Random.Shared

let private nextCryptoUnit () =
    let bytes = Array.zeroCreate<byte> 8
    RandomNumberGenerator.Fill(bytes)
    let raw = BitConverter.ToUInt64(bytes, 0) >>> 11
    float raw / float (1UL <<< 53)

let private buildRange (context: string) (startValue: float) (stopValue: float) (stepValue: float) =
    if stepValue = 0.0 then
        Error $"{context}: step must not be zero"
    else
        let rec loop current acc =
            if stepValue > 0.0 then
                if current > stopValue then
                    Ok(VVector(List.rev acc |> List.map VNumber |> List.toArray))
                else
                    loop (current + stepValue) (current :: acc)
            else
                if current < stopValue then
                    Ok(VVector(List.rev acc |> List.map VNumber |> List.toArray))
                else
                    loop (current + stepValue) (current :: acc)

        loop startValue []

let private buildLinspace (context: string) (startValue: float) (stopValue: float) (countValue: float) =
    let count = int (Math.Round countValue)
    if count <= 0 then
        Error $"{context}: count must be at least 1"
    elif count = 1 then
        Ok(VVector([| VNumber startValue |]))
    else
        let step = (stopValue - startValue) / float (count - 1)
        let values =
            [| for index in 0 .. count - 1 -> VNumber(startValue + float index * step) |]
        Ok(VVector(values))

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
///
/// `param(name, default)` allows programs to declare parameters that can be
/// bound by the host before execution. Returns the parameter value if provided,
/// otherwise returns the default. Used for interactive slider/button/input binding.
let makeBuiltinEnv (onEmit: (Value -> unit) option) (parameters: Map<string, Value> option) : Env =
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
      "random", VBuiltin("random", 0, function
          | [] -> Ok(VNumber(randomSoftware.NextDouble()))
          | args -> Error $"random: expected 0 arguments, got {args.Length}")
      "secure_random", VBuiltin("secure_random", 0, function
          | [] -> Ok(VNumber(nextCryptoUnit()))
          | args -> Error $"secure_random: expected 0 arguments, got {args.Length}")
      "range", VBuiltin("range", 3, function
          | [ startValue; stopValue; stepValue ] ->
              match asNumber "range" startValue, asNumber "range" stopValue, asNumber "range" stepValue with
              | Ok startValue, Ok stopValue, Ok stepValue -> buildRange "range" startValue stopValue stepValue
              | Error e, _, _ | _, Error e, _ | _, _, Error e -> Error e
          | args -> Error $"range: expected 3 arguments, got {args.Length}")
      "linspace", VBuiltin("linspace", 3, function
          | [ startValue; stopValue; countValue ] ->
              match asNumber "linspace" startValue, asNumber "linspace" stopValue, asNumber "linspace" countValue with
              | Ok startValue, Ok stopValue, Ok countValue -> buildLinspace "linspace" startValue stopValue countValue
              | Error e, _, _ | _, Error e, _ | _, _, Error e -> Error e
          | args -> Error $"linspace: expected 3 arguments, got {args.Length}")
      numeric2 "min" min
      numeric2 "max" max
      numeric2 "atan2" atan2
      "len", VBuiltin("len", 1, function
          | [ v ] -> asVector "len" v |> Result.map (fun items -> VNumber(float items.Length))
          | args -> Error $"len: expected 1 argument, got {args.Length}")
      "head", VBuiltin("head", 1, function
          | [ v ] ->
              asVector "head" v
              |> Result.bind (fun items ->
                  if items.Length = 0 then Error "head: vector is empty"
                  else Ok items.[0])
          | args -> Error $"head: expected 1 argument, got {args.Length}")
      "tail", VBuiltin("tail", 1, function
          | [ v ] ->
              asVector "tail" v
              |> Result.bind (fun items ->
                  if items.Length = 0 then Error "tail: vector is empty"
                  else Ok(VVector(items.[1..])))
          | args -> Error $"tail: expected 1 argument, got {args.Length}")
      "rev", VBuiltin("rev", 1, function
          | [ v ] -> asVector "rev" v |> Result.map (fun items -> VVector(Array.rev items))
          | args -> Error $"rev: expected 1 argument, got {args.Length}")
      "sum", VBuiltin("sum", 1, function
          | [ v ] ->
              asVector "sum" v
              |> Result.bind (fun items ->
                  let rec loop i acc =
                      if i >= items.Length then Ok(VNumber acc)
                      else
                          match asNumber "sum" items.[i] with
                          | Ok n -> loop (i + 1) (acc + n)
                          | Error e -> Error e
                  loop 0 0.0)
          | args -> Error $"sum: expected 1 argument, got {args.Length}")
      // map/filter/fold are registered here so lookup succeeds, but their
      // real implementations live in `applyCallable` -- they call back into
      // the evaluator to apply a user's closure per element, which a plain
      // (Value list -> Result<Value, string>) builtin function cannot do.
      "map", VBuiltin("map", 2, fun _ -> Error "map: handled by applyCallable")
      "filter", VBuiltin("filter", 2, fun _ -> Error "filter: handled by applyCallable")
      "fold", VBuiltin("fold", 3, fun _ -> Error "fold: handled by applyCallable")
      "emit", VBuiltin("emit", 1, function
          | [ v ] ->
              onEmit |> Option.iter (fun f -> f v)
              Ok v
          | args -> Error $"emit: expected 1 argument, got {args.Length}")
      "render", VBuiltin("render", 2, function
          | [ renderTypeVal; propsVal ] ->
              match asString "render type" renderTypeVal, asVector "render props" propsVal with
              | Ok renderType, Ok props ->
                  // Props is [key, value, key, value, ...] vector
                  if props.Length % 2 <> 0 then
                      Error "render: props vector must have even length (key-value pairs)"
                  else
                      let rec buildMap i (acc: Map<string, Value>) =
                          if i >= props.Length then
                              acc
                          else
                              match asString $"render prop key at index {i}" props.[i] with
                              | Error _ -> acc  // Skip malformed keys
                              | Ok key ->
                                  buildMap (i + 2) (Map.add key props.[i + 1] acc)
                      Ok(VRender(renderType, buildMap 0 Map.empty))
              | Error e, _ | _, Error e -> Error e
          | args -> Error $"render: expected 2 arguments, got {args.Length}")
      "string", VBuiltin("string", 1, function
          | [ v ] -> Ok(VString(valueToString v))
          | args -> Error $"string: expected 1 argument, got {args.Length}")
      "param", VBuiltin("param", 2, function
          | [ nameVal; defaultVal ] ->
              match asString "param" nameVal with
              | Error e -> Error e
              | Ok name ->
                  match parameters with
                  | Some paramMap ->
                      match Map.tryFind name paramMap with
                      | Some value -> Ok value
                      | None -> Ok defaultVal
                  | None -> Ok defaultVal
          | args -> Error $"param: expected 2 arguments, got {args.Length}")
      "pi", VNumber Math.PI
      "e", VNumber Math.E ]
    |> Map.ofList

let builtinEnv : Env = makeBuiltinEnv None None

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
    | String s -> Done(Ok(VString s))
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
    // map/filter/fold need to apply a user closure per element, which means
    // re-entering the evaluator -- so they're implemented here (in CPS, via
    // bindStep, so closures that recurse or call other let recs work) rather
    // than as plain builtin functions. Since this code is mostly written by
    // an AI, both argument orders are accepted -- map(vector, fn) and
    // map(fn, vector) -- disambiguated by which argument is the callable.
    | VBuiltin("map", 2, _) when args.Length = 2 ->
        (match sortVectorAndFn "map" args.[0] args.[1] with
         | Error e -> Done(Error e)
         | Ok(items, fn) ->
             let rec loop i acc =
                 if i >= Array.length items then Done(Ok(VVector(List.rev acc |> List.toArray)))
                 else bindStep (applyCallable fn [ items.[i] ]) (fun v -> loop (i + 1) (v :: acc))
             loop 0 [])
    | VBuiltin("filter", 2, _) when args.Length = 2 ->
        (match sortVectorAndFn "filter" args.[0] args.[1] with
         | Error e -> Done(Error e)
         | Ok(items, fn) ->
             let rec loop i acc =
                 if i >= Array.length items then Done(Ok(VVector(List.rev acc |> List.toArray)))
                 else
                     bindStep (applyCallable fn [ items.[i] ]) (fun keep ->
                         match keep with
                         | VBool true -> loop (i + 1) (items.[i] :: acc)
                         | VBool false -> loop (i + 1) acc
                         | other -> Done(Error $"filter: predicate must return a bool, got {describe other}"))
             loop 0 [])
    // fold(fn, init, vector) in the F# argument order, or fold(vector, init, fn);
    // the folder is called as fn(accumulator, element).
    | VBuiltin("fold", 3, _) when args.Length = 3 ->
        (match args.[0], args.[2] with
         | (VClosure _ | VBuiltin _) as fn, other ->
             (match asVector "fold" other with
              | Error e -> Done(Error e)
              | Ok items -> foldVector fn args.[1] items)
         | other, ((VClosure _ | VBuiltin _) as fn) ->
             (match asVector "fold" other with
              | Error e -> Done(Error e)
              | Ok items -> foldVector fn args.[1] items)
         | a, _ -> Done(Error $"fold: expected a function and a vector (plus an initial value), got {describe a} and {describe args.[2]}"))
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

/// Accepts a (vector, fn) argument pair in either order -- AI-written code
/// uses both `map(v, f)` and `map(f, v)` -- keyed off which one is callable.
and private sortVectorAndFn (context: string) (a: Value) (b: Value) : Result<Value[] * Value, string> =
    match a, b with
    | (VClosure _ | VBuiltin _) as fn, other -> asVector context other |> Result.map (fun items -> items, fn)
    | other, ((VClosure _ | VBuiltin _) as fn) -> asVector context other |> Result.map (fun items -> items, fn)
    | _ -> Error $"{context}: expected a vector and a function, got {describe a} and {describe b}"

and private foldVector (fn: Value) (init: Value) (items: Value[]) : Step =
    let rec loop i acc =
        if i >= items.Length then Done(Ok acc)
        else bindStep (applyCallable fn [ acc; items.[i] ]) (fun next -> loop (i + 1) next)
    loop 0 init

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
    | Add ->
        match left, right with
        | VString l, VString r -> Ok(VString(l + r))
        | VString l, r -> Ok(VString(l + valueToString r))
        | l, VString r -> Ok(VString(valueToString l + r))
        | _ -> numOp (fun l r -> VNumber(l + r))
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
    | Cons ->
        match right with
        | VVector items -> Ok(VVector(Array.append [| left |] items))
        | other -> Error $"::: right side must be a vector, got {describe other} (did you mean [{describe left}] or {describe left} :: [...]?)"
    | Append ->
        match left, right with
        | VVector l, VVector r -> Ok(VVector(Array.append l r))
        | VVector _, other -> Error $"@: right side must be a vector, got {describe other}"
        | other, _ -> Error $"@: left side must be a vector, got {describe other} (use :: to prepend a single element)"
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
    eval (makeBuiltinEnv (Some onEmit) None) expr

/// Evaluates with `emit(...)` wired to `onEmit`, `param()` bound to parameters,
/// and a host-supplied `data` binding available to the program.
let runWithEmitAndData (onEmit: Value -> unit) (dataValue: Value option) (expr: Expr) : Result<Value, string> =
    let env =
        match dataValue with
        | Some value -> Map.add "data" value (makeBuiltinEnv (Some onEmit) None)
        | None -> makeBuiltinEnv (Some onEmit) None

    eval env expr

/// Evaluates with `emit(...)` wired to `onEmit`, `param()` bound to parameters,
/// and optional `data` binding. Used for interactive programs where parameters
/// come from the host (e.g., slider values, form inputs).
let runWithEmitAndDataAndParams (onEmit: Value -> unit) (dataValue: Value option) (parameters: Map<string, Value>) (expr: Expr) : Result<Value, string> =
    let env =
        let baseEnv = makeBuiltinEnv (Some onEmit) (Some parameters)
        match dataValue with
        | Some value -> Map.add "data" value baseEnv
        | None -> baseEnv

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
    | VString s -> Ok(JsonSerializer.Serialize s)  // Use JsonSerializer to properly escape string
    | VVector items ->
        let rec collect acc =
            function
            | [] -> Ok(List.rev acc)
            | x :: rest ->
                match toJson x with
                | Ok j -> collect (j :: acc) rest
                | Error e -> Error e
        items |> Array.toList |> collect [] |> Result.map (fun parts -> "[" + String.Join(",", parts) + "]")
    | VRender(renderType, props) ->
        // Render values are kept as structured objects, not serialized to JSON
        // The host will handle them specially through the emit() channel
        Error $"render objects cannot be serialized to JSON (use emit to send them to the host)"
    | VClosure _ | VBuiltin _ -> Error $"cannot serialize {describe value} to JSON"

module DslTests

open System
open Xunit
open Djehuti.DjeLab.Dsl.Evaluator
open Djehuti.DjeLab.Dsl.Preflight

let private evalOk (source: string) =
    match Djehuti.DjeLab.Dsl.Parser.parse source with
    | Error e -> failwith $"parse error: {e}"
    | Ok expr ->
        match run expr with
        | Error e -> failwith $"eval error: {e}"
        | Ok value -> value

let private evalNumber (source: string) =
    match evalOk source with
    | VNumber n -> n
    | other -> failwith $"expected a number, got {other}"

[<Fact>]
let ``arithmetic respects precedence`` () =
    Assert.Equal(7.0, evalNumber "1 + 2 * 3")
    Assert.Equal(9.0, evalNumber "(1 + 2) * 3")
    Assert.Equal(8.0, evalNumber "2 ^ 3")
    Assert.Equal(2.0, evalNumber "2 ^ 3 ^ 0") // right-associative: 2 ^ (3 ^ 0) = 2 ^ 1

[<Fact>]
let ``unary negation and not`` () =
    Assert.Equal(-5.0, evalNumber "-5")
    Assert.Equal(3.0, evalNumber "1 - -2")
    match evalOk "not true" with
    | VBool b -> Assert.False(b)
    | other -> failwith $"expected a bool, got {other}"

[<Fact>]
let ``let binds a value for the body`` () =
    Assert.Equal(15.0, evalNumber "let x = 5 in x * 3")

[<Fact>]
let ``let can shadow an outer binding`` () =
    Assert.Equal(2.0, evalNumber "let x = 1 in let x = 2 in x")

[<Fact>]
let ``if branches on a bool condition`` () =
    Assert.Equal(1.0, evalNumber "if 1 < 2 then 1 else 0")
    Assert.Equal(0.0, evalNumber "if 1 > 2 then 1 else 0")

[<Fact>]
let ``lambda and call`` () =
    Assert.Equal(9.0, evalNumber "let square = fun x -> x * x in square(3)")

[<Fact>]
let ``let rec supports real recursion, not just one level`` () =
    // This is the case that would silently break under a buggy tie-the-knot
    // implementation: a naive immutable-Map approach lets the function call
    // itself exactly once (the first recursive call resolves to a closure
    // that doesn't itself know its own name) and then throws "Unbound
    // variable" on the second recursive call. Factorial of 5 requires 5
    // nested self-calls, so this only passes if recursion is genuinely
    // unbounded.
    Assert.Equal(120.0, evalNumber "let rec fact n = if n <= 1 then 1 else n * fact(n - 1) in fact(5)")

[<Fact>]
let ``let rec handles mutual-depth recursion via fibonacci`` () =
    let src =
        "let rec fib n = if n < 2 then n else fib(n - 1) + fib(n - 2) in fib(10)"
    Assert.Equal(55.0, evalNumber src)

[<Fact>]
let ``vectors and indexing`` () =
    Assert.Equal(2.0, evalNumber "let v = [1, 2, 3] in v[1]")
    Assert.Equal(3.0, evalNumber "len([1, 2, 3])")

[<Fact>]
let ``builtin math functions`` () =
    Assert.Equal(4.0, evalNumber "sqrt(16)")
    Assert.Equal(5.0, evalNumber "max(3, 5)")

[<Fact>]
let ``random builtins return unit interval numbers`` () =
    let randomValue =
        match evalOk "random()" with
        | VNumber n -> n
        | other -> failwith $"expected a number, got {other}"

    let secureRandomValue =
        match evalOk "secure_random()" with
        | VNumber n -> n
        | other -> failwith $"expected a number, got {other}"

    Assert.InRange(randomValue, 0.0, 1.0)
    Assert.InRange(secureRandomValue, 0.0, 1.0)
    Assert.True(randomValue < 1.0)
    Assert.True(secureRandomValue < 1.0)

[<Fact>]
let ``equality on numbers, bools, and vectors`` () =
    match evalOk "[1, 2] == [1, 2]" with
    | VBool b -> Assert.True(b)
    | other -> failwith $"expected a bool, got {other}"
    match evalOk "[1, 2] == [1, 3]" with
    | VBool b -> Assert.False(b)
    | other -> failwith $"expected a bool, got {other}"

[<Fact>]
let ``comparing functions for equality is a DSL-level error, not a crash`` () =
    match Djehuti.DjeLab.Dsl.Parser.parse "(fun x -> x) == (fun x -> x)" with
    | Error e -> failwith $"parse error: {e}"
    | Ok expr ->
        match run expr with
        | Error _ -> () // expected: functions are not comparable
        | Ok v -> failwith $"expected an error, got a value: {v}"

[<Fact>]
let ``and or short-circuit`` () =
    // The right side would error if evaluated (unbound variable), so this
    // only passes if short-circuiting actually skips it.
    match evalOk "false && boom" with
    | VBool b -> Assert.False(b)
    | other -> failwith $"expected a bool, got {other}"
    match evalOk "true || boom" with
    | VBool b -> Assert.True(b)
    | other -> failwith $"expected a bool, got {other}"

[<Fact>]
let ``unbound variable is a clear error, not an exception`` () =
    match Djehuti.DjeLab.Dsl.Parser.parse "doesNotExist" with
    | Error e -> failwith $"parse error: {e}"
    | Ok expr ->
        match run expr with
        | Error msg -> Assert.Contains("Unbound variable", msg)
        | Ok v -> failwith $"expected an error, got a value: {v}"

[<Fact>]
let ``long tail recursion can keep running without an artificial budget cap`` () =
    match Djehuti.DjeLab.Dsl.Parser.parse "let rec loop n = if n == 0 then n else loop(n - 1) in loop(20000)" with
    | Error e -> failwith $"parse error: {e}"
    | Ok expr ->
        match run expr with
        | Ok(VNumber 0.0) -> ()
        | Ok v -> failwith $"expected 0, got {v}"
        | Error msg -> failwith $"expected a successful long run, got error: {msg}"

[<Fact>]
let ``emit is a no-op pass-through when nothing is listening`` () =
    Assert.Equal(5.0, evalNumber "emit(5) + 0")

[<Fact>]
let ``runWithEmit invokes the callback for every emit call, in order`` () =
    let seen = System.Collections.Generic.List<Value>()
    let source = "let rec loop i = if i == 3 then i else loop(emit(i) + 1) in loop(0)"
    match Djehuti.DjeLab.Dsl.Parser.parse source with
    | Error e -> failwith $"parse error: {e}"
    | Ok expr ->
        match runWithEmit seen.Add expr with
        | Error e -> failwith $"eval error: {e}"
        | Ok(VNumber 3.0) -> ()
        | Ok v -> failwith $"expected 3, got {v}"
    // Value has no structural equality (it embeds functions), so compare
    // via the numbers actually seen rather than the Value list itself.
    let seenNumbers = seen |> Seq.map (function VNumber n -> n | v -> failwith $"expected a number, got {v}") |> List.ofSeq
    Assert.Equal<float list>([ 0.0; 1.0; 2.0 ], seenNumbers)

[<Fact>]
let ``toJson serializes numbers, bools, and vectors`` () =
    Assert.Equal(Ok "3", toJson (VNumber 3.0))
    Assert.Equal(Ok "true", toJson (VBool true))
    Assert.Equal(Ok "[1,2,3]", toJson (VVector [| VNumber 1.0; VNumber 2.0; VNumber 3.0 |]))

[<Fact>]
let ``toJson renders non-finite numbers as null instead of throwing`` () =
    Assert.Equal(Ok "null", toJson (VNumber(1.0 / 0.0)))
    Assert.Equal(Ok "null", toJson (VNumber(0.0 / 0.0)))

[<Fact>]
let ``toJson rejects function values`` () =
    match toJson (VBuiltin("sin", 1, Ok << List.head)) with
    | Error _ -> ()
    | Ok j -> failwith $"expected an error, got {j}"

[<Fact>]
let ``a missing 'in' after a nested let reports the real location, not a misleading reserved-word error`` () =
    // Found live: a nested `let` written without its `in` (a very natural
    // mistake for an LLM used to Python/JS-style one-statement-per-line) used
    // to make `letExpr` fail deep inside, backtrack all the way to column 1,
    // and fall through to the plain-identifier parser mistakenly rejecting
    // the outer "let" as a reserved word -- nowhere near the real problem.
    match Djehuti.DjeLab.Dsl.Parser.parse
        """
        let rec loop t =
            if t == 100 then t
            else
                let dummy = emit([t, sin(t)])
                loop(t + 1)
        in loop(0)
        """
    with
    | Ok v -> failwith $"expected a parse error, got {v}"
    | Error e ->
        Assert.DoesNotContain("reserved word", e)
        Assert.Contains("Ln: 6", e) // the "loop(t + 1)" line, not line 1

[<Fact>]
let ``emit called with brackets instead of parens reports the real location`` () =
    match Djehuti.DjeLab.Dsl.Parser.parse "let rec loop t = if t == 1 then t else let d = emit [t, t] in loop(t + 1) in loop(0)" with
    | Ok v -> failwith $"expected a parse error, got {v}"
    | Error e -> Assert.DoesNotContain("reserved word", e)

[<Fact>]
let ``parse error is reported instead of throwing`` () =
    match Djehuti.DjeLab.Dsl.Parser.parse "1 +" with
    | Error _ -> ()
    | Ok expr -> failwith $"expected a parse error, got {expr}"

[<Fact>]
let ``preflight accepts clean Spinoza and rejects file I O`` () =
    let okReport = validate "let x = 1 in x + 2"
    Assert.True(okReport.CanRun)
    Assert.True(okReport.Parsed)

    let badReport = validate "let data = readCSV(\"sample.csv\") in data"
    Assert.False(badReport.CanRun)
    Assert.Contains(badReport.Issues, fun issue -> issue.Message.Contains("cannot read files", StringComparison.OrdinalIgnoreCase))

// ── F#-style list operations (:: @ map/filter/fold/head/tail/rev/sum) ──────
// Added because AI-generated Spinoza (the language's primary author) writes
// ML-family list idioms instinctively; these tests pin the exact syntax the
// CERN-data session kept failing on.

let private evalVector (source: string) =
    match evalOk source with
    | VVector items -> items |> Array.map (function VNumber n -> n | other -> failwith $"expected numbers, got {other}")
    | other -> failwith $"expected a vector, got {other}"

[<Fact>]
let ``cons prepends an element to a vector`` () =
    Assert.Equal<float[]>([| 1.0; 2.0; 3.0 |], evalVector "1 :: [2, 3]")

[<Fact>]
let ``cons is right-associative`` () =
    Assert.Equal<float[]>([| 1.0; 2.0; 3.0 |], evalVector "1 :: 2 :: [3]")

[<Fact>]
let ``cons binds looser than addition`` () =
    // n + 1 :: xs must mean (n + 1) :: xs
    Assert.Equal<float[]>([| 3.0; 9.0 |], evalVector "1 + 2 :: [9]")

[<Fact>]
let ``append concatenates two vectors`` () =
    Assert.Equal<float[]>([| 1.0; 2.0; 3.0; 4.0 |], evalVector "[1, 2] @ [3, 4]")

[<Fact>]
let ``cons onto a non-vector reports a helpful error`` () =
    match Djehuti.DjeLab.Dsl.Parser.parse "1 :: 2" with
    | Error e -> failwith $"parse error: {e}"
    | Ok expr ->
        match run expr with
        | Error e -> Assert.Contains("right side must be a vector", e)
        | Ok v -> failwith $"expected an error, got {v}"

[<Fact>]
let ``cons works inside a recursive accumulator loop`` () =
    // The exact shape Seshat generated for the CERN data session:
    // walking rows and consing onto an accumulator.
    let source =
        """
        let rec collect i acc =
          if i >= 5 then acc
          else collect(i + 1, i * i :: acc)
        in rev(collect(0, []))
        """
    Assert.Equal<float[]>([| 0.0; 1.0; 4.0; 9.0; 16.0 |], evalVector source)

[<Fact>]
let ``map applies a lambda over a vector in either argument order`` () =
    Assert.Equal<float[]>([| 1.0; 4.0; 9.0 |], evalVector "map([1, 2, 3], fun x -> x * x)")
    Assert.Equal<float[]>([| 1.0; 4.0; 9.0 |], evalVector "map(fun x -> x * x, [1, 2, 3])")

[<Fact>]
let ``map works with a recursive let rec function`` () =
    // The old dead-code implementation of map could not handle closures that
    // recurse (it bailed on any More step); this pins the fix.
    let source =
        """
        let rec fact n = if n <= 1 then 1 else n * fact(n - 1) in
        map([3, 4, 5], fact)
        """
    Assert.Equal<float[]>([| 6.0; 24.0; 120.0 |], evalVector source)

[<Fact>]
let ``filter keeps matching elements`` () =
    Assert.Equal<float[]>([| 2.0; 4.0 |], evalVector "filter([1, 2, 3, 4], fun x -> x % 2 == 0)")

[<Fact>]
let ``fold accumulates in F# argument order`` () =
    Assert.Equal(10.0, evalNumber "fold(fun acc x -> acc + x, 0, [1, 2, 3, 4])")
    Assert.Equal(10.0, evalNumber "fold([1, 2, 3, 4], 0, fun acc x -> acc + x)")

[<Fact>]
let ``head tail rev sum work`` () =
    Assert.Equal(1.0, evalNumber "head([1, 2, 3])")
    Assert.Equal<float[]>([| 2.0; 3.0 |], evalVector "tail([1, 2, 3])")
    Assert.Equal<float[]>([| 3.0; 2.0; 1.0 |], evalVector "rev([1, 2, 3])")
    Assert.Equal(6.0, evalNumber "sum([1, 2, 3])")

[<Fact>]
let ``head and tail of empty vector report errors`` () =
    match Djehuti.DjeLab.Dsl.Parser.parse "head([])" with
    | Error e -> failwith $"parse error: {e}"
    | Ok expr ->
        match run expr with
        | Error e -> Assert.Contains("empty", e)
        | Ok v -> failwith $"expected an error, got {v}"

/// Parses DjeLab DSL source text into an `Ast.Expr`.
///
/// Grammar (informal):
///   expr      := let | letrec | if | lambda | orExpr
///   let       := "let" ident "=" expr "in" expr
///   letrec    := "let" "rec" ident ident* "=" expr "in" expr
///   if        := "if" expr "then" expr "else" expr
///   lambda    := "fun" ident+ "->" expr
///   orExpr    := andExpr ("||" andExpr)*
///   andExpr   := cmpExpr ("&&" cmpExpr)*
///   cmpExpr   := addExpr (("==" | "!=" | "<=" | ">=" | "<" | ">") addExpr)?
///   addExpr   := mulExpr (("+" | "-") mulExpr)*
///   mulExpr   := powExpr (("*" | "/" | "%") powExpr)*
///   powExpr   := unary ("^" powExpr)?          -- right-associative
///   unary     := ("-" | "not") unary | postfix
///   postfix   := atom ("(" args ")" | "[" expr "]")*
///   atom      := number | bool | ident | "(" expr ")" | "[" args "]"
module Djehuti.DjeLab.Dsl.Parser

open FParsec
open Djehuti.DjeLab.Dsl.Ast

type private P<'a> = Parser<'a, unit>

let private ws = spaces
let private ws1 = spaces1

let private strWs (s: string) = pstring s .>> ws

let private reserved =
    set [ "let"; "rec"; "in"; "if"; "then"; "else"; "fun"; "true"; "false"; "not" ]

let private identifier: P<string> =
    let isIdStart c = isLetter c || c = '_'
    let isIdCont c = isLetter c || isDigit c || c = '_'
    (many1Satisfy2 isIdStart isIdCont .>> ws)
    >>= fun name ->
        if reserved.Contains name then
            fail $"'{name}' is a reserved word"
        else
            preturn name

let private numberLit: P<Expr> =
    pfloat .>> ws |>> Number

let private boolLit: P<Expr> =
    (stringReturn "true" (Bool true) <|> stringReturn "false" (Bool false)) .>> ws

let private exprRef, exprRefImpl = createParserForwardedToRef<Expr, unit> ()

let private argList: P<Expr list> =
    sepBy exprRef (strWs ",")

let private vectorLit: P<Expr> =
    between (strWs "[") (strWs "]") argList |>> VectorLit

let private parenOrGroup: P<Expr> =
    between (strWs "(") (strWs ")") exprRef

let private atom: P<Expr> =
    choice
        [ numberLit
          boolLit
          vectorLit
          parenOrGroup
          identifier |>> Var ]

/// Postfix: call `f(a, b)` and index `v[i]`, left-associative and chainable
/// (e.g. `matrix[i][j]`, `curry(x)(y)`).
let private postfix: P<Expr> =
    let callSuffix = between (strWs "(") (strWs ")") argList |>> fun args -> fun e -> Call(e, args)
    let indexSuffix = between (strWs "[") (strWs "]") exprRef |>> fun idx -> fun e -> Index(e, idx)
    pipe2 atom (many (callSuffix <|> indexSuffix)) (fun head suffixes -> List.fold (fun acc f -> f acc) head suffixes)

let private unaryRef, unaryRefImpl = createParserForwardedToRef<Expr, unit> ()
let private powExprRef, powExprRefImpl = createParserForwardedToRef<Expr, unit> ()

do
    unaryRefImpl.Value <-
        (strWs "-" >>. unaryRef |>> fun e -> UnaryOp(Neg, e))
        <|> (strWs "not" >>. unaryRef |>> fun e -> UnaryOp(Not, e))
        <|> postfix

do
    powExprRefImpl.Value <-
        unaryRef .>>. opt (strWs "^" >>. powExprRef)
        |>> function
            | left, Some right -> BinaryOp(Pow, left, right)
            | left, None -> left

let private mulExpr: P<Expr> =
    let op = choice [ strWs "*" >>% Mul; strWs "/" >>% Div; strWs "%" >>% Mod ]
    chainl1 powExprRef (op |>> fun o l r -> BinaryOp(o, l, r))

let private addExpr: P<Expr> =
    let op = choice [ strWs "+" >>% Add; strWs "-" >>% Sub ]
    chainl1 mulExpr (op |>> fun o l r -> BinaryOp(o, l, r))

let private cmpExpr: P<Expr> =
    let op =
        choice
            [ strWs "==" >>% Eq
              strWs "!=" >>% Neq
              attempt (strWs "<=") >>% Lte
              attempt (strWs ">=") >>% Gte
              strWs "<" >>% Lt
              strWs ">" >>% Gt ]
    addExpr .>>. opt (op .>>. addExpr)
    |>> function
        | left, Some(o, right) -> BinaryOp(o, left, right)
        | left, None -> left

let private andExpr: P<Expr> =
    chainl1 cmpExpr (strWs "&&" >>% fun l r -> BinaryOp(And, l, r))

let private orExpr: P<Expr> =
    chainl1 andExpr (strWs "||" >>% fun l r -> BinaryOp(Or, l, r))

let private letExpr: P<Expr> =
    strWs "let"
    >>. opt (strWs "rec")
    .>>. identifier
    .>>. many identifier
    .>> strWs "="
    .>>. exprRef
    .>> strWs "in"
    .>>. exprRef
    |>> fun ((((isRec, name), parameters), value), body) ->
        match isRec, parameters with
        | Some _, _ -> LetRec(name, parameters, value, body)
        | None, [] -> Let(name, value, body)
        | None, _ -> Let(name, Lambda(parameters, value), body)

let private ifExpr: P<Expr> =
    strWs "if" >>. exprRef .>> strWs "then" .>>. exprRef .>> strWs "else" .>>. exprRef
    |>> fun ((cond, thenB), elseB) -> If(cond, thenB, elseB)

let private lambdaExpr: P<Expr> =
    strWs "fun" >>. many1 identifier .>> strWs "->" .>>. exprRef
    |>> fun (parameters, body) -> Lambda(parameters, body)

// Deliberately NOT `attempt`-wrapped: `let`/`if`/`fun` are unambiguous
// lookahead tokens in this grammar (§ the module doc comment) -- if the
// input starts with the literal word "let", it can only be a let-form,
// never anything else. `pstring "let"` already fails without consuming
// input when the token doesn't match, which is all `choice` needs to try
// the next alternative; wrapping the FULL let/if/fun parsers in `attempt`
// bought nothing for that case but was actively harmful for every other
// case: it silently discarded the true failure position whenever the
// keyword matched but something LATER in that construct was wrong (e.g. a
// missing `in`), backtracking all the way to column 1 and letting the
// fallback `identifier` parser misreport the real error as "'let' is a
// reserved word" pointing at the wrong place entirely.
do
    exprRefImpl.Value <-
        choice
            [ letExpr
              ifExpr
              lambdaExpr
              orExpr ]

let private program: P<Expr> = ws >>. exprRef .>> ws .>> eof

/// Parses DjeLab DSL source into an AST, or a human-readable error message.
let parse (source: string) : Result<Expr, string> =
    // `Ok`/`Error` are qualified as `Result.Ok`/`Result.Error` here because
    // FParsec's `Reply<'a>` exposes members named `Ok`/`Error` that would
    // otherwise shadow F#'s Result case constructors after `open FParsec`.
    match FParsec.CharParsers.run program source with
    | FParsec.CharParsers.ParserResult.Success(expr, _, _) -> Result.Ok expr
    | FParsec.CharParsers.ParserResult.Failure(message, _, _) -> Result.Error message

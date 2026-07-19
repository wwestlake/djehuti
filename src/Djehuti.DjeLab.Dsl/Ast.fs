/// The DjeLab DSL's abstract syntax tree.
///
/// This is deliberately the *entire* vocabulary of what a DjeLab program can
/// express. There is no node type for file I/O, network access, reflection,
/// or mutable state -- those aren't blocked by a guardrail pass, they simply
/// have no representation in the tree, so they cannot be constructed by the
/// parser in the first place. The only way to repeat computation is
/// recursion (LetRec); there is no loop construct.
module Djehuti.DjeLab.Dsl.Ast

type BinOp =
    | Add
    | Sub
    | Mul
    | Div
    | Mod
    | Pow
    | Eq
    | Neq
    | Lt
    | Lte
    | Gt
    | Gte
    | And
    | Or
    /// F#-style list cons: `x :: xs` prepends x to vector xs. Added because
    /// AI-generated code (the language's primary author) writes ML-family
    /// list idioms instinctively -- supporting them beats prompting against
    /// them.
    | Cons
    /// F#-style list append: `xs @ ys` concatenates two vectors.
    | Append

type UnOp =
    | Neg
    | Not

type Expr =
    | Number of float
    | Bool of bool
    | String of string
    | Var of string
    | VectorLit of Expr list
    | Index of Expr * Expr
    | UnaryOp of UnOp * Expr
    | BinaryOp of BinOp * Expr * Expr
    | If of cond: Expr * thenBranch: Expr * elseBranch: Expr
    /// Non-recursive binding: `value` cannot refer to `name`.
    | Let of name: string * value: Expr * body: Expr
    /// Recursive function binding: `funcBody` may refer to `name` for
    /// recursion (the only repetition mechanism in the language).
    | LetRec of name: string * parameters: string list * funcBody: Expr * body: Expr
    | Lambda of parameters: string list * body: Expr
    | Call of callee: Expr * args: Expr list

﻿module Spiral.PartEval

open Spiral.HashConsing
open Spiral.Tokenize
open Spiral.Parsing
open Spiral.Prepass
open Spiral.Utils
open System.Collections.Generic
open System

type Env<'a,'b> = {type' : StackSize * 'a []; value : StackSize * 'b []}

type ReifiedJoinPointLayout =
    | RJPStack
    | RJPHeap

type Tag = int
type [<CustomComparison;CustomEquality>] T<'a,'b when 'a: equality and 'a: comparison> = 
    | T of 'a * 'b

    override a.Equals(b) =
        match b with
        | :? T<'a,'b> as b -> match a,b with T(a,_), T(b,_) -> a = b
        | _ -> false
    override a.GetHashCode() = match a with T(a,_) -> hash a
    interface IComparable with
        member a.CompareTo(b) = 
            match b with
            | :? T<'a,'b> as b -> match a,b with T(a,_), T(b,_) -> compare a b
            | _ -> raise <| ArgumentException "Invalid comparison for T."

type Ty =
    | BT
    | PairT of Ty * Ty
    | KeywordT of KeywordTag * Ty []
    | FunctionT of Expr * StackSize * Ty [] * StackSize * Ty [] * is_forall : bool // TODO: Remove this. Disallow raw functions being returned from JPs and if statements.
    | TypeFunctionT of TExpr * StackSize * Ty []
    | RecordT of Map<KeywordTag, Ty>
    | YPrim of PrimitiveType
    
    | RJPT of ReifiedJoinPointLayout * RData
    | ArrayT of Ty
    | RuntimeFunctionT of Ty * Ty
    | MacroT of RData

and Data =
    | TyB
    | TyPair of Data * Data
    | TyKeyword of KeywordTag * Data []
    | TyFunction of Expr * StackSize * Ty [] * StackSize * Data [] * is_forall : bool
    | TyRecord of Map<KeywordTag, Data>
    | DLit of Literal
    | TyV of TyV

and RData = 
    | RTyB
    | RTyPair of ConsedNode<RData * RData>
    | RTyKeyword of ConsedNode<KeywordTag * RData []>
    | RTyFunction of ConsedNode<Expr * StackSize * Ty [] * StackSize * RData [] * bool>
    | RTyRecord of ConsedNode<Map<KeywordTag, RData>>
    | RTyLit of Literal
    | RTyV of ConsedNode<Tag * Ty>

and TyV = T<Tag,Ty>

let lit_is = function
    | DLit _ -> true
    | _ -> false

type Trace = ParserCombinators.PosKey list
type JoinPointKey = 
    | JPMethod of Expr * ConsedNode<RData [] * Ty []>
    | JPClosure of Expr * ConsedNode<RData [] * Ty [] * Ty [] list>

type JoinPointCall = JoinPointKey * TyV []

type TypedBind =
    | TyLet of Data * Trace * TypedOp
    | TyLocalReturnOp of Trace * TypedOp
    | TyLocalReturnData of Data * Trace

and TypedOp = 
    | TyOp of Op * Data []
    | TyIf of cond: Data * tr: TypedBind [] * fl: TypedBind []
    | TyWhile of cond: JoinPointCall * TypedBind []
    | TyJoinPoint of JoinPointCall

let data_to_rdata'' (hc : HashConsTable) call_data =
    let hc x = hc.Add x
    let m = Dictionary(HashIdentity.Reference)
    let call_args = ResizeArray()
    let rec f x =
        memoize m (function
            | TyPair(a,b) -> RTyPair(hc(f a, f b))
            | TyKeyword(a,b) -> RTyKeyword(hc (a, Array.map f b))
            | TyFunction(a,b,c,d,e,z) -> RTyFunction(hc(a,b,c,d,Array.map f e,z))
            | TyRecord l -> RTyRecord(hc(Map.map (fun _ -> f) l))
            | TyV(T(v,ty) as t) -> call_args.Add t; RTyV(hc (call_args.Count-1,ty))
            | DLit v -> RTyLit v
            | TyB -> RTyB
        ) x
    let x = Array.map f call_data
    call_args.ToArray(),x

let data_to_rdata' (hc : HashConsTable) call_data = let a,b = data_to_rdata'' hc [|call_data|] in a,b.[0]
let data_to_rdata hc call_data = data_to_rdata' hc call_data |> snd // TODO: Specialize this.
let (|R|) (x : ConsedNode<'a>) = x.node

let rdata_to_data' i call_data =
    let m = Dictionary(HashIdentity.Reference)
    let r_args = ResizeArray()
    let rec f x =
        memoize m (function
            | RTyPair(R(a,b)) -> TyPair(f a, f b)
            | RTyKeyword(R(a,b)) -> TyKeyword(a, Array.map f b)
            | RTyFunction(R(a,b,c,d,e,z)) -> TyFunction(a,b,c,d,Array.map f e,z)
            | RTyRecord(R l) -> TyRecord(Map.map (fun _ -> f) l)
            | RTyV(R(v,ty)) -> r_args.Add(v,ty); let r = TyV(T(!i,ty)) in i := !i+1; r
            | RTyLit v -> DLit v
            | RTyB -> TyB
            ) x
    let x = f call_data
    r_args.ToArray(),x

let rdata_to_data i x = rdata_to_data' i x |> snd // TODO: Specialize this.

let data_free_vars call_data =
    let m = HashSet(HashIdentity.Reference)
    let free_vars = ResizeArray()
    let rec f x =
        if m.Add x then
            match x with
            | TyPair(a,b) -> f a; f b
            | TyKeyword(a,b) -> Array.iter f b
            | TyFunction(a,b,c,d,e,z) -> Array.iter f e
            | TyRecord l -> Map.iter (fun _ -> f) l
            | TyV(T(_,ty) as t) -> free_vars.Add t
            | DLit _ | TyB -> ()
    f call_data
    free_vars.ToArray()

let data_term_vars call_data =
    let term_vars = ResizeArray(64)
    let rec f = function
        | TyPair(a,b) -> f a; f b
        | TyKeyword(a,b) -> Array.iter f b
        | TyFunction(a,b,c,d,e,z) -> Array.iter f e
        | TyRecord l -> Map.iter (fun _ -> f) l
        | DLit _ | TyV _ as x -> term_vars.Add x
        | TyB -> ()
    f call_data
    term_vars.ToArray()

let rdata_free_vars x = 
    let m = HashSet(HashIdentity.Reference)
    let free_vars = ResizeArray()
    let rec f x =
        if m.Add x then
            match x with
            | RTyPair(R(a,b)) -> f a; f b
            | RTyKeyword(R(a,b)) -> Array.iter f b
            | RTyFunction(R(a,b,c,d,e,z)) -> Array.iter f e
            | RTyRecord(R l) -> Map.iter (fun _ -> f) l
            | RTyV(R(v,ty)) -> free_vars.Add(v,ty)
            | RTyLit _ | RTyB -> ()
            
    f x
    free_vars.ToArray()

let ty_to_data i x =
    let rec f = function
        | BT -> TyB
        | PairT(a,b) -> TyPair(f a, f b) 
        | KeywordT(a,b) -> TyKeyword(a,Array.map f b)
        | FunctionT(a,b,c,d,e,z) -> TyFunction(a,b,c,d,Array.map f e,z)
        | RecordT l -> TyRecord(Map.map (fun k -> f) l)
        | YPrim _ | RJPT _ | ArrayT _ | RuntimeFunctionT _ | MacroT _ as x -> let r = TyV(T(!i,x)) in i := !i+1; r
        | TypeFunctionT _ -> failwith "Compiler error: Cannot turn a type function to a runtime variable."
    f x

let ty_vars x =
    let vars = ResizeArray()
    let rec f = function
        | BT -> ()
        | PairT(a,b) -> f a; f b
        | KeywordT(a,b) -> Array.iter f b
        | FunctionT(a,b,c,d,e,z) -> Array.iter f e
        | RecordT l -> Map.iter (fun k -> f) l
        | YPrim _ | RJPT _ | ArrayT _ | RuntimeFunctionT _ | MacroT _ as x -> vars.Add(x)
        | TypeFunctionT _ -> failwith "Compiler error: Cannot turn a type function to a runtime variable."
    f x
    vars.ToArray()

let lit_to_primitive_type = function
    | LitUInt8 _ -> UInt8T
    | LitUInt16 _ -> UInt16T
    | LitUInt32 _ -> UInt32T
    | LitUInt64 _ -> UInt64T
    | LitInt8 _ -> Int8T
    | LitInt16 _ -> Int16T
    | LitInt32 _ -> Int32T
    | LitInt64 _ -> Int64T
    | LitFloat32 _ -> Float32T
    | LitFloat64 _ -> Float64T   
    | LitBool _ -> BoolT
    | LitString _ -> StringT
    | LitChar _ -> CharT

let lit_to_ty x = lit_to_primitive_type x |> YPrim

let rdata_to_ty call_data =
    let m = Dictionary(HashIdentity.Reference)
    let rec f x =
        memoize m (function
            | RTyPair(R(a,b)) -> PairT(f a, f b)
            | RTyKeyword(R(a,b)) -> KeywordT(a, Array.map f b)
            | RTyFunction(R(a,b,c,d,e,z)) -> FunctionT(a,b,c,d,Array.map f e,z)
            | RTyRecord(R l) -> RecordT(Map.map (fun _ -> f) l)
            | RTyV(R(v,ty)) -> ty
            | RTyLit x -> lit_to_ty x
            | RTyB -> BT
            ) x
    f call_data

let data_to_ty x =
    let m = Dictionary(HashIdentity.Reference)
    let rec f x =
        memoize m (function
            | TyPair(a,b) -> PairT(f a, f b)
            | TyKeyword(a,b) -> KeywordT(a, Array.map f b)
            | TyFunction(a,b,c,d,e,z) -> FunctionT(a,b,c,d,Array.map f e,z)
            | TyRecord l -> RecordT(Map.map (fun _ -> f) l)
            | TyV(T(_,ty) as t) -> ty
            | DLit x -> lit_to_ty x
            | TyB -> BT
            ) x
    f x

let (|TyType|) x = data_to_ty x

type ExternalLangEnv = {
    keywords : KeywordEnv
    hc_table : HashConsTable
    // TODO: Replace these dicts with weak tables.
    join_point_method : Dictionary<Expr,Dictionary<ConsedNode<RData [] * Ty []>, (TyV [] * TypedBind [] * Ty) option>>
    memoized_modules_value : Dictionary<Map<KeywordTag,Expr>,Data>
    memoized_modules_type : Dictionary<Map<KeywordTag,TExpr>,Ty>
    }

type LangEnv = {
    trace : Trace
    seq : ResizeArray<TypedBind>
    cse : Dictionary<Op * Data [], Data> list
    i : VarTag ref
    env_global_type : Ty []
    env_global_value : Data []
    env_stack_type : Ty []
    env_stack_type_ptr : int
    env_stack_value : Data []
    env_stack_value_ptr : int 
    }

let push_value_var x (d: LangEnv) =
    d.env_stack_value.[d.env_stack_value_ptr] <- x
    {d with env_stack_value_ptr=d.env_stack_value_ptr+1}

let push_type_var x (d: LangEnv) =
    d.env_stack_type.[d.env_stack_type_ptr] <- x
    {d with env_stack_type_ptr=d.env_stack_type_ptr+1}

let seq_apply (d: LangEnv) end_dat =
    let inline end_ () = d.seq.Add(TyLocalReturnData(end_dat,d.trace))
    if d.seq.Count > 0 then
        match d.seq.[d.seq.Count-1] with
        | TyLet(end_dat',a,b) when Object.ReferenceEquals(end_dat,end_dat') -> d.seq.[d.seq.Count-1] <- TyLocalReturnOp(a,b)
        | _ -> end_()
    else end_()
    d.seq.ToArray()

let cse_tryfind (d: LangEnv) key =
    d.cse |> List.tryPick (fun x ->
        match x.TryGetValue key with
        | true, v -> Some v
        | _ -> None
        )

let cse_add (d: LangEnv) k v = (List.head d.cse).Add(k,v)

let push_typedop d op ret_ty =
    let ret = ty_to_data d.i ret_ty
    d.seq.Add(TyLet(ret,d.trace,op))
    ret

let push_op_no_rewrite' (d: LangEnv) op l ret_ty = push_typedop d (TyOp(op,l)) ret_ty
let push_op_no_rewrite d op a ret_ty = push_op_no_rewrite' d op [|a|] ret_ty
let push_binop_no_rewrite d op (a,b) ret_ty = push_op_no_rewrite' d op [|a;b|] ret_ty
let push_triop_no_rewrite d op (a,b,c) ret_ty = push_op_no_rewrite' d op [|a;b;c|] ret_ty

let push_op' (d: LangEnv) op l ret_ty =
    let key = op,l
    match cse_tryfind d key with
    | Some x -> x
    | None ->
        let x = ty_to_data d.i ret_ty
        d.seq.Add(TyLet(x,d.trace,TyOp(op,l)))
        cse_add d key x
        x

let push_op d op a ret_ty = push_op' d op [|a|] ret_ty
let push_binop d op (a,b) ret_ty = push_op' d op [|a;b|] ret_ty
let push_triop d op (a,b,c) ret_ty = push_op' d op [|a;b;c|] ret_ty

let rjp_to_none (d: LangEnv) = function
    | TyV(T(_,RJPT(_,l))) as v ->
        let key = RJPToNone,[|v|]
        match cse_tryfind d key with
        | Some x -> x
        | None ->
            let x = rdata_to_data d.i l 
            d.seq.Add(TyLet(x,d.trace,TyOp key))
            cse_add d key x
            x
    | a -> a

let rjp_to_some layout dict (d: LangEnv) = function
    | TyV(T(_,RJPT(t,l))) as x when t = layout -> x
    | x ->
        let x = rjp_to_none d x
        let op = match layout with RJPStack -> RJPToStack | RJPHeap -> RJPToHeap
        let key = op,[|x|]
        match cse_tryfind d key with
        | Some x -> x
        | None ->
            let ret = ty_to_data d.i (RJPT(layout,data_to_rdata dict x))
            d.seq.Add(TyLet(ret,d.trace,TyOp(op,[|x;ret|]))) // The codegen needs the return var to print it.
            cse_add d key ret
            ret

exception TypeError of Trace * string
let raise_type_error (d: LangEnv) x = raise (TypeError(d.trace,x))

let show_primt = function
    | UInt8T -> "uint8"
    | UInt16T -> "uint16"
    | UInt32T -> "uint32"
    | UInt64T -> "uint64"
    | Int8T -> "int8"
    | Int16T -> "int16"
    | Int32T -> "int32"
    | Int64T -> "int64"
    | Float32T -> "float32"
    | Float64T -> "float64"
    | BoolT -> "bool"
    | StringT -> "string"
    | CharT -> "char"

let show_lit = function
    | LitUInt8 x -> sprintf "%iu8" x
    | LitUInt16 x -> sprintf "%iu16" x
    | LitUInt32 x -> sprintf "%iu32" x
    | LitUInt64 x -> sprintf "%iu64" x
    | LitInt8 x -> sprintf "%ii8" x
    | LitInt16 x -> sprintf "%ii16" x
    | LitInt32 x -> sprintf "%ii32" x
    | LitInt64 x -> sprintf "%ii64" x
    | LitFloat32 x -> sprintf "%ff32" x
    | LitFloat64 x -> sprintf "%ff64" x
    | LitBool x -> sprintf "%b" x
    | LitString x -> sprintf "%s" x
    | LitChar x -> sprintf "%c" x

let show_layout_type = function
    | RJPStack -> "stack"
    | RJPHeap -> "heap"

let inline show_keyword (keywords: KeywordEnv) show (keyword,l: _[]) =
    if l.Length > 0 then
        let a = (keywords.From keyword).Split([|':'|], StringSplitOptions.RemoveEmptyEntries)
        Array.map2 (fun a l -> String.concat "" [|a;":(";show l;")"|]) a l
        |> String.concat " "
    else
        keywords.From keyword

let inline show_map (keywords: KeywordEnv) show v = 
    let body = 
        Map.toArray v
        |> Array.map (fun (k,v) -> sprintf "%s=%s" (keywords.From k) (show v))
        |> String.concat "; "

    sprintf "{%s}" body

let rec show_ty (keywords: KeywordEnv) x = 
    let f x = show_ty keywords x
    match x with
    | BT -> "()"
    | YPrim x -> show_primt x
    | KeywordT(keyword,l) -> show_keyword keywords f (keyword,l)
    | PairT(a, b) -> sprintf "(%s * %s)" (f a) (f b)
    | RecordT v -> show_map keywords f v
    | FunctionT(_,_,_,_,_,false) -> "<function>"
    | FunctionT(_,_,_,_,_,true) -> "<forall>"
    | TypeFunctionT(_,_,_) -> "<type function>" // TODO: Do proper printing for this case.
    | RJPT (layout_type,body) -> sprintf "%s (%s)" (show_layout_type layout_type) (show_typed_data keywords (rdata_to_data (ref 0) body))
    | RuntimeFunctionT (a,b) -> sprintf "(%s -> %s)" (f a) (f b)
    | ArrayT x -> sprintf "array %s" (f x)
    | MacroT x -> show_typed_data keywords (rdata_to_data (ref 0) x) |> sprintf "macro (%s)"

and show_typed_data (keywords: KeywordEnv) (x: Data) =
    let f x = show_typed_data keywords x
    match x with
    | TyB -> "()"
    | TyV(T(_,t)) -> sprintf "var (%s)" (show_ty keywords t)
    | TyKeyword(keyword,l) -> show_keyword keywords f (keyword,l)
    | TyPair(a,b) -> sprintf "(%s, %s)" (f a) (f b)
    | TyRecord a -> show_map keywords f a
    | TyFunction(_,_,_,_,_,false) -> "<function>"
    | TyFunction(_,_,_,_,_,true) -> "<forall>"
    | DLit v -> sprintf "lit %s" (show_lit v)

let is_numeric = function
    | YPrim (UInt8T | UInt16T | UInt32T | UInt64T 
        | Int8T | Int16T | Int32T | Int64T 
        | Float32T | Float64T) -> true
    | _ -> false

let is_signed_numeric = function
    | YPrim (Int8T | Int16T | Int32T | Int64T | Float32T | Float64T) -> true
    | _ -> false

let is_non_float_primitive = function
    | YPrim (Float32T | Float64T) -> false
    | YPrim _ -> true
    | _ -> false

let is_primitive = function
    | YPrim _ -> true
    | _ -> false

let is_string = function
    | YPrim StringT -> true
    | _ -> false

let is_char = function
    | YPrim CharT -> true
    | _ -> false

let is_primt = function
    | YPrim x -> true
    | _ -> false

let is_float = function
    | YPrim (Float32T | Float64T) -> true
    | _ -> false

let is_bool = function
    | YPrim BoolT -> true
    | _ -> false

let is_int = function
    | YPrim (UInt32T | UInt64T | Int32T | Int64T) -> true
    | _ -> false

let is_any_int = function
    | YPrim (UInt8T | UInt16T | UInt32T | UInt64T 
        | Int8T | Int16T | Int32T | Int64T) -> true
    | _ -> false

let is_int64 = function
    | YPrim Int64T -> true
    | _ -> false

let is_int32 = function
    | YPrim Int32T -> true
    | _ -> false

let is_lit_zero = function
    | DLit a ->
        match a with
        | LitInt8 0y | LitInt16 0s | LitInt32 0 | LitInt64 0L
        | LitUInt8 0uy | LitUInt16 0us | LitUInt32 0u | LitUInt64 0UL
        | LitFloat32 0.0f | LitFloat64 0.0 -> true
        | _ -> false
    | _ -> false

let is_lit_one = function
    | DLit a ->
        match a with
        | LitInt8 1y | LitInt16 1s | LitInt32 1 | LitInt64 1L
        | LitUInt8 1uy | LitUInt16 1us | LitUInt32 1u | LitUInt64 1UL
        | LitFloat32 1.0f | LitFloat64 1.0 -> true
        | _ -> false
    | _ -> false

let is_int_lit_zero = function
    | DLit a ->
        match a with
        | LitInt8 0y | LitInt16 0s | LitInt32 0 | LitInt64 0L
        | LitUInt8 0uy | LitUInt16 0us | LitUInt32 0u | LitUInt64 0UL -> true
        | _ -> false
    | _ -> false

let is_int_lit_one = function
    | DLit a ->
        match a with
        | LitInt8 1y | LitInt16 1s | LitInt32 1 | LitInt64 1L
        | LitUInt8 1uy | LitUInt16 1us | LitUInt32 1u | LitUInt64 1UL -> true
        | _ -> false
    | _ -> false

let lit_zero k d = function
    | YPrim Int8T -> LitInt8 0y
    | YPrim Int16T -> LitInt16 0s
    | YPrim Int32T -> LitInt32 0
    | YPrim Int64T -> LitInt64 0L
    | YPrim UInt8T -> LitUInt8 0uy
    | YPrim UInt16T -> LitUInt16 0us
    | YPrim UInt32T -> LitUInt32 0u
    | YPrim UInt64T -> LitUInt64 0UL
    | YPrim Float32T -> LitFloat32 0.0f
    | YPrim Float64T -> LitFloat64 0.0
    | ty -> raise_type_error d <| sprintf "Compiler error: Expected a numeric value in value_zero.\nGot: %s" (show_ty k ty)

let rec partial_eval_type (dex: ExternalLangEnv) d x =
    let ev' d x = partial_eval_type dex d x
    let ev d x =
        match ev' d x with
        | TypeFunctionT _ -> raise_type_error d "The type expression must not evaluate to a type function."
        | x -> x
            
    let inline vt x = if x < 0 then d.env_global_type.[-x-1] else d.env_stack_type.[x]
    match x with
    | TV x -> vt x
    | TPair (a,b) -> PairT(ev d a, ev d b)
    | TFun (a,b) -> RuntimeFunctionT(ev d a, ev d b)
    | TRecord l -> RecordT(Map.map (fun k -> ev d) l)
    | TKeyword (k,l) -> KeywordT(k,Array.map (ev d) l)
    | TApply(l,r) ->
        match ev' d l with
        | RecordT x ->
            match ev' d r with
            | KeywordT(k,_) ->
                match Map.tryFind k x with
                | Some x -> x
                | None -> raise_type_error d <| sprintf "Cannot find the member %s inside the type level record." (dex.keywords.From k)
            | r -> raise_type_error d <| sprintf "Expected a keyword.\nGot: %s" (show_ty dex.keywords r)
        | TypeFunctionT(a,b,c) ->
            let d =
                {d with
                    env_global_type=c
                    env_stack_type=Array.zeroCreate b
                    env_stack_type_ptr=0}
            ev' (push_type_var (ev' d r) d) a
        | l -> raise_type_error d <| sprintf "Expected a type level function as the first argument in type application.Got: %s" (show_ty dex.keywords l)
    | TInl(a,b) -> TypeFunctionT(a,b.stack_size,Array.map vt b.free_vars)
    | TUnit -> BT
    | TPrim x -> YPrim x
    | TArray x -> ArrayT (ev d x)
    | TModule x -> memoize dex.memoized_modules_type (fun _ -> RecordT(Map.map (fun k -> ev d) x)) x
    | TPos x -> ev {d with trace = x.Pos :: d.trace} x.Expression

and partial_eval_value (dex: ExternalLangEnv) (s: LangEnv) x = 
    let inline show_typed_data x = show_typed_data dex.keywords x
    let inline show_ty x = show_ty dex.keywords x

    let inline tev d x = partial_eval_type dex d x
    let inline ev d x = partial_eval_value dex d x
    let inline term2 d a b = ev d a, ev d b
    let inline ev3 d a b c = ev d a, ev d b, ev d c
    let inline ev_seq d x =
        let d = {d with seq=ResizeArray(); i=ref !d.i}
        let x = ev d x
        let x_ty = data_to_ty x
        seq_apply d x, x_ty
    let inline ev_branch d x = ev_seq {d with cse=Dictionary(HashIdentity.Structural) :: d.cse} x
    
    let inline push_var_ptrt ptr x = s.env_stack_type.[ptr] <- x; ptr+1
    let inline push_var_ptr ptr x = s.env_stack_value.[ptr] <- x; ptr+1
    let inline vt x = if x < 0 then s.env_global_type.[-x-1] else s.env_stack_type.[x]
    let inline v x = if x < 0 then s.env_global_value.[-x-1] else s.env_stack_value.[x]

    let inline nan_guardf32 x = if Single.IsNaN x then raise_type_error s "A 32-bit floating point operation resulting in a nan detected at compile time. Spiral cannot propagate nan literal without diverging." else x
    let inline nan_guardf64 x = if Double.IsNaN x then raise_type_error s "A 64-bit floating point operation resulting in a nan detected at compile time. Spiral cannot propagate nan literal without diverging." else x

    let func_env d (t_sz,t_env,v_sz,v_env) =
        {d with 
            env_global_type=t_env
            env_global_value=v_env
            env_stack_type=Array.zeroCreate t_sz
            env_stack_type_ptr=0
            env_stack_value=Array.zeroCreate v_sz
            env_stack_value_ptr=0
            }

    let rec apply' d a b =
        match a,b with
        | TyFunction(body,t_sz,t_env,v_sz,v_env,false), b ->
            let d = func_env d (t_sz,t_env,v_sz,v_env)
            ev (push_value_var b d) body
        | TyRecord l, TyKeyword(keyword,_) ->
            match Map.tryFind keyword l with
            | Some a -> a
            | None -> raise_type_error d <| sprintf "The record does not have the field %s." (dex.keywords.From keyword)
        | TyRecord _, b -> raise_type_error d <| sprintf "The second argument in a record application is not a keyword.\nGot: %s" (show_typed_data b)
        | TyV(T(_,RJPT _)) & a, b -> apply' d (rjp_to_none d a) b
        | TyV(T(_,RuntimeFunctionT(clo_arg_ty,clo_ret_ty))) & a, b -> 
            let b_ty = data_to_ty b
            if clo_arg_ty <> b_ty then raise_type_error d <| sprintf "Cannot apply an argument of type %s to a function of type: %s -> %s" (show_ty b_ty) (show_ty clo_arg_ty) (show_ty clo_ret_ty)
            else push_binop_no_rewrite d Apply (a, b) clo_ret_ty
        | a, _ -> raise_type_error d <| sprintf "The first argument provided cannot be applied.\nGot: %s" (show_typed_data a)


    let apply d a b = 
        match ev d a with
        | TyFunction(body,t_sz,t_env,v_sz,v_env,true) ->
            match b with
            | Type b -> 
                let d = func_env d (t_sz,t_env,v_sz,v_env)
                ev (push_type_var (tev d b) d) body
            | _ -> raise_type_error d <| sprintf "Expected a type as the second argument in the forall application."
        | a -> apply' d a (ev d b)

    let function_to_runtime_function env (a : Expr, b : StackSize, c : Ty [], d : StackSize, e : Data []) (a' : Ty, b' : Ty) =
        raise_type_error env "function_to_runtime_function not implemented."

    let rec box' d (a,b) = 
        let box (a,b) = box' d (a,b)
        let fail a b = raise_type_error d <| sprintf "Cannot box %s into %s" (show_typed_data a) (show_ty b)
        match a, b with
        | TyB, BT -> a
        | TyPair(a,b), PairT(a',b') -> TyPair(box (a, a'), box (b, b'))
        | TyKeyword(k,l), KeywordT(k',l') -> TyKeyword(k,Array.map2 (fun a b -> box (a,b)) l l')
        | TyFunction(a,b,c,d',e,false), RuntimeFunctionT(a',b') -> function_to_runtime_function d (a,b,c,d',e) (a',b')
        | TyFunction(_,_,_,_,_,true), _ -> raise_type_error d <| sprintf "Cannot box foralls.\nGot: %s" (show_typed_data a)
        | TyRecord l & a, RecordT l' -> 
            if Map.count l = Map.count l' then
                TyRecord(
                    Map.map (fun k v -> 
                        match Map.tryFind k l' with
                        | Some v' -> box (v, v')
                        | None -> fail a b
                        ) l)
            else fail a b
        | DLit x, YPrim x' -> if lit_to_primitive_type x = x' then push_op_no_rewrite d Dynamize a b else fail a b
        | TyV(T(_,x)), x' -> if x = x' then a else fail a b
        | a,b -> fail a b

    let function' is_forall on_succ (data : ExprData) = 
        TyFunction(on_succ,
            data.type'.stack_size,Array.zeroCreate<_> data.type'.free_vars.Length,
            data.value.stack_size,Array.map v data.value.free_vars,
            is_forall)

    let rec if_ cond on_succ on_fail = 
        match cond with
        | DLit (LitBool true) -> ev s on_succ
        | DLit (LitBool false) -> ev s on_fail
        | cond ->
            match data_to_ty cond with
            | YPrim BoolT as type_bool ->
                let lit_tr = DLit(LitBool true)
                match cse_tryfind s (EQ, [|cond; lit_tr|]) with
                | Some cond -> if_ cond on_succ on_fail
                | None ->
                    let lit_fl = DLit(LitBool false)
                    let add_rewrite_cases is_true = 
                        let cse = Dictionary(HashIdentity.Structural)
                        let tr,fl = if is_true then lit_tr, lit_fl else lit_fl, lit_tr
                        let inline op op cond' res = cse.Add((op,[|cond;cond'|]),res); cse.Add((op,[|cond';cond|]),res)
                        op EQ lit_tr tr; op NEQ lit_tr fl; op EQ lit_fl fl; op NEQ lit_fl tr
                        cse
                    let tr, type_tr = ev_seq {s with cse = add_rewrite_cases true :: s.cse} on_succ
                    let fl, type_fl = ev_seq {s with cse = add_rewrite_cases false :: s.cse} on_fail
                    if type_tr = type_fl then
                        if tr.Length = 1 && fl.Length = 1 then
                            match tr.[0], fl.[0] with
                            | TyLocalReturnOp(_,tr), TyLocalReturnOp(_,fl) when tr = fl -> push_typedop s tr type_tr
                            | TyLocalReturnData(tr',_), TyLocalReturnData(fl',_) -> 
                                match tr', fl' with
                                | tr, fl when tr = fl -> tr
                                | DLit(LitBool false), DLit(LitBool true) -> push_binop s EQ (cond,lit_fl) type_bool
                                | DLit(LitBool false), fl when cond = fl -> lit_fl
                                | DLit(LitBool true), fl -> // boolean or
                                    match fl with
                                    | DLit (LitBool false) -> cond
                                    | _ -> if cond = fl then cond else push_binop s BoolOr (cond,fl) type_bool
                                | tr, DLit(LitBool false) -> // boolean and
                                    match tr with
                                    | DLit(LitBool true) -> cond
                                    | _ -> if cond = tr then cond else push_binop s BoolAnd (cond,tr) type_bool
                                | _ -> push_typedop s (TyIf(cond,tr,fl)) type_tr
                            | _ -> push_typedop s (TyIf(cond,tr,fl)) type_tr
                        else push_typedop s (TyIf(cond,tr,fl)) type_tr
                    else raise_type_error s <| sprintf "Types in branches of If do not match.\nGot: %s and %s" (show_ty type_tr) (show_ty type_fl)
            | cond_ty -> raise_type_error s <| sprintf "Expected a bool in conditional.\nGot: %s" (show_ty cond_ty)

    let eq d a b = 
        let inline op a b = a = b
        match a,b with
        | DLit a, DLit b ->
            match a, b with
            | LitInt8 a, LitInt8 b -> op a b |> LitBool |> DLit
            | LitInt16 a, LitInt16 b -> op a b |> LitBool |> DLit
            | LitInt32 a, LitInt32 b -> op a b |> LitBool |> DLit
            | LitInt64 a, LitInt64 b -> op a b |> LitBool |> DLit
            | LitUInt8 a, LitUInt8 b -> op a b |> LitBool |> DLit
            | LitUInt16 a, LitUInt16 b -> op a b |> LitBool |> DLit
            | LitUInt32 a, LitUInt32 b -> op a b |> LitBool |> DLit
            | LitUInt64 a, LitUInt64 b -> op a b |> LitBool |> DLit
            | LitFloat32 a, LitFloat32 b -> op a b |> LitBool |> DLit
            | LitFloat64 a, LitFloat64 b -> op a b |> LitBool |> DLit
            | LitString a, LitString b -> op a b |> LitBool |> DLit
            | LitChar a, LitChar b -> op a b |> LitBool |> DLit
            | LitBool a, LitBool b -> op a b |> LitBool |> DLit
            | a, b -> raise_type_error d <| sprintf "The two literals must be equal in type.\nGot: %s and %s" (show_lit a) (show_lit b)
        | TyV(T(a,a_ty)), TyV(T(b,_)) when a = b && is_non_float_primitive a_ty -> LitBool true |> DLit
        | a, b ->
            let a_ty, b_ty = data_to_ty a, data_to_ty b 
            if a_ty = b_ty then
                if is_primitive a_ty then push_binop d EQ (a,b) (YPrim BoolT)
                else raise_type_error d <| sprintf "The type of the two arguments needs to be a primitive type.\nGot: %s" (show_ty a_ty)
            else
                raise_type_error d <| sprintf "The two sides need to have the same primitive types.\nGot: %s and %s." (show_ty a_ty) (show_ty b_ty)            

    match x with
    | UnionTest _ -> raise_type_error s "UnionTest not yet implemented." // TODO
    | B -> TyB
    | V x -> v x
    | Lit x -> DLit x
    | KeywordCreate(k,l) -> TyKeyword(k,Array.map (ev s) l)
    | Inline(on_succ,data) ->
        let d = 
            {s with 
                env_global_type=Array.map vt data.type'.free_vars
                env_global_value=Array.map v data.value.free_vars
                env_stack_type=Array.zeroCreate data.type'.stack_size
                env_stack_type_ptr=0
                env_stack_value=Array.zeroCreate data.value.stack_size
                env_stack_value_ptr=0
                }
        ev d on_succ
    | Inl(on_succ,data) -> function' false on_succ data
    | Forall(on_succ,data) -> function' true on_succ data
    | Glob(e) -> ev s !e
    | Let(bind,on_succ) -> ev (push_value_var (ev s bind) s) on_succ
    | RecBlock(l,on_succ) ->
        let l,d =
            let function' is_forall on_succ (data : ExprData) = 
                TyFunction(on_succ,
                    data.type'.stack_size,Array.zeroCreate<_> data.type'.free_vars.Length,
                    data.value.stack_size,Array.zeroCreate<_> data.value.free_vars.Length,
                    is_forall)
            Array.mapFold (fun d x -> 
                let data,x = 
                    match x with
                    | Inl(on_succ,data) -> data,function' false on_succ data
                    | Forall(on_succ,data) -> data,function' true on_succ data
                    | _ -> raise_type_error d "Compiler error: Expected a inl or a forall in the recursive block."
                (data,x), push_value_var x d
                ) s l
        l |> Array.iter (function 
            | data,TyFunction(_,_,c,_,e,_) ->
                data.type'.free_vars |> Array.iteri (fun i x -> c.[i] <- vt x) // Don't mind the lack of d being passed to vt and v.
                data.value.free_vars |> Array.iteri (fun i x -> e.[i] <- v x)
            | _ -> failwith "impossible"
            )
        ev d on_succ
    | LitTest(a,b,on_succ,on_fail) -> 
        let b = v b
        if lit_to_ty a = data_to_ty b then if_ (eq s (DLit a) b) on_succ on_fail
        else ev s on_fail
    | PairTest(x,on_succ,on_fail) ->
        match v x with
        | TyPair(a,b) -> ev (push_value_var a s |> push_value_var b) on_succ
        | _ -> ev s on_fail
    | KeywordTest(keyword, bind, on_succ, on_fail) ->
        match v bind with
        | TyKeyword(keyword', l) when keyword = keyword' -> ev {s with env_stack_value_ptr=Array.fold push_var_ptr s.env_stack_value_ptr l} on_succ
        | _ -> ev s on_fail
    | RecordTest(pats, bind, on_succ, on_fail) ->
        let inline on_fail () = ev s on_fail
        match v bind with
        | TyRecord l -> 
            let rec loop d i =
                let inline case keyword =
                    match l.TryFind keyword with
                    | Some x -> loop (push_value_var x d) (i+1)
                    | None -> on_fail()
                if i < pats.Length then
                    match pats.[i] with
                    | RecordTestKeyword keyword -> case keyword
                    | RecordTestInjectVar var ->
                        match ev d var with
                        | TyKeyword(keyword,_) -> case keyword
                        | _ -> raise_type_error d "The injected variable needs to an unary keyword."
                else ev d on_succ
            loop s 0
        | _ -> on_fail()
    | RecordWith(vars, withs) ->
        let withs l =
            let push_keyword keyword =
                match Map.tryFind keyword l with
                | Some this -> push_value_var this s
                | None -> push_value_var TyB s
            let var_to_keyword var_string var = 
                match ev s var with
                | TyKeyword(keyword,_) -> keyword
                | _ -> raise_type_error s <| sprintf "The injected variable %s in RecordWith is not a keyword." var_string
            Array.fold (fun l -> function
                | RecordWithKeyword(keyword,expr) -> Map.add keyword (ev (push_keyword keyword) expr) l
                | RecordWithInjectVar(var_string,var,expr) -> let keyword = var_to_keyword var_string var in Map.add keyword (ev (push_keyword keyword) expr) l
                | RecordWithoutKeyword keyword -> Map.remove keyword l
                | RecordWithoutInjectVar(var_string,var) -> Map.remove (var_to_keyword var_string var) l
                ) l withs
            |> TyRecord
        match vars with
        | [||] -> withs Map.empty
        | _ -> 
            match ev s vars.[0] with
            | TyRecord l -> 
                let rec loop l i =
                    if i < vars.Length then
                        let th = function 1 -> "st" | 2 -> "nd" | 3 -> "rd" | _ -> "th"
                        match ev s vars.[i] with
                        | TyKeyword(keyword,_) ->
                            match Map.tryFind keyword l with
                            | Some(TyRecord l') -> Map.add keyword (loop l' (i+1)) l |> TyRecord
                            | Some _ -> raise_type_error s <| sprintf "The %i%s variable application in RecordWith does not result in a record." i (th i)
                            | None -> raise_type_error s <| sprintf "The keyword %s cannot be found in the %i%s sub-record." (dex.keywords.From keyword) i (th i)
                        | _ -> let i = i+1 in raise_type_error s <| sprintf "The %i%s variable in RecordWith is not a keyword." i (th i)
                    else withs l
                loop l 1
            | _ -> raise_type_error s "The first variable must be a record."
    | Module l -> memoize dex.memoized_modules_value (fun _ -> TyRecord(Map.map (fun k -> ev s) l)) l
    | Type _ -> raise_type_error s "Types should only appear as a part of the application to foralls."
    | UnitTest(a,on_succ,on_fail)-> 
        match v a with
        | TyB -> ev s on_succ
        | _ -> ev s on_fail
    | Typecase(a,l) ->
        let f x = tev s x
        let a = f a
        match Array.tryPick (fun (b,body) -> if a = f b then Some (ev s body) else None) l with
        | Some x -> x
        | None -> raise_type_error s <| sprintf "Typecase miss for %s" (show_ty a)
    | Pos(pos) -> ev {s with trace=pos.Pos :: s.trace} pos.Expression
    | AnnotTest(do_boxing,ty,expr,on_succ,on_fail) ->
        let a = v expr
        let b = tev s ty
        if do_boxing then ev (push_value_var (box' s (a,b)) s) on_succ
        else if data_to_ty a = b then ev s on_succ else ev s on_fail
    | Annot(tb,a') ->
        let a = ev s a'
        let tb = tev s tb
        let ta = data_to_ty a
        if ta = tb then a else 
            let d = match a' with Pos x -> {s with trace = x.Pos :: s.trace} | _ -> s
            raise_type_error d <| sprintf "Type annotation mismatch.\nReturn type: %s\nAnnotation : %s\n" (show_ty ta) (show_ty tb)
    | JoinPoint(ret_ty',body) ->
        // Note: All the join points must be wrapped in an Inline so that their local environments are empty and all used free vars are in the globals.
        let call_args, env_global_value = data_to_rdata'' dex.hc_table s.env_global_value
        let join_point_key = dex.hc_table.Add(env_global_value, s.env_global_type)
           
        let ret_ty = 
            let dict = memoize dex.join_point_method (fun _ -> Dictionary(HashIdentity.Structural)) body
            match dict.TryGetValue join_point_key with
            | false, _ ->
                dict.[join_point_key] <- None
                let seq,ret_ty = ev_seq {s with cse=[Dictionary(HashIdentity.Structural)]} body
                dict.[join_point_key] <- Some (call_args,seq,ret_ty)
                match ret_ty' with
                | Some x -> let annot = tev s x in if ret_ty <> annot then raise_type_error s "Join point type annotation mismatch.\nReturn type: %s\nAnnotation : %s\n" (show_ty ret_ty) (show_ty annot)
                | None -> ()
                ret_ty
            | true, None -> 
                match ret_ty' with
                | Some x -> tev s x
                | None -> raise_type_error s <| sprintf "The recursive join point requires an annotation."
            | true, Some (_,_,x) -> x

        push_typedop s (TyJoinPoint(JPMethod(body,join_point_key),call_args)) ret_ty
    | Op(Apply,[|a;b|]) -> apply s a b
    | Op(RJPToStack,[|a|]) -> rjp_to_some RJPStack dex.hc_table s (ev s a)
    | Op(RJPToHeap,[|a|]) -> rjp_to_some RJPHeap dex.hc_table s (ev s a)
    | Op(RJPToNone,[|a|]) -> rjp_to_none s (ev s a)
    | Op(If,[|cond;on_succ;on_fail|]) -> if_ (ev s cond) on_succ on_fail
    | Op(While,[|cond;on_succ|]) ->
        match ev_seq s cond with
        | [|TyLocalReturnOp(_,TyJoinPoint cond)|], ty ->
            match ty with
            | YPrim BoolT -> 
                match ev_branch s on_succ with
                | on_succ, BT & ty -> push_typedop s (TyWhile(cond,on_succ)) ty
                | _, ty -> raise_type_error s <| sprintf "The body of the while loop must be of type unit.\nGot: %s" (show_ty ty)
            | _ -> raise_type_error s <| sprintf "The conditional of the while loop must be of type bool.\nGot: %s" (show_ty ty)
        | _ -> raise_type_error s "Compiler error: The body of the conditional of the while loop must be a solitary join point."
    | Op(Dynamize,[|a|]) ->
        let m = Dictionary(HashIdentity.Reference)
        let rec f x = 
            memoize m (function
                | TyB as x -> x
                | TyPair(a,b) -> TyPair(f a, f b)
                | TyKeyword(a,b) -> (a,Array.map f b) |> TyKeyword
                | TyFunction(a,b,c,d,e,z) -> TyFunction(a,b,c,d,Array.map f e,z)
                | TyRecord l -> Map.map (fun _ -> f) l |> TyRecord
                | TyV _ as x -> x
                | DLit v as x -> push_op_no_rewrite s Dynamize x (lit_to_ty v)
                ) x
            
        f (ev s a)
    | Op(StringSlice, [|a;b;c|]) ->
        match ev3 s a b c with
        | DLit(LitString a), DLit(LitInt64 b), DLit(LitInt64 c) ->
            if int b >= 0 && int c < a.Length then a.[(int b)..(int c)] |> LitString |> DLit
            else raise_type_error s <| sprintf "String slice out of bounds. length: %i from: %i to: %i" a.Length b c
        | TyType(YPrim StringT) & a, TyType(YPrim Int64T) & b, TyType(YPrim Int64T) & c -> push_triop s StringSlice (a,b,c) (YPrim StringT)
        | a,b,c -> raise_type_error s <| sprintf "Expected a string and two int64s as arguments to StringSlice.\nstring: %s\nfrom: %s\nto: %s" (show_typed_data a) (show_typed_data b) (show_typed_data c)
    | Op(StringIndex, [|a;b|]) ->
        match term2 s a b with
        | DLit(LitString a), DLit(LitInt64 b) ->
            if int b >= 0 && int b < a.Length then a.[int b] |> LitChar |> DLit
            else raise_type_error s <| sprintf "String index out of bounds. length: %i index: %i" a.Length b
        | TyType(YPrim StringT) & a, TyType(YPrim Int64T) & b -> push_binop s StringIndex (a,b) (YPrim CharT)
        | a,b -> raise_type_error s <| sprintf "Expected a string and an int64 as arguments to StringIndex.\nstring: %s\ni: %s" (show_typed_data a) (show_typed_data b)
    | Op(StringLength, [|a|]) ->
        match ev s a with
        | DLit (LitString str) -> DLit (LitInt64 (int64 str.Length))
        | TyType(YPrim StringT) & str -> push_op s StringLength str (YPrim Int64T)
        | x -> raise_type_error s <| sprintf "Expected a string.\nGot: %s" (show_typed_data x)
    | Op(UnsafeConvert,[|Type tot;from|]) ->
        let tot = tev s tot
        let from = ev s from
        let fromt = data_to_ty from
        if tot = fromt then from
        else
            let inline conv_lit x =
                match tot with
                | YPrim Int8T -> int8 x |> LitInt8
                | YPrim Int16T -> int16 x |> LitInt16
                | YPrim Int32T -> int32 x |> LitInt32
                | YPrim Int64T -> int64 x |> LitInt64
                | YPrim UInt8T -> uint8 x |> LitUInt8
                | YPrim UInt16T -> uint16 x |> LitUInt16
                | YPrim UInt32T -> uint32 x |> LitUInt32
                | YPrim UInt64T -> uint64 x |> LitUInt64
                | YPrim CharT -> char x |> LitChar
                | YPrim Float32T -> float32 x |> LitFloat32
                | YPrim Float64T -> float x |> LitFloat64
                | YPrim StringT -> string x |> LitString
                | _ -> raise_type_error s <| sprintf "Cannot convert the literal to the following type: %s" (show_ty tot)
                |> DLit
            match from with
            | DLit (LitInt8 a) -> conv_lit a
            | DLit (LitInt16 a) -> conv_lit a
            | DLit (LitInt32 a) -> conv_lit a
            | DLit (LitInt64 a) -> conv_lit a
            | DLit (LitUInt8 a) -> conv_lit a
            | DLit (LitUInt16 a) -> conv_lit a
            | DLit (LitUInt32 a) -> conv_lit a
            | DLit (LitUInt64 a) -> conv_lit a
            | DLit (LitChar a) -> conv_lit a
            | DLit (LitFloat32 a) -> conv_lit a
            | DLit (LitFloat64 a) -> conv_lit a
            | DLit (LitString a) -> conv_lit a
            | DLit (LitBool _) -> raise_type_error s "Cannot convert the bool to any type."
            | _ ->
                let is_convertible_primt x =
                    match x with
                    | YPrim BoolT | YPrim StringT -> false
                    | YPrim _ -> true
                    | _ -> false
                if is_convertible_primt fromt && is_convertible_primt tot then 
                    let ret = ty_to_data s.i tot
                    s.seq.Add(TyLet(ret,s.trace,TyOp(UnsafeConvert,[|ret;from|])))
                    ret
                else raise_type_error s <| sprintf "Cannot convert %s to the following type: %s" (show_typed_data from) (show_ty tot)
    | Op(PrintStatic,[|a|]) -> printfn "%s" (ev s a |> show_typed_data); TyB
    | Op(RecordMap,[|a;b|]) ->
        match term2 s a b with
        | a, TyRecord l ->
            let kv = dex.keywords.To "key:value:"
            Map.map (fun k v -> apply' s a (TyKeyword(kv,[|TyKeyword(k,[||]); v|]))) l
            |> TyRecord
        | _, b -> raise_type_error s <| sprintf "Expected a record.\nGot: %s" (show_typed_data b)
    | Op(RecordFilter,[|a;b|]) ->
        match term2 s a b with
        | a, TyRecord l ->
            let kv = dex.keywords.To "key:value:"
            Map.filter (fun k v ->
                match apply' s a (TyKeyword(kv,[|TyKeyword(k,[||]); v|])) with
                | DLit(LitBool x) -> x
                | x -> raise_type_error s <| sprintf "Expected a bool literal in ModuleFilter.\nGot: %s" (show_typed_data x)
                ) l
            |> TyRecord
        | _, b -> raise_type_error s <| sprintf "Expected a record.\nGot: %s" (show_typed_data b)
    | Op(RecordFoldL,[|a;b;c|]) ->
        match ev3 s a b c with
        | a, state, TyRecord l -> 
            let kv = dex.keywords.To "state:key:value:"
            Map.fold (fun state k v -> apply' s a (TyKeyword(kv,[|state; TyKeyword(k,[||]); v|]))) state l
        | _, _, r -> raise_type_error s <| sprintf "Expected a record.\nGot: %s" (show_typed_data r)
    | Op(RecordFoldR,[|a;b;c|]) ->
        match ev3 s a b c with
        | a, state, TyRecord l -> 
            let kv = dex.keywords.To "state:key:value:"
            Map.foldBack (fun k x state -> apply' s a (TyKeyword(kv,[|state; TyKeyword(k,[||]); x|]))) l state
        | _, r, _ -> raise_type_error s <| sprintf "Expected a record.\nGot: %s" (show_typed_data r)
    | Op(RecordLength,[|a|]) ->
        match ev s a with
        | TyRecord l -> Map.count l |> int64 |> LitInt64 |> DLit
        | r -> raise_type_error s <| sprintf "Expected a record.\nGot: %s" (show_typed_data r)
    | Op(IsLit,[|a|]) -> 
        match ev s a with
        | DLit _ -> DLit (LitBool true)
        | _ -> DLit (LitBool false)
    | Op(IsPrim,[|a|]) -> 
        match ev s a |> data_to_ty with
        | YPrim _ -> DLit (LitBool true)
        | _ -> DLit (LitBool false)
    | Op(IsRJP,[|a|]) -> 
        match ev s a |> data_to_ty with
        | RJPT _ -> DLit (LitBool true)
        | _ -> DLit (LitBool false)
    | Op(IsKeyword,[|a|]) -> 
        match ev s a with
        | TyKeyword _ -> DLit (LitBool true)
        | _ -> DLit (LitBool false)
    | Op(IsNan,[|a|]) ->
        match ev s a with
        | a & TyType (YPrim (Float32T | Float64T)) -> push_op s IsNan a (YPrim BoolT)
        | x -> raise_type_error s <| sprintf "Expected a float in NanIs. Got: %s" (show_typed_data x)
    | Op(StripKeyword,[|a|]) -> 
        match ev s a with
        | TyKeyword(_,[||]) -> TyB
        | TyKeyword(_,l) -> Array.reduceBack (fun a b -> TyPair(a,b)) l
        | a -> raise_type_error s <| sprintf "Expected a keyword.\nGot: %s" (show_typed_data a)
    | Op(EqType,[|a;b|]) -> data_to_ty (ev s a) = data_to_ty (ev s b) |> LitBool |> DLit
    | Op(ArrayCreate,[|Type a;b|]) ->
        match tev s a, ev s b with
        | a, TyType(YPrim Int64T) & b -> push_op_no_rewrite s ArrayCreate b (ArrayT a)
        | _, b -> raise_type_error s <| sprintf "Expected an int64 as the size of the array.\nGot: %s" (show_typed_data b)
    | Op(ArrayLength,[|a|]) ->
        let a = ev s a
        match data_to_ty a with
        | ArrayT _ -> push_op s ArrayLength a (YPrim Int64T)
        | _ -> raise_type_error s <| sprintf "ArrayLength is only supported for .NET arrays. Got: %s" (show_typed_data a)
    | Op(GetArray,[|a;b|]) ->
        match ev s a with
        | TyType (ArrayT ty) & a ->
            match ev s b with
            | TyType (YPrim Int64T) & b -> push_binop_no_rewrite s GetArray (a,b) ty
            | b -> raise_type_error s <| sprintf "Expected a int64 as the index argumet in GetArray.\nGot: %s" (show_typed_data b)
        | a -> raise_type_error s <| sprintf "Expected an array in GetArray.\nGot: %s" (show_typed_data a)
    | Op(SetArray,[|a;b;c|]) ->
        match ev s a with
        | TyType (ArrayT ty) & a ->
            match ev s b with
            | TyType (YPrim Int64T) & b -> 
                match ev s c with
                | TyType ty' & c -> 
                    if ty' = ty then push_triop_no_rewrite s SetArray (a,b,c) BT
                    else raise_type_error s <| sprintf "The array and the value being set do not have the same type.\nGot: %s\nAnd: %s" (show_ty ty) (show_ty ty')
            | b -> raise_type_error s <| sprintf "Expected a int64 as the index argumet in GetArray.\nGot: %s" (show_typed_data b)
        | a -> raise_type_error s <| sprintf "Expected an array in SetArray.\nGot: %s" (show_typed_data a)
    // Primitive operations on expressions.
    | Op(Add,[|a;b|]) -> 
        let inline op a b = a + b
        match term2 s a b with
        | DLit a, DLit b ->
            match a, b with
            | LitInt8 a, LitInt8 b -> op a b |> LitInt8 |> DLit
            | LitInt16 a, LitInt16 b -> op a b |> LitInt16 |> DLit
            | LitInt32 a, LitInt32 b -> op a b |> LitInt32 |> DLit
            | LitInt64 a, LitInt64 b -> op a b |> LitInt64 |> DLit
            | LitUInt8 a, LitUInt8 b -> op a b |> LitUInt8 |> DLit
            | LitUInt16 a, LitUInt16 b -> op a b |> LitUInt16 |> DLit
            | LitUInt32 a, LitUInt32 b -> op a b |> LitUInt32 |> DLit
            | LitUInt64 a, LitUInt64 b -> op a b |> LitUInt64 |> DLit
            | LitFloat32 a, LitFloat32 b -> op a b |> nan_guardf32  |> LitFloat32 |> DLit
            | LitFloat64 a, LitFloat64 b -> op a b |> nan_guardf64 |> LitFloat64 |> DLit
            | a, b -> raise_type_error s <| sprintf "The two literals must be both numeric and equal in type.\nGot: %s and %s" (show_lit a) (show_lit b)
        | a, b ->
            let a_ty, b_ty = data_to_ty a, data_to_ty b 
            if a_ty = b_ty then
                if is_lit_zero a then b
                elif is_lit_zero b then a
                elif is_numeric a_ty then push_binop s Add (a,b) a_ty
                else raise_type_error s <| sprintf "The type of the two arguments needs to be a numeric type.\nGot: %s" (show_ty a_ty)
            else
                raise_type_error s <| sprintf "The two sides need to have the same numeric types.\nGot: %s and %s." (show_ty a_ty) (show_ty b_ty)
    | Op(Sub,[|a;b|]) ->
        let inline op a b = a - b
        match term2 s a b with
        | DLit a, DLit b ->
            match a, b with
            | LitInt8 a, LitInt8 b -> op a b |> LitInt8 |> DLit
            | LitInt16 a, LitInt16 b -> op a b |> LitInt16 |> DLit
            | LitInt32 a, LitInt32 b -> op a b |> LitInt32 |> DLit
            | LitInt64 a, LitInt64 b -> op a b |> LitInt64 |> DLit
            | LitUInt8 a, LitUInt8 b -> op a b |> LitUInt8 |> DLit
            | LitUInt16 a, LitUInt16 b -> op a b |> LitUInt16 |> DLit
            | LitUInt32 a, LitUInt32 b -> op a b |> LitUInt32 |> DLit
            | LitUInt64 a, LitUInt64 b -> op a b |> LitUInt64 |> DLit
            | LitFloat32 a, LitFloat32 b -> op a b |> nan_guardf32  |> LitFloat32 |> DLit
            | LitFloat64 a, LitFloat64 b -> op a b |> nan_guardf64 |> LitFloat64 |> DLit
            | a, b -> raise_type_error s <| sprintf "The two literals must be both numeric and equal in type.\nGot: %s and %s" (show_lit a) (show_lit b)
        | a, b ->
            let a_ty, b_ty = data_to_ty a, data_to_ty b 
            if a_ty = b_ty then
                if is_lit_zero a then push_op s Neg b b_ty
                elif is_lit_zero b then a
                elif is_numeric a_ty then push_binop s Sub (a,b) a_ty
                else raise_type_error s <| sprintf "The type of the two arguments needs to be a numeric type.\nGot: %s" (show_ty a_ty)
            else
                raise_type_error s <| sprintf "The two sides need to have the same numeric types.\nGot: %s and %s." (show_ty a_ty) (show_ty b_ty)
    | Op(Mult,[|a;b|]) ->
        let inline op a b = a * b
        match term2 s a b with
        | DLit a, DLit b ->
            match a, b with
            | LitInt8 a, LitInt8 b -> op a b |> LitInt8 |> DLit
            | LitInt16 a, LitInt16 b -> op a b |> LitInt16 |> DLit
            | LitInt32 a, LitInt32 b -> op a b |> LitInt32 |> DLit
            | LitInt64 a, LitInt64 b -> op a b |> LitInt64 |> DLit
            | LitUInt8 a, LitUInt8 b -> op a b |> LitUInt8 |> DLit
            | LitUInt16 a, LitUInt16 b -> op a b |> LitUInt16 |> DLit
            | LitUInt32 a, LitUInt32 b -> op a b |> LitUInt32 |> DLit
            | LitUInt64 a, LitUInt64 b -> op a b |> LitUInt64 |> DLit
            | LitFloat32 a, LitFloat32 b -> op a b |> nan_guardf32  |> LitFloat32 |> DLit
            | LitFloat64 a, LitFloat64 b -> op a b |> nan_guardf64 |> LitFloat64 |> DLit
            | a, b -> raise_type_error s <| sprintf "The two literals must be both numeric and equal in type.\nGot: %s and %s" (show_lit a) (show_lit b)
        | a, b ->
            let a_ty, b_ty = data_to_ty a, data_to_ty b 
            if a_ty = b_ty then
                if is_int_lit_zero a || is_int_lit_zero b then lit_zero dex.keywords s a_ty |> DLit
                elif is_lit_one a then b
                elif is_lit_one b then a
                elif is_numeric a_ty then push_binop s Mult (a,b) a_ty
                else raise_type_error s <| sprintf "The type of the two arguments needs to be a numeric type.\nGot: %s" (show_ty a_ty)
            else
                raise_type_error s <| sprintf "The two sides need to have the same numeric types.\nGot: %s and %s." (show_ty a_ty) (show_ty b_ty)
    | Op(Div,[|a;b|]) -> 
        let inline op a b = a / b
        try
            match term2 s a b with
            | DLit a, DLit b ->
                match a, b with
                | LitInt8 a, LitInt8 b -> op a b |> LitInt8 |> DLit
                | LitInt16 a, LitInt16 b -> op a b |> LitInt16 |> DLit
                | LitInt32 a, LitInt32 b -> op a b |> LitInt32 |> DLit
                | LitInt64 a, LitInt64 b -> op a b |> LitInt64 |> DLit
                | LitUInt8 a, LitUInt8 b -> op a b |> LitUInt8 |> DLit
                | LitUInt16 a, LitUInt16 b -> op a b |> LitUInt16 |> DLit
                | LitUInt32 a, LitUInt32 b -> op a b |> LitUInt32 |> DLit
                | LitUInt64 a, LitUInt64 b -> op a b |> LitUInt64 |> DLit
                | LitFloat32 a, LitFloat32 b -> op a b |> nan_guardf32  |> LitFloat32 |> DLit
                | LitFloat64 a, LitFloat64 b -> op a b |> nan_guardf64 |> LitFloat64 |> DLit
                | a, b -> raise_type_error s <| sprintf "The two literals must be both numeric and equal in type.\nGot: %s and %s" (show_lit a) (show_lit b)
            | a, b ->
                let a_ty, b_ty = data_to_ty a, data_to_ty b 
                if a_ty = b_ty then
                    if is_lit_zero b then raise (DivideByZeroException())
                    elif is_lit_one b then a
                    elif is_numeric a_ty then push_binop s Div (a,b) a_ty
                    else raise_type_error s <| sprintf "The type of the two arguments needs to be a numeric type.\nGot: %s" (show_ty a_ty)
                else
                    raise_type_error s <| sprintf "The two sides need to have the same numeric types.\nGot: %s and %s." (show_ty a_ty) (show_ty b_ty)
        with :? DivideByZeroException ->
            raise_type_error s <| sprintf "An attempt to divide by zero has been detected at compile time."
    | Op(Pow,[|a;b|]) -> 
        let inline op a b = a ** b
        match term2 s a b with
        | DLit a, DLit b ->
            match a, b with
            | LitFloat32 a, LitFloat32 b -> op a b |> nan_guardf32 |> LitFloat32 |> DLit
            | LitFloat64 a, LitFloat64 b -> op a b |> nan_guardf64 |> LitFloat64 |> DLit
            | a, b -> raise_type_error s <| sprintf "The two literals must be both float and equal in type.\nGot: %s and %s" (show_lit a) (show_lit b)
        | a, b ->
            let a_ty, b_ty = data_to_ty a, data_to_ty b 
            if a_ty = b_ty then push_binop s Pow (a,b) a_ty
            else 
                raise_type_error s <| sprintf "The two sides need to have the same numeric types.\nGot: %s and %s." (show_ty a_ty) (show_ty b_ty)
    | Op(Mod,[|a;b|]) -> 
        let inline op a b = a % b
        try
            match term2 s a b with
            | DLit a, DLit b ->
                match a, b with
                | LitInt8 a, LitInt8 b -> op a b |> LitInt8 |> DLit
                | LitInt16 a, LitInt16 b -> op a b |> LitInt16 |> DLit
                | LitInt32 a, LitInt32 b -> op a b |> LitInt32 |> DLit
                | LitInt64 a, LitInt64 b -> op a b |> LitInt64 |> DLit
                | LitUInt8 a, LitUInt8 b -> op a b |> LitUInt8 |> DLit
                | LitUInt16 a, LitUInt16 b -> op a b |> LitUInt16 |> DLit
                | LitUInt32 a, LitUInt32 b -> op a b |> LitUInt32 |> DLit
                | LitUInt64 a, LitUInt64 b -> op a b |> LitUInt64 |> DLit
                | LitFloat32 a, LitFloat32 b -> op a b |> nan_guardf32  |> LitFloat32 |> DLit
                | LitFloat64 a, LitFloat64 b -> op a b |> nan_guardf64 |> LitFloat64 |> DLit
                | a, b -> raise_type_error s <| sprintf "The two literals must be both numeric and equal in type.\nGot: %s and %s" (show_lit a) (show_lit b)
            | a, b ->
                let a_ty, b_ty = data_to_ty a, data_to_ty b 
                if a_ty = b_ty then
                    if is_lit_zero b then raise (DivideByZeroException())
                    elif is_numeric a_ty then push_binop s Mod (a,b) a_ty
                    else raise_type_error s <| sprintf "The type of the two arguments needs to be a numeric type.\nGot: %s" (show_ty a_ty)
                else
                    raise_type_error s <| sprintf "The two sides need to have the same numeric types.\nGot: %s and %s." (show_ty a_ty) (show_ty b_ty)
        with :? DivideByZeroException ->
            raise_type_error s <| sprintf "An attempt to divide by zero has been detected at compile time."
    | Op(LT,[|a;b|]) ->
        let inline op a b = a < b
        match term2 s a b with
        | DLit a, DLit b ->
            match a, b with
            | LitInt8 a, LitInt8 b -> op a b |> LitBool |> DLit
            | LitInt16 a, LitInt16 b -> op a b |> LitBool |> DLit
            | LitInt32 a, LitInt32 b -> op a b |> LitBool |> DLit
            | LitInt64 a, LitInt64 b -> op a b |> LitBool |> DLit
            | LitUInt8 a, LitUInt8 b -> op a b |> LitBool |> DLit
            | LitUInt16 a, LitUInt16 b -> op a b |> LitBool |> DLit
            | LitUInt32 a, LitUInt32 b -> op a b |> LitBool |> DLit
            | LitUInt64 a, LitUInt64 b -> op a b |> LitBool |> DLit
            | LitFloat32 a, LitFloat32 b -> op a b |> LitBool |> DLit
            | LitFloat64 a, LitFloat64 b -> op a b |> LitBool |> DLit
            | LitString a, LitString b -> op a b |> LitBool |> DLit
            | LitChar a, LitChar b -> op a b |> LitBool |> DLit
            | LitBool a, LitBool b -> op a b |> LitBool |> DLit
            | a, b -> raise_type_error s <| sprintf "The two literals must be equal in type.\nGot: %s and %s" (show_lit a) (show_lit b)
        | a, b ->
            let a_ty, b_ty = data_to_ty a, data_to_ty b 
            if a_ty = b_ty then
                if is_primitive a_ty then push_binop s LT (a,b) (YPrim BoolT)
                else raise_type_error s <| sprintf "The type of the two arguments needs to be a primitive type.\nGot: %s" (show_ty a_ty)
            else
                raise_type_error s <| sprintf "The two sides need to have the same primitive types.\nGot: %s and %s." (show_ty a_ty) (show_ty b_ty)
    | Op(LTE,[|a;b|]) ->
        let inline op a b = a <= b
        match term2 s a b with
        | DLit a, DLit b ->
            match a, b with
            | LitInt8 a, LitInt8 b -> op a b |> LitBool |> DLit
            | LitInt16 a, LitInt16 b -> op a b |> LitBool |> DLit
            | LitInt32 a, LitInt32 b -> op a b |> LitBool |> DLit
            | LitInt64 a, LitInt64 b -> op a b |> LitBool |> DLit
            | LitUInt8 a, LitUInt8 b -> op a b |> LitBool |> DLit
            | LitUInt16 a, LitUInt16 b -> op a b |> LitBool |> DLit
            | LitUInt32 a, LitUInt32 b -> op a b |> LitBool |> DLit
            | LitUInt64 a, LitUInt64 b -> op a b |> LitBool |> DLit
            | LitFloat32 a, LitFloat32 b -> op a b |> LitBool |> DLit
            | LitFloat64 a, LitFloat64 b -> op a b |> LitBool |> DLit
            | LitString a, LitString b -> op a b |> LitBool |> DLit
            | LitChar a, LitChar b -> op a b |> LitBool |> DLit
            | LitBool a, LitBool b -> op a b |> LitBool |> DLit
            | a, b -> raise_type_error s <| sprintf "The two literals must be equal in type.\nGot: %s and %s" (show_lit a) (show_lit b)
        | a, b ->
            let a_ty, b_ty = data_to_ty a, data_to_ty b 
            if a_ty = b_ty then
                if is_primitive a_ty then push_binop s LTE (a,b) (YPrim BoolT)
                else raise_type_error s <| sprintf "The type of the two arguments needs to be a primitive type.\nGot: %s" (show_ty a_ty)
            else
                raise_type_error s <| sprintf "The two sides need to have the same primitive types.\nGot: %s and %s." (show_ty a_ty) (show_ty b_ty)
    | Op(GT,[|a;b|]) -> 
        let inline op a b = a > b
        match term2 s a b with
        | DLit a, DLit b ->
            match a, b with
            | LitInt8 a, LitInt8 b -> op a b |> LitBool |> DLit
            | LitInt16 a, LitInt16 b -> op a b |> LitBool |> DLit
            | LitInt32 a, LitInt32 b -> op a b |> LitBool |> DLit
            | LitInt64 a, LitInt64 b -> op a b |> LitBool |> DLit
            | LitUInt8 a, LitUInt8 b -> op a b |> LitBool |> DLit
            | LitUInt16 a, LitUInt16 b -> op a b |> LitBool |> DLit
            | LitUInt32 a, LitUInt32 b -> op a b |> LitBool |> DLit
            | LitUInt64 a, LitUInt64 b -> op a b |> LitBool |> DLit
            | LitFloat32 a, LitFloat32 b -> op a b |> LitBool |> DLit
            | LitFloat64 a, LitFloat64 b -> op a b |> LitBool |> DLit
            | LitString a, LitString b -> op a b |> LitBool |> DLit
            | LitChar a, LitChar b -> op a b |> LitBool |> DLit
            | LitBool a, LitBool b -> op a b |> LitBool |> DLit
            | a, b -> raise_type_error s <| sprintf "The two literals must be equal in type.\nGot: %s and %s" (show_lit a) (show_lit b)
        | a, b ->
            let a_ty, b_ty = data_to_ty a, data_to_ty b 
            if a_ty = b_ty then
                if is_primitive a_ty then push_binop s GT (a,b) (YPrim BoolT)
                else raise_type_error s <| sprintf "The type of the two arguments needs to be a primitive type.\nGot: %s" (show_ty a_ty)
            else
                raise_type_error s <| sprintf "The two sides need to have the same primitive types.\nGot: %s and %s." (show_ty a_ty) (show_ty b_ty)
    | Op(GTE,[|a;b|]) -> 
        let inline op a b = a >= b
        match term2 s a b with
        | DLit a, DLit b ->
            match a, b with
            | LitInt8 a, LitInt8 b -> op a b |> LitBool |> DLit
            | LitInt16 a, LitInt16 b -> op a b |> LitBool |> DLit
            | LitInt32 a, LitInt32 b -> op a b |> LitBool |> DLit
            | LitInt64 a, LitInt64 b -> op a b |> LitBool |> DLit
            | LitUInt8 a, LitUInt8 b -> op a b |> LitBool |> DLit
            | LitUInt16 a, LitUInt16 b -> op a b |> LitBool |> DLit
            | LitUInt32 a, LitUInt32 b -> op a b |> LitBool |> DLit
            | LitUInt64 a, LitUInt64 b -> op a b |> LitBool |> DLit
            | LitFloat32 a, LitFloat32 b -> op a b |> LitBool |> DLit
            | LitFloat64 a, LitFloat64 b -> op a b |> LitBool |> DLit
            | LitString a, LitString b -> op a b |> LitBool |> DLit
            | LitChar a, LitChar b -> op a b |> LitBool |> DLit
            | LitBool a, LitBool b -> op a b |> LitBool |> DLit
            | a, b -> raise_type_error s <| sprintf "The two literals must be equal in type.\nGot: %s and %s" (show_lit a) (show_lit b)
        | a, b ->
            let a_ty, b_ty = data_to_ty a, data_to_ty b 
            if a_ty = b_ty then
                if is_primitive a_ty then push_binop s GTE (a,b) (YPrim BoolT)
                else raise_type_error s <| sprintf "The type of the two arguments needs to be a primitive type.\nGot: %s" (show_ty a_ty)
            else
                raise_type_error s <| sprintf "The two sides need to have the same primitive types.\nGot: %s and %s." (show_ty a_ty) (show_ty b_ty)
    | Op(EQ,[|a;b|]) -> eq s (ev s a) (ev s b)
    | Op(NEQ,[|a;b|]) ->
        let inline op a b = a <> b
        match term2 s a b with
        | DLit a, DLit b ->
            match a, b with
            | LitInt8 a, LitInt8 b -> op a b |> LitBool |> DLit
            | LitInt16 a, LitInt16 b -> op a b |> LitBool |> DLit
            | LitInt32 a, LitInt32 b -> op a b |> LitBool |> DLit
            | LitInt64 a, LitInt64 b -> op a b |> LitBool |> DLit
            | LitUInt8 a, LitUInt8 b -> op a b |> LitBool |> DLit
            | LitUInt16 a, LitUInt16 b -> op a b |> LitBool |> DLit
            | LitUInt32 a, LitUInt32 b -> op a b |> LitBool |> DLit
            | LitUInt64 a, LitUInt64 b -> op a b |> LitBool |> DLit
            | LitFloat32 a, LitFloat32 b -> op a b |> LitBool |> DLit
            | LitFloat64 a, LitFloat64 b -> op a b |> LitBool |> DLit
            | LitString a, LitString b -> op a b |> LitBool |> DLit
            | LitChar a, LitChar b -> op a b |> LitBool |> DLit
            | LitBool a, LitBool b -> op a b |> LitBool |> DLit
            | a, b -> raise_type_error s <| sprintf "The two literals must be equal in type.\nGot: %s and %s" (show_lit a) (show_lit b)
        | TyV(T(a,a_ty)), TyV(T(b,_)) when a = b && is_non_float_primitive a_ty -> LitBool false |> DLit
        | a, b ->
            let a_ty, b_ty = data_to_ty a, data_to_ty b 
            if a_ty = b_ty then
                if is_primitive a_ty then push_binop s NEQ (a,b) (YPrim BoolT)
                else raise_type_error s <| sprintf "The type of the two arguments needs to be a primitive type.\nGot: %s" (show_ty a_ty)
            else
                raise_type_error s <| sprintf "The two sides need to have the same primitive types.\nGot: %s and %s." (show_ty a_ty) (show_ty b_ty)
    | Op(BitwiseAnd,[|a;b|]) -> 
        let inline op a b = a &&& b
        match term2 s a b with
        | DLit a, DLit b ->
            match a, b with
            | LitInt8 a, LitInt8 b -> op a b |> LitInt8 |> DLit
            | LitInt16 a, LitInt16 b -> op a b |> LitInt16 |> DLit
            | LitInt32 a, LitInt32 b -> op a b |> LitInt32 |> DLit
            | LitInt64 a, LitInt64 b -> op a b |> LitInt64 |> DLit
            | LitUInt8 a, LitUInt8 b -> op a b |> LitUInt8 |> DLit
            | LitUInt16 a, LitUInt16 b -> op a b |> LitUInt16 |> DLit
            | LitUInt32 a, LitUInt32 b -> op a b |> LitUInt32 |> DLit
            | LitUInt64 a, LitUInt64 b -> op a b |> LitUInt64 |> DLit
            | a, b -> raise_type_error s <| sprintf "The two literals must be both ints and equal in type.\nGot: %s and %s" (show_lit a) (show_lit b)
        | a, b ->
            let a_ty, b_ty = data_to_ty a, data_to_ty b 
            if a_ty = b_ty then
                if is_any_int a_ty then push_binop s BitwiseAnd (a,b) a_ty
                else raise_type_error s <| sprintf "The type of the two arguments needs to be a int type.\nGot: %s" (show_ty a_ty)
            else
                raise_type_error s <| sprintf "The two sides need to have the same int types.\nGot: %s and %s." (show_ty a_ty) (show_ty b_ty)
    | Op(BitwiseOr,[|a;b|]) ->
        let inline op a b = a ||| b
        match term2 s a b with
        | DLit a, DLit b ->
            match a, b with
            | LitInt8 a, LitInt8 b -> op a b |> LitInt8 |> DLit
            | LitInt16 a, LitInt16 b -> op a b |> LitInt16 |> DLit
            | LitInt32 a, LitInt32 b -> op a b |> LitInt32 |> DLit
            | LitInt64 a, LitInt64 b -> op a b |> LitInt64 |> DLit
            | LitUInt8 a, LitUInt8 b -> op a b |> LitUInt8 |> DLit
            | LitUInt16 a, LitUInt16 b -> op a b |> LitUInt16 |> DLit
            | LitUInt32 a, LitUInt32 b -> op a b |> LitUInt32 |> DLit
            | LitUInt64 a, LitUInt64 b -> op a b |> LitUInt64 |> DLit
            | a, b -> raise_type_error s <| sprintf "The two literals must be both ints and equal in type.\nGot: %s and %s" (show_lit a) (show_lit b)
        | a, b ->
            let a_ty, b_ty = data_to_ty a, data_to_ty b 
            if a_ty = b_ty then
                if is_any_int a_ty then push_binop s BitwiseOr (a,b) a_ty
                else raise_type_error s <| sprintf "The type of the two arguments needs to be a int type.\nGot: %s" (show_ty a_ty)
            else
                raise_type_error s <| sprintf "The two sides need to have the same int types.\nGot: %s and %s." (show_ty a_ty) (show_ty b_ty)
    | Op(BitwiseXor,[|a;b|]) ->
        let inline op a b = a ^^^ b
        match term2 s a b with
        | DLit a, DLit b ->
            match a, b with
            | LitInt8 a, LitInt8 b -> op a b |> LitInt8 |> DLit
            | LitInt16 a, LitInt16 b -> op a b |> LitInt16 |> DLit
            | LitInt32 a, LitInt32 b -> op a b |> LitInt32 |> DLit
            | LitInt64 a, LitInt64 b -> op a b |> LitInt64 |> DLit
            | LitUInt8 a, LitUInt8 b -> op a b |> LitUInt8 |> DLit
            | LitUInt16 a, LitUInt16 b -> op a b |> LitUInt16 |> DLit
            | LitUInt32 a, LitUInt32 b -> op a b |> LitUInt32 |> DLit
            | LitUInt64 a, LitUInt64 b -> op a b |> LitUInt64 |> DLit
            | a, b -> raise_type_error s <| sprintf "The two literals must be both ints and equal in type.\nGot: %s and %s" (show_lit a) (show_lit b)
        | a, b ->
            let a_ty, b_ty = data_to_ty a, data_to_ty b 
            if a_ty = b_ty then
                if is_any_int a_ty then push_binop s BitwiseXor (a,b) a_ty
                else raise_type_error s <| sprintf "The type of the two arguments needs to be a int type.\nGot: %s" (show_ty a_ty)
            else
                raise_type_error s <| sprintf "The two sides need to have the same int types.\nGot: %s and %s." (show_ty a_ty) (show_ty b_ty)
    | Op(ShiftLeft,[|a;b|]) -> 
        let inline op a b = a <<< b
        match term2 s a b with
        | DLit a, DLit b ->
            match a, b with
            | LitInt32 a, LitInt32 b -> op a b |> LitInt32 |> DLit
            | LitInt64 a, LitInt32 b -> op a b |> LitInt64 |> DLit
            | LitUInt32 a, LitInt32 b -> op a b |> LitUInt32 |> DLit
            | LitUInt64 a, LitInt32 b -> op a b |> LitUInt64 |> DLit
            | a, b -> raise_type_error s <| sprintf "The first literal must be a 32 or 64 bit int and the second must be a 32-bit signed int.\nGot: %s and %s" (show_lit a) (show_lit b)
        | a, b ->
            let a_ty, b_ty = data_to_ty a, data_to_ty b 
            if is_int a_ty && is_int32 b_ty then push_binop s ShiftLeft (a,b) a_ty
            else raise_type_error s <| sprintf "The type of the first argument must be a 32 or 64 bit int and the second must be a 32-bit signed int.\nGot: %s and %s" (show_ty a_ty) (show_ty b_ty)
    | Op(ShiftRight,[|a;b|]) ->
        let inline op a b = a >>> b
        match term2 s a b with
        | DLit a, DLit b ->
            match a, b with
            | LitInt32 a, LitInt32 b -> op a b |> LitInt32 |> DLit
            | LitInt64 a, LitInt32 b -> op a b |> LitInt64 |> DLit
            | LitUInt32 a, LitInt32 b -> op a b |> LitUInt32 |> DLit
            | LitUInt64 a, LitInt32 b -> op a b |> LitUInt64 |> DLit
            | a, b -> raise_type_error s <| sprintf "The first literal must be a 32 or 64 bit int and the second must be a 32-bit signed int.\nGot: %s and %s" (show_lit a) (show_lit b)
        | a, b ->
            let a_ty, b_ty = data_to_ty a, data_to_ty b 
            if is_int a_ty && is_int32 b_ty then push_binop s ShiftRight (a,b) a_ty
            else raise_type_error s <| sprintf "The type of the first argument must be a 32 or 64 bit int and the second must be a 32-bit signed int.\nGot: %s and %s" (show_ty a_ty) (show_ty b_ty)
    | Op(Log,[|a|]) ->
        let inline op a = log a
        match ev s a with
        | DLit a ->
            match a with
            | LitFloat32 a -> op a |> nan_guardf32 |> LitFloat32 |> DLit
            | LitFloat64 a -> op a |> nan_guardf64 |> LitFloat64 |> DLit
            | _ -> raise_type_error s <| sprintf "The literal must be a float type.\nGot: %s" (show_lit a)
        | a ->
            let a_ty = data_to_ty a
            if is_float a_ty then push_op s Log a a_ty
            else raise_type_error s <| sprintf "The argument must be a float type.\nGot: %s" (show_ty a_ty)
    | Op(Exp,[|a|]) ->
        let inline op a = exp a
        match ev s a with
        | DLit a ->
            match a with
            | LitFloat32 a -> op a |> nan_guardf32 |> LitFloat32 |> DLit
            | LitFloat64 a -> op a |> nan_guardf64 |> LitFloat64 |> DLit
            | _ -> raise_type_error s <| sprintf "The literal must be a float type.\nGot: %s" (show_lit a)
        | a ->
            let a_ty = data_to_ty a
            if is_float a_ty then push_op s Exp a a_ty
            else raise_type_error s <| sprintf "The argument must be a float type.\nGot: %s" (show_ty a_ty)
    | Op(Tanh,[|a|]) -> 
        let inline op a = tanh a
        match ev s a with
        | DLit a ->
            match a with
            | LitFloat32 a -> op a |> nan_guardf32 |> LitFloat32 |> DLit
            | LitFloat64 a -> op a |> nan_guardf64 |> LitFloat64 |> DLit
            | _ -> raise_type_error s <| sprintf "The literal must be a float type.\nGot: %s" (show_lit a)
        | a ->
            let a_ty = data_to_ty a
            if is_float a_ty then push_op s Tanh a a_ty
            else raise_type_error s <| sprintf "The argument must be a float type.\nGot: %s" (show_ty a_ty)
    | Op(Sqrt,[|a|]) ->
        let inline op a = sqrt a
        match ev s a with
        | DLit a ->
            match a with
            | LitFloat32 a -> op a |> nan_guardf32 |> LitFloat32 |> DLit
            | LitFloat64 a -> op a |> nan_guardf64 |> LitFloat64 |> DLit
            | _ -> raise_type_error s <| sprintf "The literal must be a float type.\nGot: %s" (show_lit a)
        | a ->
            let a_ty = data_to_ty a
            if is_float a_ty then push_op s Sqrt a a_ty
            else raise_type_error s <| sprintf "The argument must be a float type.\nGot: %s" (show_ty a_ty)
    | Op(Neg,[|a|]) ->
        let inline op a = -a
        match ev s a with
        | DLit a ->
            match a with
            | LitInt8 a -> op a |> LitInt8 |> DLit
            | LitInt16 a -> op a |> LitInt16 |> DLit
            | LitInt32 a -> op a |> LitInt32 |> DLit
            | LitInt64 a -> op a |> LitInt64 |> DLit
            | LitFloat32 a -> op a |> LitFloat32 |> DLit
            | LitFloat64 a -> op a |> LitFloat64 |> DLit
            | _ -> raise_type_error s <| sprintf "The literal must be a signed numeric type.\nGot: %s" (show_lit a)
        | a ->
            let a_ty = data_to_ty a
            if is_signed_numeric a_ty then push_op s Neg a a_ty
            else raise_type_error s <| sprintf "The argument must be a signed numeric type.\nGot: %s" (show_ty a_ty)
    | Op(PairCreate, [|a;b|]) -> TyPair(ev s a, ev s b)

    | Op(ErrorType,[|a|]) -> ev s a |> show_typed_data |> raise_type_error s
    | Op(ErrorNonUnit,[|a'|]) -> 
        match ev s a' with
        | TyB as a -> a
        | a ->
            let d = match a' with Pos(x) -> {s with trace = x.Pos :: s.trace} | _ -> s
            raise_type_error d <| sprintf "Only the last expression of a block is allowed to be unit. Use `ignore` if it intended to be such.\nGot: %s" (show_typed_data a)
    | Op(ErrorPatMiss,[|a|]) -> ev s a |> show_typed_data |> sprintf "Pattern miss error. The argument is %s" |> raise_type_error s
    | Op(FailWith,[|Type typ;a|]) ->
        match tev s typ, ev s a with
        | typ, TyType (YPrim StringT) & a -> push_op_no_rewrite s FailWith a typ
        | _,a -> raise_type_error s "Expected a string as input to failwith.\nGot: %s" (show_typed_data a)
    | Op(InfinityF64,[||]) -> DLit (LitFloat64 infinity)
    | Op(InfinityF32,[||]) -> DLit (LitFloat32 infinityf)
    | Op(op,_) -> raise_type_error s <| sprintf "Compiler error: %A not implemented" op


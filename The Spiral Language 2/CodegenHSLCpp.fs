﻿module Spiral.Codegen.HLS.Cpp

open Spiral
open Spiral.Utils
open Spiral.Tokenize
open Spiral.BlockParsing
open Spiral.PartEval.Main
open Spiral.CodegenUtils
open System
open System.Text
open System.Collections.Generic

let is_string = function DV(L(_,YPrim StringT)) | DLit(LitString _) -> true | _ -> false

let sizeof_tyv = function
    | YPrim (Int64T | UInt64T | Float64T) -> 64
    | YPrim (Int32T | UInt32T | Float32T) -> 32
    | YPrim (Int16T | UInt16T) -> 16
    | YPrim (Int8T | UInt8T | CharT | BoolT) -> 8
    | _ -> 64
let order_args v = v |> Array.sortWith (fun (L(_,t)) (L(_,t')) -> compare (sizeof_tyv t') (sizeof_tyv t))
let line x s = if s <> "" then x.text.Append(' ', x.indent).AppendLine s |> ignore
let line' x s = line x (String.concat " " s)

type BindsReturn =
    | BindsTailEnd
    | BindsLocal of TyV []

let term_vars_to_tys x = x |> Array.map (function WV(L(_,t)) -> t | WLit x -> YPrim (lit_to_primitive_type x))
let binds_last_data x = x |> Array.last |> function TyLocalReturnData(x,_) | TyLocalReturnOp(_,_,x) -> x | TyLet _ -> raise_codegen_error "Compiler error: Cannot find the return data of the last bind."

type UnionRec = {tag : int; free_vars : Map<string, TyV[]>}
type MethodRec = {tag : int; free_vars : L<Tag,Ty>[]; range : Ty; body : TypedBind[]; name : string option}
type ClosureRec = {tag : int; free_vars : L<Tag,Ty>[]; domain : Ty; domain_args : TyV[]; range : Ty; body : TypedBind[]}
type TupleRec = {tag : int; tys : Ty []}
type CFunRec = {tag : int; domain_args_ty : Ty[]; range : Ty}

let size_t = UInt32T

// Replaces the invalid symbols in Spiral method names for the C backend.
let fix_method_name (x : string) = x.Replace(''','_')

let lit_string x =
    let strb = StringBuilder(String.length x + 2)
    strb.Append '"' |> ignore
    String.iter (function
        | '"' -> strb.Append "\\\"" 
        | '\b' -> strb.Append @"\b"
        | '\t' -> strb.Append @"\t"
        | '\n' -> strb.Append @"\n"
        | '\r' -> strb.Append @"\r"
        | '\\' -> strb.Append @"\\"
        | x -> strb.Append x
        >> ignore 
        ) x
    strb.Append '"' |> ignore
    strb.ToString()

let codegen' (env : PartEvalResult) (x : TypedBind []) =
    let globals = ResizeArray()
    let fwd_dcls = ResizeArray()
    let types = ResizeArray()
    let functions = ResizeArray()

    let print show r =
        let s_typ_fwd = {text=StringBuilder(); indent=0}
        let s_typ = {text=StringBuilder(); indent=0}
        let s_fun = {text=StringBuilder(); indent=0}
        show s_typ_fwd s_typ s_fun r
        let f (a : _ ResizeArray) (b : CodegenEnv) = 
            let text = b.text.ToString()
            if text <> "" then a.Add(text)
        f fwd_dcls s_typ_fwd
        f types s_typ
        f functions s_fun

    let union show =
        let dict = Dictionary(HashIdentity.Reference)
        let f (a : Union) : UnionRec = 
            let free_vars = a.Item.cases |> Map.map (fun _ -> env.ty_to_data >> data_free_vars)
            {free_vars=free_vars; tag=dict.Count}
        fun x ->
            let mutable dirty = false
            let r = Utils.memoize dict (fun x -> dirty <- true; f x) x
            if dirty then print show r
            r

    let jp f show =
        let dict = Dictionary(HashIdentity.Structural)
        let f x = f (x, dict.Count)
        fun x ->
            let mutable dirty = false
            let r = Utils.memoize dict (fun x -> dirty <- true; f x) x
            if dirty then print show r
            r

    let tuple show =
        let dict = Dictionary(HashIdentity.Structural)
        let f x = {tag=dict.Count; tys=x}
        fun x ->
            let mutable dirty = false
            let r = Utils.memoize dict (fun x -> dirty <- true; f x) x
            if dirty then print show r
            r

    let cfun' show =
        let dict = Dictionary(HashIdentity.Structural)
        let f (a : Ty, b : Ty) = {tag=dict.Count; domain_args_ty=a |> env.ty_to_data |> data_free_vars |> Array.map (fun (L(_,t)) -> t); range=b}
        fun x ->
            let mutable dirty = false
            let r = Utils.memoize dict (fun x -> dirty <- true; f x) x
            if dirty then print show r
            r

    let args x = x |> Array.map (fun (L(i,_)) -> sprintf "v%i" i) |> String.concat ", "

    let tmp =
        let mutable i = 0u
        fun () -> let x = i in i <- i + 1u; x

    let global' =
        let has_added = HashSet()
        fun x -> if has_added.Add(x) then globals.Add x

    let import x = global' $"#include <{x}>"
    let import' x = global' $"#include \"{x}\""

    let tyvs_to_tys (x : TyV []) = Array.map (fun (L(i,t)) -> t) x

    let rec binds_start (s : CodegenEnv) (x : TypedBind []) = binds s BindsTailEnd x
    and return_local s ret (x : string) = 
        match ret with
        | [||] -> line s $"{x};"
        | [|L(i,_)|] -> line s $"v{i} = {x};"
        | ret ->
            let tmp_i = tmp()
            line s $"{tup_ty_tyvs ret} tmp{tmp_i} = {x};"
            Array.mapi (fun i (L(i',_)) -> $"v{i'} = tmp{tmp_i}.v{i};") ret |> line' s
    and binds (s : CodegenEnv) (ret : BindsReturn) (stmts : TypedBind []) = 
        let tup_destruct (a,b) =
            Array.map2 (fun (L(i,_)) b -> 
                match b with
                | WLit b -> $"v{i} = {lit b};"
                | WV (L(i',_)) -> $"v{i} = v{i'};"
                ) a b
        Array.iter (fun x ->
            match x with
            | TyLet(d,trace,a) ->
                try let d = data_free_vars d
                    let decl_vars = Array.map (fun (L(i,t)) -> $"{tyv t} v{i};") d
                    match a with
                    | TyMacro a ->
                        let m = a |> List.map (function CMText x -> x | CMTerm x -> tup_data x | CMType x -> tup_ty x | CMTypeLit x -> type_lit x) |> String.concat ""
                        let q = m.Split("v$")
                        if q.Length = 1 then 
                            decl_vars |> line' s
                            return_local s d m 
                        else
                            if d.Length = q.Length-1 then
                                let w = StringBuilder(m.Length+8)
                                let tag (L(i,_)) = i : int
                                Array.iteri (fun i v -> w.Append(q.[i]).Append('v').Append(tag v) |> ignore) d
                                w.Append(q.[d.Length]).Append(';').ToString() |> line s
                            else
                                raise_codegen_error "The special v$ macro requires the same number of free vars in its binding as there are v$ in the code."
                    | TyArrayLiteral(a,b') -> 
                        let inits = List.map tup_data b' |> String.concat "," |> sprintf "{%s}"
                        match d with
                        | [|L(i,YArray t)|] -> // For the regular arrays.
                            line s $"%s{tyv t} v{i}[] = %s{inits};"
                        //| [|L(i,t)|] -> // TODO: For overloaded arrays. Structs with a single field that are arrays can be initialized like this in C++.
                        //    line s $"%s{tyv t} v{i} = %s{inits};"
                        | _ ->
                            raise_codegen_error "Compiler error: Expected a single variable on the left side of an array literal op."
                    | TyArrayCreate(a,b) ->  
                        match d with
                        | [|L(i,YArray t)|] -> 
                            let size =
                                match b with
                                | DLit x -> lit x
                                | _ -> raise_codegen_error "Array sizes need to be statically known in the HLS C++ backend."
                            line s $"%s{tyv t} v{i}[{size}];"
                        //| [|L(i,t)|] -> line s $"%s{tyv t} v{i};" // TODO: Put in overloaded arrays later.
                        | _ -> raise_codegen_error "Compiler error: Expected a single variable on the left side of an array create op."
                    | _ ->
                        decl_vars |> line' s
                        op s (BindsLocal d) a
                with :? CodegenError as e -> raise_codegen_error' trace (e.Data0, e.Data1)
            | TyLocalReturnOp(trace,a,_) ->
                try op s ret a
                with :? CodegenError as e -> raise_codegen_error' trace (e.Data0, e.Data1)
            | TyLocalReturnData(d,trace) ->
                try match ret with
                    | BindsLocal l -> line' s (tup_destruct (l,data_term_vars d))
                    | BindsTailEnd -> line s $"return {tup_data d};"
                with :? CodegenError as e -> raise_codegen_error' trace (e.Data0, e.Data1)
            ) stmts
    and show_w = function WV(L(i,_)) -> sprintf "v%i" i | WLit a -> lit a
    and args' b = data_term_vars b |> Array.map show_w |> String.concat ", "
    and tup_term_vars x =
        let args = Array.map show_w x |> String.concat ", "
        if 1 < x.Length then sprintf "TupleCreate%i(%s)" (tup (term_vars_to_tys x)).tag args else args
    and tup_data x = tup_term_vars (data_term_vars x)
    and tup_ty_tys = function
        | [||] -> "void"
        | [|x|] -> tyv x
        | x -> sprintf "Tuple%i" (tup x).tag
    and tup_ty_tyvs (x : TyV []) = tup_ty_tys (tyvs_to_tys x)
    and tup_ty x = env.ty_to_data x |> data_free_vars |> tup_ty_tyvs
    and tyv = function
        | YUnion a ->
            match a.Item.layout with
            | UStack -> sprintf "US%i" (ustack a).tag
            | UHeap -> sprintf "UH%i *" (uheap a).tag
        | YLayout(a,Heap) -> raise_codegen_error "Heap layout types aren't supported in the HLS C++ backend due to them needing to be heap allocated."
        | YLayout(a,HeapMutable) -> raise_codegen_error "Heap mutable layout types aren't supported in the HLS C++ backend due to them needing to be heap allocated."
        | YMacro a -> a |> List.map (function Text a -> a | Type a -> tup_ty a | TypeLit a -> type_lit a) |> String.concat ""
        | YPrim a -> prim a
        | YArray a -> sprintf "%s *" (tup_ty a)
        | YFun(a,b) -> sprintf "Fun%i" (cfun (a,b)).tag
        | a -> raise_codegen_error (sprintf "Compiler error: Type not supported in the codegen.\nGot: %A" a)
    and prim = function
        | Int8T -> "int8_t" 
        | Int16T -> "int16_t"
        | Int32T -> "int32_t"
        | Int64T -> "int64_t"
        | UInt8T -> "uint8_t"
        | UInt16T -> "uint16_t"
        | UInt32T -> "uint32_t"
        | UInt64T -> "uint64_t" // are defined in cstdint
        | Float32T -> "float"
        | Float64T -> "double"
        | BoolT -> "bool" // part of c++ standard
        | CharT -> "char"
        | StringT -> "const char *"
    and lit = function
        | LitInt8 x -> sprintf "%i" x
        | LitInt16 x -> sprintf "%i" x
        | LitInt32 x -> sprintf "%il" x
        | LitInt64 x -> sprintf "%ill" x
        | LitUInt8 x -> sprintf "%iu" x
        | LitUInt16 x -> sprintf "%iu" x
        | LitUInt32 x -> sprintf "%iul" x
        | LitUInt64 x -> sprintf "%iull" x
        | LitFloat32 x -> 
            if x = infinityf then "HUGE_VALF" // nan/inf macros are defined in math.h
            elif x = -infinityf then "-HUGE_VALF"
            elif Single.IsNaN x then "NAN"
            else x.ToString("R") |> add_dec_point |> sprintf "%sf"
        | LitFloat64 x ->
            if x = infinity then "HUGE_VAL"
            elif x = -infinity then "-HUGE_VAL"
            elif Double.IsNaN x then "NAN"
            else x.ToString("R") |> add_dec_point
        | LitString x -> lit_string x
        | LitChar x -> 
            match x with
            | '\b' -> @"\b"
            | '\n' -> @"\n"
            | '\t' -> @"\t"
            | '\r' -> @"\r"
            | '\\' -> @"\\"
            | x -> string x
            |> sprintf "'%s'"
        | LitBool x -> if x then "true" else "false" // true and false are defined in stddef.h
    and type_lit = function
        | YLit x -> lit x
        | YSymbol x -> x
        | x -> raise_codegen_error "Compiler error: Expecting a type literal in the macro." 
    and op s (ret : BindsReturn) a =
        let binds a b = binds a b
        let return' (x : string) =
            match ret with
            | BindsLocal ret -> return_local s ret x
            | BindsTailEnd -> line s $"return {x};"
        let jp (a,b') =
            let args = args b'
            match a with
            | JPMethod(a,b) -> 
                let x = method (a,b)
                sprintf "%s%i(%s)" (Option.defaultValue "method" x.name) x.tag args
            | JPClosure(a,b) -> sprintf "ClosureMethod%i" (closure (a,b)).tag
        match a with
        | TyMacro _ -> raise_codegen_error "Macros are supposed to be taken care of in the `binds` function."
        | TyIf(cond,tr,fl) ->
            line s (sprintf "if (%s){" (tup_data cond))
            binds (indent s) ret tr
            line s "} else {"
            binds (indent s) ret fl
            line s "}"
        | TyJoinPoint(a,args) -> return' (jp (a, args))
        | TyBackend(_,_,(r,_)) -> raise_codegen_error_backend r "The C backend does not support nesting of other backends."
        | TyWhile(a,b) ->
            let cond =
                match a with
                | JPMethod(a,b),b' -> sprintf "method%i(%s)" (method (a,b)).tag (args b')
                | _ -> raise_codegen_error "Expected a regular method rather than closure create in the while conditional."
            line s (sprintf "while (%s){" cond)
            binds (indent s) (BindsLocal [||]) b
            line s "}"
        | TyIntSwitch(L(v_i,_),on_succ,on_fail) ->
            line s (sprintf "switch (v%i) {" v_i)
            let _ =
                let s = indent s
                Array.iteri (fun i x ->
                    line s (sprintf "case %i: {" i)
                    binds (indent s) ret x
                    line (indent s) "break;"
                    line s "}"
                    ) on_succ
                line s "default: {"
                binds (indent s) ret on_fail
                line s "}"
            line s "}"
        | TyUnionUnbox(is,x,on_succs,on_fail) ->
            let case_tags = x.Item.tags
            let acs = match x.Item.layout with UHeap -> "->" | UStack -> "."
            let head = List.head is |> fun (L(i,_)) -> $"v{i}{acs}tag"
            let print_successes s =
                line s $"switch ({head}) {{"
                let _ =
                    let s = indent s
                    Map.iter (fun k (a,b) ->
                        let union_i = case_tags.[k]
                        line s (sprintf "case %i: { // %s" union_i k)
                        List.iter2 (fun (L(data_i,_)) a ->
                            let a, s = data_free_vars a, indent s
                            let qs = ResizeArray(a.Length)
                            Array.iteri (fun field_i (L(v_i,t) as v) -> 
                                qs.Add $"{tyv t} v{v_i} = v{data_i}{acs}v.case{union_i}.v{field_i};"
                                ) a 
                            line' s qs
                            ) is a
                        binds (indent s) ret b
                        line (indent s) "break;"
                        line s "}"
                        ) on_succs
                line s "}"
            List.pairwise is
            |> List.map (fun (L(i,_), L(i',_)) -> $"v{i}{acs}tag == v{i'}{acs}tag")
            |> String.concat " && "
            |> function 
                | "" -> 
                    print_successes s
                | x ->
                    line s $"if ({x}) {{"
                    print_successes (indent s)
                    line s "} else {"
                    on_fail |> Option.iter (binds (indent s) ret)
                    line s "}"
        | TyUnionBox(a,b,c') ->
            let c = c'.Item
            let i = c.tags.[a]
            let vars = args' b
            match c.layout with
            | UHeap -> sprintf "UH%i_%i(%s)" (uheap c').tag i vars
            | UStack -> sprintf "US%i_%i(%s)" (ustack c').tag i vars
            |> return'
        | TyLayoutToHeap(a,b) -> raise_codegen_error "Cannot create a heap layout type in the HLS C++ backend due to them needing to be heap allocated."
        | TyLayoutToHeapMutable(a,b) -> raise_codegen_error "Cannot create a heap mutable layout type in the HLS C++ backend due to them needing to be heap allocated."
        | TyLayoutIndexAll _ 
        | TyLayoutIndexByKey _ -> raise_codegen_error "Cannot index into a layout type in the HLS C++ backend due to them needing to be heap allocated."
        | TyLayoutHeapMutableSet(L(i,t),b,c) -> raise_codegen_error "Cannot set a value into a layout type in the HLS C++ backend due to them needing to be heap allocated."
        | TyArrayLiteral(a,b') -> raise_codegen_error "Compiler error: TyArrayLiteral should have been taken care of in TyLet."
        | TyArrayCreate(a,b) ->  raise_codegen_error "Compiler error: TyArrayCreate should have been taken care of in TyLet."
        | TyFailwith(a,b) -> raise_codegen_error "Failwith is not supported in the HLS C++ backend."
        | TyConv(a,b) -> return' $"({tyv a}){tup_data b}"
        | TyApply(L(i,_),b) -> 
            //raise_codegen_error "Function pointer application is not supported in the HLS C++ backend."
            $"v{i}({args' b})" |> return'
        | TyArrayLength(_,b) -> raise_codegen_error "Array length is not supported in the HLS C++ backend as they are bare pointers."
        | TyStringLength(_,b) -> raise_codegen_error "String length is not supported in the HLS C++ backend."
        | TyOp(Global,[DLit (LitString x)]) -> global' x
        | TyOp(op,l) ->
            let float_suffix = function
                | DV(L(_,YPrim Float32T)) | DLit(LitFloat32 _) -> "f"
                | _ -> ""
            match op, l with
            | Dyn,[a] -> tup_data a
            | TypeToVar, _ -> raise_codegen_error "The use of `` should never appear in generated code."
            | StringIndex, [a;b] -> sprintf "%s[%s]" (tup_data a) (tup_data b)
            | StringSlice, [a;b;c] -> raise_codegen_error "String slice is not supported natively in the C backend. Use a library implementation instead."
            | ArrayIndex, [DV(L(_,YArray t)) & a;b] -> 
                match tup_ty t with
                | "void" -> "/* void array index */"
                | _ -> sprintf "%s[%s]" (tup_data a) (tup_data b)
            | ArrayIndexSet, [DV(L(_,YArray t)) as a;b;c] -> 
                let a',b',c' = tup_data a, tup_data b, tup_data c
                match c' with
                | "" -> "/* void array set */"
                | _ -> $"{a'}[{b'}] = {c'}"
            // Math
            | Add, [a;b] -> sprintf "%s + %s" (tup_data a) (tup_data b)
            | Sub, [a;b] -> sprintf "%s - %s" (tup_data a) (tup_data b)
            | Mult, [a;b] -> sprintf "%s * %s" (tup_data a) (tup_data b)
            | Div, [a;b] -> sprintf "%s / %s" (tup_data a) (tup_data b)
            | Mod, [a;b] -> sprintf "%s %% %s" (tup_data a) (tup_data b)
            | Pow, [a;b] -> sprintf "pow%s(%s,%s)" (float_suffix a) (tup_data a) (tup_data b)
            | LT, [a;b] -> sprintf "%s < %s" (tup_data a) (tup_data b)
            | LTE, [a;b] -> sprintf "%s <= %s" (tup_data a) (tup_data b)
            | EQ, [a;b] | NEQ, [a;b] | GT, [a;b] | GTE, [a;b] when is_string a -> raise_codegen_error "String comparison operations are not supported in the HLS C++ backend."
            | EQ, [a;b] -> sprintf "%s == %s" (tup_data a) (tup_data b)
            | NEQ, [a;b] -> sprintf "%s != %s" (tup_data a) (tup_data b)
            | GT, [a;b] -> sprintf "%s > %s" (tup_data a) (tup_data b)
            | GTE, [a;b] -> sprintf "%s >= %s" (tup_data a) (tup_data b)
            | BoolAnd, [a;b] -> sprintf "%s && %s" (tup_data a) (tup_data b)
            | BoolOr, [a;b] -> sprintf "%s || %s" (tup_data a) (tup_data b)
            | BitwiseAnd, [a;b] -> sprintf "%s & %s" (tup_data a) (tup_data b)
            | BitwiseOr, [a;b] -> sprintf "%s | %s" (tup_data a) (tup_data b)
            | BitwiseXor, [a;b] -> sprintf "%s ^ %s" (tup_data a) (tup_data b)

            | ShiftLeft, [a;b] -> sprintf "%s << %s" (tup_data a) (tup_data b)
            | ShiftRight, [a;b] -> sprintf "%s >> %s" (tup_data a) (tup_data b)

            | Neg, [x] -> sprintf "-%s" (tup_data x)
            | Log, [x] -> import "math.h"; sprintf "log%s(%s)" (float_suffix x) (tup_data x)
            | Exp, [x] -> import "math.h"; sprintf "exp%s(%s)" (float_suffix x) (tup_data x)
            | Tanh, [x] -> import "math.h"; sprintf "tanh%s(%s)" (float_suffix x) (tup_data x)
            | Sqrt, [x] -> import "math.h"; sprintf "sqrt%s(%s)" (float_suffix x) (tup_data x)
            | NanIs, [x] -> import "math.h"; sprintf "isnan(%s)" (tup_data x)
            | UnionTag, [DV(L(i,YUnion l)) as x] -> 
                match l.Item.layout with
                | UHeap -> "->tag"
                | UStack -> ".tag"
                |> sprintf "v%i%s" i
            | _ -> raise_codegen_error <| sprintf "Compiler error: %A with %i args not supported" op l.Length
            |> return'
    and print_ordered_args s v = // Unlike C# for example, C keeps the struct fields in input order. To reduce padding, it is best to order the fields from largest to smallest.
        order_args v |> Array.iter (fun (L(i,x)) -> line s $"{tyv x} v{i};")
    and method : _ -> MethodRec =
        jp (fun ((jp_body,key & (C(args,_))),i) ->
            match (fst env.join_point_method.[jp_body]).[key] with
            | Some a, Some range, name -> {tag=i; free_vars=rdata_free_vars args; range=range; body=a; name=Option.map fix_method_name name}
            | _ -> raise_codegen_error "Compiler error: The method dictionary is malformed"
            ) (fun s_fwd s_typ s_fun x ->
            let ret_ty = tup_ty x.range
            let fun_name = Option.defaultValue "method" x.name
            let args = x.free_vars |> Array.mapi (fun i (L(_,x)) -> $"{tyv x} v{i}") |> String.concat ", "
            line s_fwd (sprintf "%s %s%i(%s);" ret_ty fun_name x.tag args)
            line s_fun (sprintf "%s %s%i(%s){" ret_ty fun_name x.tag args)
            binds_start (indent s_fun) x.body
            line s_fun "}"
            )
    and closure : _ -> ClosureRec =
        jp (fun ((jp_body,key & (C(args,_,domain,range))),i) ->
            match (fst env.join_point_closure.[jp_body]).[key] with
            | Some(domain_args, body) -> 
                let assert_empty x = if Array.isEmpty x then x else raise_codegen_error "The HLS C++ backend doesn't support closures due to them needing to be heap allocated, only function pointers. For them to be converted to pointers, the closures must not have any free variables in them."
                {tag=i; free_vars=rdata_free_vars args |> assert_empty; domain=domain; domain_args=data_free_vars domain_args; range=range; body=body}
            | _ -> raise_codegen_error "Compiler error: The method dictionary is malformed"
            ) (fun _ s_typ s_fun x ->
            let i, range = x.tag, tup_ty x.range
            let domain_args = x.domain_args |> Array.map (fun (L(i,t)) -> $"{tyv t} v{i}") |> String.concat ", "
            sprintf "%s ClosureMethod%i(%s){" range i domain_args |> line s_fun
            binds_start (indent s_fun) x.body
            line s_fun "}"
            )
    and cfun : _ -> CFunRec =
        cfun' (fun _ s_typ s_fun x ->
            let i, range = x.tag, tup_ty x.range
            let domain_args_ty = x.domain_args_ty |> Array.map tyv |> String.concat ", "
            line s_typ $"typedef %s{range} (* Fun%i{i})(%s{domain_args_ty});"
            )
    and tup : _ -> TupleRec = 
        tuple (fun _ s_typ s_fun x ->
            let name = sprintf "Tuple%i" x.tag
            line s_typ "typedef struct {"
            x.tys |> Array.mapi (fun i x -> L(i,x)) |> print_ordered_args (indent s_typ)
            line s_typ (sprintf "} %s;" name)

            let args = x.tys |> Array.mapi (fun i x -> $"{tyv x} v{i}")
            line s_fun (sprintf "static inline %s TupleCreate%i(%s){" name x.tag (String.concat ", " args))
            let _ =
                let s_fun = indent s_fun
                line s_fun $"{name} x;"
                Array.init args.Length (fun i -> $"x.v{i} = v{i};") |> line' s_fun
                line s_fun $"return x;"
            line s_fun "}"
            )
    and ustack : _ -> UnionRec =
        let inline map_iteri f x = Map.fold (fun i k v -> f i k v; i+1) 0 x |> ignore
        union (fun s_fwd s_typ s_fun x ->
            let i = x.tag
            line s_typ $"struct US{i} {{"
            let _ =
                let s_typ = indent s_typ
                let num_bits_needed_to_represent (x : int) = Numerics.BitOperations.Log2(x * 2 - 1 |> uint) |> max 1
                line s_typ $"unsigned int tag : {num_bits_needed_to_represent x.free_vars.Count};"
                line s_typ "union U {"
                let _ =
                    let s_typ = indent s_typ
                    map_iteri (fun tag k v -> 
                        if Array.isEmpty v = false then
                            line s_typ "struct {"
                            print_ordered_args (indent s_typ) v
                            line s_typ (sprintf "} case%i; // %s" tag k)
                        ) x.free_vars
                    line s_typ "U() {}"
                line s_typ "} v;"
                line s_typ $"US{i}() {{}}"
                let print_assignments () =
                    let s_typ = indent s_typ
                    line s_typ "this->tag = x.tag;"
                    line s_typ "switch (x.tag) {"
                    let _ =
                        let s_typ = indent s_typ
                        map_iteri (fun tag k v -> 
                            if Array.length v <> 0 then
                                line s_typ $"case {tag}: {{ this->v.case{tag} = x.v.case{tag}; break; }}"
                            ) x.free_vars
                    line s_typ "}"

                line s_typ $"US{i}(const US{i} & x) {{"
                print_assignments ()
                line s_typ "}"

                line s_typ $"US{i}(const US{i} && x) {{"
                print_assignments ()
                line s_typ "}"

                line s_typ $"US{i} & operator=(US{i} & x) {{"
                print_assignments()
                line (indent s_typ) "return *this;"
                line s_typ "}"
                
                line s_typ $"US{i} & operator=(US{i} && x) {{"
                print_assignments()
                line (indent s_typ) "return *this;"
                line s_typ "}"
            line s_typ "};"

            map_iteri (fun tag k v -> 
                let args = v |> Array.map (fun (L(i,t)) -> $"{tyv t} v{i}") |> String.concat ", "
                line s_fun (sprintf "US%i US%i_%i(%s) { // %s" i i tag args k)
                let _ =
                    let s_fun = indent s_fun
                    line s_fun $"US{i} x;"
                    line s_fun $"x.tag = {tag};"
                    if v.Length <> 0 then
                        v |> Array.map (fun (L(i,t)) -> $"x.v.case{tag}.v{i} = v{i};") |> line' s_fun
                    line s_fun "return x;"
                line s_fun "}"
                ) x.free_vars
            )
    and uheap _ : UnionRec = raise_codegen_error "Recursive unions aren't allowed in the HLS C++ backend due to them needing to be heap allocated."

    import "cstdint"

    global' "template <int dim, typename el> struct array { el v[dim]; };"

    let main_defs = {text=StringBuilder(); indent=0}

    line main_defs (sprintf "%s entry(){" (binds_last_data x |> data_term_vars |> term_vars_to_tys |> tup_ty_tys))
    binds_start (indent main_defs) x
    line main_defs "}"

    let program = StringBuilder()

    program.AppendLine("#ifndef _ENTRY")
        .AppendLine("#define _ENTRY") |> ignore

    globals |> Seq.iter (fun x -> program.AppendLine(x) |> ignore)
    types |> Seq.iter (fun x -> program.Append(x) |> ignore)
    fwd_dcls |> Seq.iter (fun x -> program.Append(x) |> ignore)
    functions |> Seq.iter (fun x -> program.Append(x) |> ignore)
    program.Append(main_defs.text)
        .AppendLine("#endif")
        .ToString()

let codegen (env : PartEvalResult) (x : TypedBind []) = codegen' env x
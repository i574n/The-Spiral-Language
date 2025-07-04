module Spiral.Codegen.C
#nowarn 40

open Spiral
open Spiral.Utils
open Spiral.Tokenize
open Spiral.Startup
open Spiral.BlockParsing
open Spiral.PartEval.Main
open Spiral.RefCounting
open Spiral.CodegenUtils
open System
open System.Text
open System.Collections.Generic

let sizeof_tyv = function
    | YPrim (Int64T | UInt64T | Float64T) -> 8
    | YPrim (Int32T | UInt32T | Float32T) -> 4
    | YPrim (Int16T | UInt16T) -> 2
    | YPrim (Int8T | UInt8T | CharT | BoolT) -> 1
    | _ -> 8
let order_args v = v |> Array.sortWith (fun (L(_,t)) (L(_,t')) -> compare (sizeof_tyv t') (sizeof_tyv t))
let line x s = if s <> "" then x.text.Append(' ', x.indent).AppendLine s |> ignore
let line' x s = line x (String.concat " " s)

let rec is_heap f x = 
    Array.exists (fun (L(i,t)) -> 
        match t with
        | YUnion a when a.Item.layout = UStack -> Array.exists (snd >> f >> is_heap f) a.Item.tag_cases
        | YPrim StringT -> true
        | YPrim _ -> false
        | _ -> true
        ) x
let is_string = function DV(L(_,YPrim StringT)) | DLit(LitString _) -> true | _ -> false

type BindsReturn =
    | BindsTailEnd
    | BindsLocal of TyV []

let term_vars_to_tys x = x |> Array.map (function WV(L(_,t)) -> t | WLit x -> YPrim (lit_to_primitive_type x))
let binds_last_data x = x |> Array.last |> function TyLocalReturnData(x,_) | TyLocalReturnOp(_,_,x) -> x | TyLet _ -> raise_codegen_error "Compiler error: Cannot find the return data of the last bind."

type UnionRec = {tag : int; free_vars : Map<int * string, TyV[]>}
type LayoutRec = {tag : int; data : Data; free_vars : TyV[]; free_vars_by_key : Map<int * string, TyV[]>}
type MethodRec = {tag : int; free_vars : L<Tag,Ty>[]; range : Ty; body : TypedBind[]; name : string option}
type ClosureRec = {tag : int; free_vars : L<Tag,Ty>[]; domain : Ty; domain_args : TyV[]; range : Ty; body : TypedBind[]}
type TupleRec = {tag : int; tys : Ty []}
type ArrayRec = {tag : int; ty : Ty; tyvs : TyV[]}
type CFunRec = {tag : int; domain_args_ty : Ty[]; range : Ty}

let size_t = UInt32T

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

let codegen (env : PartEvalResult) (x : TypedBind []) =
    let globals = ResizeArray()
    let fwd_dcls = ResizeArray()
    let types = ResizeArray()
    let functions = ResizeArray()

    let malloc, free = "malloc", "free"

    let print_decref s_fun name_fun type_arg name_decref =
        line s_fun (sprintf "void %s(%s * x){" name_fun type_arg)
        let _ =
            let s_fun = indent s_fun
            line s_fun (sprintf "if (x != NULL && --(x->refc) == 0) { %s(x); %s(x); }" name_decref free)
        line s_fun "}"

    let print show r =
        let s_typ_fwd = {text=StringBuilder(); indent=0}
        let s_typ = {text=StringBuilder(); indent=0}
        let s_fun = {text=StringBuilder(); indent=0}
        show s_typ_fwd s_typ s_fun r
        let f (a : _ ResizeArray) (b : string_builder_env) = 
            let text = b.text.ToString()
            if text <> "" then a.Add(text)
        f fwd_dcls s_typ_fwd
        f types s_typ
        f functions s_fun

    let layout show =
        let dict' = Dictionary(HashIdentity.Structural)
        let dict = Dictionary(HashIdentity.Reference)
        let f x : LayoutRec = 
            match x with
            | YLayout(x,_) ->
                let x = env.ty_to_data x
                let a, b =
                    match x with
                    | DRecord a -> let a = Map.map (fun _ -> data_free_vars) a in a |> Map.toArray |> Array.collect snd, a
                    | _ -> data_free_vars x, Map.empty
                {data=x; free_vars=a; free_vars_by_key=b; tag=dict'.Count}
            | _ -> raise_codegen_error $"Compiler error: Expected a layout type (7).\nGot: %s{show_ty x}"
        fun x ->
            let mutable dirty = false
            let r = Utils.memoize dict (Utils.memoize dict' (fun x -> dirty <- true; f x)) x
            if dirty then print show r
            r

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

    let carray' show =
        let dict = Dictionary(HashIdentity.Structural)
        let f x = {tag=dict.Count; ty=x; tyvs = env.ty_to_data x |> data_free_vars}
        fun x ->
            let mutable dirty = false
            let r = Utils.memoize dict (fun x -> dirty <- true; f x) x
            if dirty then print show r
            r

    let cstring' show =
        let mutable dirty = true
        fun () ->
            if dirty then print show ()
            dirty <- false

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
        let has_added = HashSet env.globals
        fun x -> if has_added.Add(x) then globals.Add x

    let import x = global' $"#include <{x}>"
    let import' x = global' $"#include \"{x}\""

    let tyvs_to_tys (x : TyV []) = Array.map (fun (L(i,t)) -> t) x
    
    let rec binds_start (args : TyV []) (s : string_builder_env) (x : TypedBind []) = binds (refc_prepass Set.empty (Set args) x) s BindsTailEnd x
    and return_local s ret (x : string) = 
        match ret with
        | [||] -> line s $"{x};"
        | [|L(i,_)|] -> line s $"v{i} = {x};"
        | ret ->
            let tmp_i = tmp()
            line s $"{tup_ty_tyvs ret} tmp{tmp_i} = {x};"
            Array.mapi (fun i (L(i',_)) -> $"v{i'} = tmp{tmp_i}.v{i};") ret |> line' s
    and binds (vars : RefcVars) (s : string_builder_env) (ret : BindsReturn) (stmts : TypedBind []) = 
        let tup_destruct (a,b) =
            Array.map2 (fun (L(i,_)) b -> 
                match b with
                | WLit b -> $"v{i} = {lit b};"
                | WV (L(i',_)) -> $"v{i} = v{i'};"
                ) a b
        Array.iter (fun x ->
            // This complicated looking piece of code is responsible for putting the incref and decref statements at the beginning of every
            // statement. It's actually the only place where ref counting code is outputted in the codegen.
            let _ =
                let f k = get_default k x (fun () -> Set.empty)
                let f' k = get_default k x (fun () -> Map.empty)
                let incr, decr, op, op_decr = varc_set (f vars.g_incr) 1, varc_set (f vars.g_decr) -1, f' vars.g_op, varc_set (f vars.g_op_decr) -1
                let incr, decr = varc_union incr decr |> varc_union op |> varc_union op_decr |> Map.partition (fun _ v -> 0 < v)
                refc_varc incr |> line' s; refc_varc decr |> line' s
            match x with
            | TyLet(d,trace,a) ->
                try let d = data_free_vars d
                    let decl_vars = Array.map (fun (L(i,t)) -> $"{tyv t} v{i};") d
                    match a with
                    | TyMacro a ->
                        let m = a |> List.map (function CMText x -> x | CMTerm (x,inl) -> (if inl then args' x else tup_data x) | CMType x -> tup_ty x | CMTypeLit x -> type_lit x) |> String.concat ""
                        let q = m.Split("\\v")
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
                                raise_codegen_error "The special \\v macro requires the same number of free vars in its binding as there are \\v in the code."
                    | _ ->
                        decl_vars |> line' s
                        op vars s (BindsLocal d) a
                with :? CodegenError as e -> raise_codegen_error' trace (e.Data0, e.Data1)
            | TyLocalReturnOp(trace,a,_) ->
                try op vars s ret a
                with :? CodegenError as e -> raise_codegen_error' trace (e.Data0, e.Data1)
            | TyLocalReturnData(d,trace) ->
                try match ret with
                    | BindsLocal l -> line' s (tup_destruct (l,data_term_vars d))
                    | BindsTailEnd -> line s $"return {tup_data d};"
                with :? CodegenError as e -> raise_codegen_error' trace (e.Data0, e.Data1)
            ) stmts
    and refc_change'' (f : int * Ty -> string) count (L(i,t')) =
        let v = i,t'
        let inline g decref =
            if count = -1 then Some (decref())
            elif count = 1 then Some $"{f v}->refc++;"
            elif 1 < count then Some $"{f v}->refc += {count};"
            else raise_codegen_error $"Compiler error: Invalid count in refc_change''. Got: {count}"
        match t' with
        | YUnion t -> 
            match t.Item.layout with
            | UStack -> 
                if count = -1 then Some $"USDecref{(ustack t).tag}(&({f v}));"
                elif 0 < count then Some (String.replicate count $"USIncref{(ustack t).tag}(&({f v}));")
                else raise_codegen_error $"Compiler error: Invalid count in refc_change''. UStack case. Got: {count}"
            | UHeap -> g (fun () -> $"UHDecref{(uheap t).tag}({f v});")
        | YArray t -> g (fun () -> $"ArrayDecref{(carray t).tag}({f v});")
        | YFun(a,b,FT_Vanilla) -> g (fun () ->  $"{f v}->decref_fptr({f v});")
        | YPrim StringT -> g (fun () ->  $"StringDecref({f v});" )
        | YLayout(_,Heap) as a -> g (fun () ->  $"HeapDecref{(heap a).tag}({f v});")
        | YLayout(_,HeapMutable) as a -> g (fun () ->  $"MutDecref{(mut a).tag}({f v});")
        | _ -> None
    and refc_change' (f : int * Ty -> string) count (x : TyV []) : string [] = Array.choose (refc_change'' f count) x
    and refc_change f c x = refc_change' (fun (i,t) -> f i) c x
    and refc_varc x = 
        let ar = ResizeArray(Map.count x)
        Map.iter (fun k v -> refc_change'' (fun (i,_) -> $"v{i}") v k |> Option.iter ar.Add) x
        ar
    //and refc_incr x : string [] = refc_change (fun i -> $"v{i}") 1 x
    //and refc_decr x : string [] = refc_change (fun i -> $"v{i}") -1 x
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
        | YLayout(_,lay) as a -> 
            match lay with
            | Heap -> sprintf "Heap%i *" (heap a).tag
            | HeapMutable -> sprintf "Mut%i *" (mut a).tag
            | StackMutable -> raise_codegen_error "Compiler error: The C backend doesn't support stack mutable layout types."
        | YMacro a -> a |> List.map (function Text a -> a | Type a -> tup_ty a | TypeLit a -> type_lit a) |> String.concat ""
        | YPrim a -> prim a
        | YArray a -> sprintf "Array%i *" (carray a).tag
        | YFun(a,b,FT_Vanilla) -> sprintf "Fun%i *" (cfun (a,b)).tag
        | YExists -> raise_codegen_error "Existentials are not supported at runtime. They are a compile time feature only."
        | YForall -> raise_codegen_error "Foralls are not supported at runtime. They are a compile time feature only."
        | a -> raise_codegen_error (sprintf "Compiler error: Type not supported in the codegen.\nGot: %A" a)
    and prim = function
        | Int8T -> "int8_t" 
        | Int16T -> "int16_t"
        | Int32T -> "int32_t"
        | Int64T -> "int64_t"
        | UInt8T -> "uint8_t"
        | UInt16T -> "uint16_t"
        | UInt32T -> "uint32_t"
        | UInt64T -> "uint64_t" // are defined in stdint.h
        | Float32T -> "float"
        | Float64T -> "double"
        | BoolT -> "bool" // is defined in stdbool.h
        | CharT -> "char"
        | StringT -> cstring(); "String *"
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
        | LitString x ->
            cstring()
            lit_string x |> sprintf "StringLit(%i, %s)" (x.Length + 1)
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
        | YNominal _ | YApply _ as x -> type_lit (env.nominal_apply x)
        | x -> raise_codegen_error "Compiler error: Expecting a type literal in the macro." 
    and op (vars : RefcVars) s (ret : BindsReturn) a =
        let binds a b = binds vars a b
        let return' (x : string) =
            match ret with
            | BindsLocal ret -> return_local s ret x
            | BindsTailEnd -> line s $"return {x};"
        let layout_index (x'_i : int) (x' : TyV []) =
            match ret with
            | BindsLocal x -> Array.map2 (fun (L(i,_)) (L(i',_)) -> $"v{i} = v{x'_i}->v{i'};") x x' |> line' s
            | BindsTailEnd -> raise_codegen_error "Compiler error: Layout index should never come in end position."
        let jp (a,b') =
            let args = args b'
            match a with
            | JPMethod(a,b) -> 
                let x = method (a,b)
                sprintf "%s%i(%s)" (Option.defaultValue "method" x.name) x.tag args
            | JPClosure(a,b) -> sprintf "ClosureCreate%i(%s)" (closure (a,b)).tag args
        let string_in_op = function DLit (LitString b) -> lit_string b | b -> $"{tup_data b}->ptr"
        match a with
        | TySizeOf t -> return' $"sizeof({tup_ty t})"
        | TyMacro _ -> raise_codegen_error "Macros are supposed to be taken care of in the `binds` function."
        | TyIf(cond,tr,fl) ->
            line s (sprintf "if (%s){" (tup_data cond))
            binds (indent s) ret tr
            line s "} else {"
            binds (indent s) ret fl
            line s "}"
        | TyJoinPoint(a,args) -> return' (jp (a, args))
        | TyBackend(_,_,r) -> raise_codegen_error_backend r "The C backend does not support nesting of other backends."
        | TyWhile(a,b) ->
            let cond =
                match a with
                | JPMethod(a,b),b' -> sprintf "method_while%i(%s)" (method_while (a,b)).tag (args b')
                | _ -> raise_codegen_error "Expected a regular method rather than closure create in the while conditional."
            line s (sprintf "while (%s){" cond)
            binds (indent s) (BindsLocal [||]) b
            line s "}"
        | TyDo a | TyIndent a ->
            binds s ret a
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
            List.pairwise is
            |> List.map (fun (L(i,_), L(i',_)) -> $"v{i}{acs}tag == v{i'}{acs}tag")
            |> String.concat " && "
            |> function "" -> head | x -> $"{x} ? {head} : -1"
            |> sprintf "switch (%s) {" |> line s
            let _ =
                let s = indent s
                Map.iter (fun k (a,b) ->
                    let union_i = case_tags.[k]
                    let decr = get_default vars.g_decr (Array.head b) (fun () -> Set.empty)
                    line s (sprintf "case %i: { // %s" union_i k)
                    List.iter2 (fun (L(data_i,_)) a ->
                        let a, s = data_free_vars a, indent s
                        let qs = ResizeArray(a.Length)
                        Array.iteri (fun field_i (L(v_i,t) as v) -> 
                            if Set.contains v decr = false then qs.Add $"{tyv t} v{v_i} = v{data_i}{acs}case{union_i}.v{field_i};"
                            ) a 
                        line' s qs
                        ) is a
                    binds (indent s) ret b
                    line (indent s) "break;"
                    line s "}"
                    ) on_succs
                on_fail |> Option.iter (fun b ->
                    line s "default: {"
                    binds (indent s) ret b
                    line s "}"
                    )
            line s "}"
        | TyUnionBox(a,b,c') ->
            let c = c'.Item
            let i = c.tags.[a]
            let vars = args' b
            match c.layout with
            | UHeap -> sprintf "UH%i_%i(%s)" (uheap c').tag i vars
            | UStack -> sprintf "US%i_%i(%s)" (ustack c').tag i vars
            |> return'
        | TyToLayout(a,b) -> 
            match b with
            | YLayout(_,layout) -> 
                match layout with
                | Heap -> sprintf "HeapCreate%i(%s)" (heap b).tag (args' a)
                | HeapMutable -> sprintf "MutCreate%i(%s)" (mut b).tag (args' a)
                | StackMutable -> raise_codegen_error "The C backend doesn't support stack mutable layout types."
            | _ -> raise_codegen_error $"Compiler error: Expected a layout type (8).\nGot: %s{show_ty b}"
            |> return'
        | TyLayoutIndexAll(L(i,YLayout(_,lay) & a)) ->
            match lay with
            | Heap -> heap a 
            | HeapMutable -> mut a
            | StackMutable -> raise_codegen_error "The C backend doesn't support indexing into stack mutable layout types."
            |> fun x -> x.free_vars |> layout_index i 
        | TyLayoutIndexByKey(L(i,YLayout(_,lay) & a),key) ->
            match lay with
            | Heap -> heap a 
            | HeapMutable -> mut a
            | StackMutable -> raise_codegen_error "The C backend doesn't support indexing into stack mutable layout types."
            |> fun x ->
                x.free_vars_by_key
                |> Map.tryPick (fun (_, k) v -> if k = key then Some v else None)
                |> Option.iter (layout_index i)
        | TyLayoutIndexAll _ | TyLayoutIndexByKey _ -> raise_codegen_error "Compiler error: Expected the TyV in layout index to be a layout type."
        | TyLayoutMutableSet(L(i,t),b,c) ->
            let q = mut t // `mut t` is correct here, peval strips the YLayout.
            let a = List.fold (fun s k ->
                match s with
                | DRecord l -> l |> Map.pick (fun (_,k') v -> if k' = k then Some v else None)
                | _ -> raise_codegen_error "Compiler error: Expected a record.") q.data b 
            Array.map2 (fun (L(i',_)) b -> $"&(v{i}->v{i'}), {show_w b}") (data_free_vars a) (data_term_vars c) |> String.concat ", " 
            |> sprintf "AssignMut%i(%s)" (assign_mut (tyvs_to_tys q.free_vars)).tag |> return'
        | TyArrayLiteral(a,b') ->
            let b = List.map tup_data b' |> String.concat "," |> sprintf "{%s}"
            $"ArrayLit{(carray a).tag}({b'.Length}, ({tup_ty a} []){b})" |> return'
        | TyArrayCreate(a,b) -> 
            let a = carray a
            let is_heap : string = is_heap (env.ty_to_data >> data_free_vars) a.tyvs |> sprintf "%b"
            $"ArrayCreate{a.tag}({tup_data b}, {is_heap})" |> return'
        | TyFailwith(a,b) -> 
            let fmt = @"%s\n"
            line s $"fprintf(stderr, \"{fmt}\", {string_in_op b});"
            line s "exit(EXIT_FAILURE);" // TODO: Print out the error traces as well.
        | TyConv(a,b) -> return' $"({tyv a}){tup_data b}"
        | TyApply(L(i,_),b) -> 
            match args' b with
            | "" -> $"v{i}->fptr(v{i})"
            | b -> $"v{i}->fptr(v{i}, {b})"
            |> return'
        | TyArrayLength(_,b) -> return' $"{tup_data b}->len"
        | TyStringLength(_,b) -> return' $"{tup_data b}->len-1"
        | TyOp(Global,[DLit (LitString x)]) -> global' x
        | TyOp(op,l) ->
            let float_suffix = function
                | DV(L(_,YPrim Float32T)) | DLit(LitFloat32 _) -> "f"
                | _ -> ""
            match op, l with
            | Dyn,[a] -> tup_data a
            | TypeToVar, _ -> raise_codegen_error "The use of `` should never appear in generated code."
            | StringIndex, [a;b] -> sprintf "%s->ptr[%s]" (tup_data a) (tup_data b)
            | StringSlice, [a;b;c] -> raise_codegen_error "String slice is not supported natively in the C backend. Use a library implementation instead."
            | ArrayIndex, [DV(L(_,YArray t)) & a;b] -> 
                match tup_ty t with
                | "void" -> "/* void array index */"
                | _ -> sprintf "%s->ptr[%s]" (tup_data a) (tup_data b)
            | ArrayIndexSet, [DV(L(_,YArray t)) as a;b;c] -> 
                let a',b',c' = tup_data a, tup_data b, tup_data c
                match c' with
                | "" -> "/* void array set */"
                | _ -> $"AssignArray{(assign_array (tyvs_to_tys (carray t).tyvs)).tag}(&({a'}->ptr[{b'}]), {c'})"
            // Math
            | Add, [a;b] -> sprintf "%s + %s" (tup_data a) (tup_data b)
            | Sub, [a;b] -> sprintf "%s - %s" (tup_data a) (tup_data b)
            | Mult, [a;b] -> sprintf "%s * %s" (tup_data a) (tup_data b)
            | Div, [a;b] -> sprintf "%s / %s" (tup_data a) (tup_data b)
            | Mod, [a;b] -> sprintf "%s %% %s" (tup_data a) (tup_data b)
            | Pow, [a;b] -> sprintf "pow%s(%s,%s)" (float_suffix a) (tup_data a) (tup_data b)
            | LT, [a;b] -> sprintf "%s < %s" (tup_data a) (tup_data b)
            | LTE, [a;b] -> sprintf "%s <= %s" (tup_data a) (tup_data b)
            | EQ, [a;b] when is_string a -> import "string.h"; sprintf "strcmp(%s->ptr, %s->ptr) == 0" (string_in_op a) (string_in_op b) // TODO: Optimize string structural comparison in the real_core
            | NEQ, [a;b] when is_string a -> import "string.h"; sprintf "strcmp(%s->ptr, %s->ptr) != 0" (string_in_op a) (string_in_op b)
            | GT, [a;b] when is_string a -> import "string.h"; sprintf "strcmp(%s->ptr, %s->ptr) > 0" (string_in_op a) (string_in_op b)
            | GTE, [a;b] when is_string a -> import "string.h"; sprintf "strcmp(%s->ptr, %s->ptr) >= 0" (string_in_op a) (string_in_op b)
            | EQ, [a;b] -> sprintf "%s == %s" (tup_data a) (tup_data b)
            | NEQ, [a;b] -> sprintf "%s != %s" (tup_data a) (tup_data b)
            | GT, [a;b] -> sprintf "%s > %s" (tup_data a) (tup_data b)
            | GTE, [a;b] -> sprintf "%s >= %s" (tup_data a) (tup_data b)
            | BoolAnd, [a;b] -> sprintf "%s && %s" (tup_data a) (tup_data b)
            | BoolOr, [a;b] -> sprintf "%s || %s" (tup_data a) (tup_data b)
            | BitwiseAnd, [a;b] -> sprintf "%s & %s" (tup_data a) (tup_data b)
            | BitwiseOr, [a;b] -> sprintf "%s | %s" (tup_data a) (tup_data b)
            | BitwiseXor, [a;b] -> sprintf "%s ^ %s" (tup_data a) (tup_data b)
            | BitwiseComplement, [a] -> sprintf "~%s" (tup_data a)

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
    and method_templ is_while fun_name : _ -> MethodRec =
        jp (fun ((jp_body,key & (C(args,_))),i) ->
            match (fst env.join_point_method.[jp_body]).[key] with
            | Some a, Some range, name -> {tag=i; free_vars=rdata_free_vars args; range=range; body=a; name=name}
            | _ -> raise_codegen_error "Compiler error: The method dictionary is malformed"
            ) (fun _ s_typ s_fun x ->
            let ret_ty = tup_ty x.range
            let args = x.free_vars |> Array.mapi (fun i (L(_,x)) -> $"{tyv x} v{i}") |> String.concat ", "
            let fun_name = Option.defaultValue fun_name x.name
            line s_fun (sprintf "%s %s%i(%s){" ret_ty fun_name x.tag args)
            binds_start (if is_while then [||] else x.free_vars) (indent s_fun) x.body
            line s_fun "}"
            )
    and method_while : _ -> MethodRec = method_templ true "method_while"
    and method : _ -> MethodRec = method_templ false "method"
    and closure : _ -> ClosureRec =
        jp (fun ((jp_body,key & (C(args,_,fun_ty))),i) ->
            match fun_ty with
            | YFun(domain,range,FT_Vanilla) ->
                match (fst env.join_point_closure.[jp_body]).[key] with
                | Some(domain_args, body) -> {tag=i; free_vars=rdata_free_vars args; domain=domain; domain_args=data_free_vars domain_args; range=range; body=body}
                | _ -> raise_codegen_error "Compiler error: The method dictionary is malformed"
            | YFun(_,_,_)-> raise_codegen_error "Non-standard functions are not supported in the C backend."
            | _ -> raise_codegen_error "Compiler error: Unexpected type in the closure join point."
            ) (fun _ s_typ s_fun x ->
            
            let i, range = x.tag, tup_ty x.range
            line s_typ (sprintf "typedef struct Closure%i Closure%i;" i i)
            line s_typ (sprintf "struct Closure%i {" i)
            let _ =
                let s_typ = indent s_typ
                line s_typ $"int refc;"
                line s_typ $"void (*decref_fptr)(Closure{i} *);"
                match x.domain_args |> Array.map (fun (L(_,t)) -> tyv t) |> String.concat ", " with
                | "" -> $"{range} (*fptr)(Closure{i} *);"
                | domain_args_ty -> $"{range} (*fptr)(Closure{i} *, {domain_args_ty});"
                |> line s_typ
                print_ordered_args s_typ x.free_vars
            line s_typ "};"

            line s_fun (sprintf "static inline void ClosureDecrefBody%i(Closure%i * x){" i i)
            let _ =
                let s_fun = indent s_fun
                x.free_vars |> refc_change (fun i -> $"x->v{i}") -1 |> line' s_fun
            line s_fun "}"

            print_decref s_fun $"ClosureDecref{i}" $"Closure{i}" $"ClosureDecrefBody{i}"
            
            match x.domain_args |> Array.map (fun (L(i,t)) -> $"{tyv t} v{i}") |> String.concat ", " with
            | "" -> sprintf "%s ClosureMethod%i(Closure%i * x){" range i i
            | domain_args -> sprintf "%s ClosureMethod%i(Closure%i * x, %s){" range i i domain_args
            |> line s_fun
            let _ =
                let s_fun = indent s_fun
                x.free_vars |> Array.map (fun (L(i,t)) -> $"{tyv t} v{i} = x->v{i};") |> line' s_fun
                line s_fun $"ClosureDecref{i}(x);"
                binds_start x.domain_args s_fun x.body
            line s_fun "}"

            let fun_tag = (cfun (x.domain,x.range)).tag
            let free_vars = x.free_vars |> Array.map (fun (L(i,t)) -> $"{tyv t} v{i}")
            line s_fun (sprintf "Fun%i * ClosureCreate%i(%s){" fun_tag i (String.concat ", " free_vars))
            let _ =
                let s_fun = indent s_fun
                line s_fun $"Closure{i} * x = {malloc}(sizeof(Closure{i}));"
                line s_fun "x->refc = 1;"
                line s_fun $"x->decref_fptr = ClosureDecref{i};"
                line s_fun $"x->fptr = ClosureMethod{i};"
                x.free_vars |> Array.map (fun (L(i,_)) -> $"x->v{i} = v{i};")  |> line' s_fun
                line s_fun $"return (Fun{fun_tag} *) x;"
            line s_fun "}"
            )
    and cfun : _ -> CFunRec =
        cfun' (fun _ s_typ s_fun x ->
            let i, range = x.tag, tup_ty x.range
            line s_typ $"typedef struct Fun{i} Fun{i};"
            line s_typ (sprintf "struct Fun%i{" i)
            let _ =
                let s_typ = indent s_typ
                line s_typ $"int refc;"
                line s_typ $"void (*decref_fptr)(Fun{i} *);"
                match x.domain_args_ty |> Array.map tyv |> String.concat ", " with
                | "" -> $"{range} (*fptr)(Fun{i} *);"
                | domain_args_ty -> $"{range} (*fptr)(Fun{i} *, {domain_args_ty});"
                |> line s_typ
            line s_typ "};"
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
    and assign_mut : _ -> TupleRec = 
        tuple (fun _ s_typ s_fun x ->
            let tyvs = Array.mapi (fun i t -> L(i,t)) x.tys
            let args = Array.mapi (fun i t -> let t = tyv t in $"{t} * a{i}, {t} b{i}") x.tys |> String.concat ", "
            line s_fun (sprintf "static inline void AssignMut%i(%s){" x.tag args)
            let _ =
                let s_fun = indent s_fun
                refc_change (fun i -> $"b{i}") 1 tyvs |> line' s_fun
                refc_change (fun i -> $"*a{i}") -1 tyvs |> line' s_fun
                Array.init tyvs.Length (fun i -> $"*a{i} = b{i};") |> line' s_fun
            line s_fun "}"
            )
    and assign_array : _ -> TupleRec = 
        tuple (fun _ s_typ s_fun x ->
            let tyvs, T = Array.mapi (fun i t -> L(i,t)) x.tys, tup_ty_tys x.tys
            line s_fun (sprintf "static inline void AssignArray%i(%s * a, %s b){" x.tag T T)
            let _ =
                let s_fun = indent s_fun
                match tyvs with
                | [||] -> raise_codegen_error "Compiler error: Void types not allowed in assign."
                | [|t|] -> 
                    refc_change (fun i -> "b") 1 tyvs |> line' s_fun
                    refc_change (fun i -> "*a") -1 tyvs |> line' s_fun
                    $"*a = b;" |> line s_fun
                | _ ->
                    refc_change (fun i -> $"b.v{i}") 1 tyvs |> line' s_fun
                    refc_change (fun i -> $"a->v{i}") -1 tyvs |> line' s_fun
                    $"*a = b;" |> line s_fun
            line s_fun "}"
            )
    and layout_tmpl name : _ -> LayoutRec =
        layout (fun _ s_typ s_fun (x : LayoutRec) ->
            let i = x.tag
            let name' = sprintf "%s%i" name i

            line s_typ "typedef struct {"
            let _ =
                let s_typ = indent s_typ
                line s_typ "int refc;"
                print_ordered_args s_typ x.free_vars
            line s_typ (sprintf "} %s;" name')

            line s_fun (sprintf "static inline void %sDecrefBody%i(%s * x){" name i name')
            let _ =
                let s_fun = indent s_fun
                x.free_vars |> refc_change (fun i -> $"x->v{i}") -1 |> line' s_fun
            line s_fun "}"

            print_decref s_fun $"{name}Decref{i}" name' $"{name}DecrefBody{i}"

            let args = x.free_vars |> Array.map (fun (L(i,x)) -> $"{tyv x} v{i}")
            line s_fun (sprintf "%s * %sCreate%i(%s){" name' name i (String.concat ", " args))
            let _ =
                let s_fun = indent s_fun
                line s_fun $"{name'} * x = {malloc}(sizeof({name'}));"
                line s_fun "x->refc = 1;"
                Array.init args.Length (fun i -> $"x->v{i} = v{i};") |> line' s_fun
                line s_fun $"return x;"
            line s_fun "}"
            )
    and heap : _ -> LayoutRec = layout_tmpl "Heap"
    and mut : _ -> LayoutRec = layout_tmpl "Mut"
    and union_tmpl is_stack : Union -> UnionRec = 
        let inline map_iteri f x = Map.fold (fun i k v -> f i k v; i+1) 0 x |> ignore
        union (fun s_fwd s_typ s_fun x ->
            let i = x.tag
            match is_stack with
            | true  -> line s_typ "typedef struct {"
            | false -> 
                line s_fwd (sprintf "typedef struct UH%i UH%i;" i i)
                line s_typ (sprintf "struct UH%i {" i)
            let _ =
                let s_typ = indent s_typ
                match is_stack with
                | true -> ()
                | false -> line s_typ "int refc;"
                line s_typ "int tag;"
                line s_typ "union {"
                let _ =
                    let s_typ = indent s_typ
                    map_iteri (fun tag (_, k) v -> 
                        if Array.isEmpty v = false then
                            line s_typ "struct {"
                            print_ordered_args (indent s_typ) v
                            line s_typ (sprintf "} case%i; // %s" tag k)
                        ) x.free_vars
                line s_typ "};"
            match is_stack with
            | true  -> line s_typ (sprintf "} US%i;" i)
            | false -> line s_typ "};"

            let print_refc name typ q =
                line s_fun (sprintf "static inline void %s(%s * x){" name typ)
                let _ =
                    let s_fun = indent s_fun
                    line s_fun "switch (x->tag) {"
                    map_iteri (fun tag k v -> 
                        let s_fun = indent s_fun
                        let refc = v |> refc_change (fun i -> $"x->case{tag}.v{i}") q
                        if refc.Length <> 0 then
                            line s_fun (sprintf "case %i: {" tag)
                            let _ =
                                let s_fun = indent s_fun
                                refc |> line' s_fun
                                line s_fun "break;"
                            line s_fun "}"
                        ) x.free_vars
                    line s_fun "}"
                line s_fun "}"

            match is_stack with
            | true  -> 
                print_refc $"USIncrefBody{i}" $"US{i}" 1
                print_refc $"USDecrefBody{i}" $"US{i}" -1
            | false -> print_refc $"UHDecrefBody{i}" $"UH{i}" -1

            match is_stack with
            | true  -> 
                line s_fun (sprintf "void USIncref%i(US%i * x){ USIncrefBody%i(x); }" i i i)
                line s_fun (sprintf "void USDecref%i(US%i * x){ USDecrefBody%i(x); }" i i i)
            | false -> 
                line s_fwd (sprintf "void UHDecref%i(UH%i * x);" i i)
                print_decref s_fun $"UHDecref{i}" $"UH{i}" $"UHDecrefBody{i}"
            
            map_iteri (fun tag (_, k) v -> 
                let args = v |> Array.map (fun (L(i,t)) -> $"{tyv t} v{i}") |> String.concat ", "
                if is_stack then
                    line s_fun (sprintf "US%i US%i_%i(%s) { // %s" i i tag args k)
                    let _ =
                        let s_fun = indent s_fun
                        line s_fun $"US{i} x;"
                        line s_fun $"x.tag = {tag};"
                        if v.Length <> 0 then
                            v |> Array.map (fun (L(i,t)) -> $"x.case{tag}.v{i} = v{i};") |> line' s_fun
                        line s_fun "return x;"
                    line s_fun "}"
                else
                    line s_fun (sprintf "UH%i * UH%i_%i(%s) { // %s" i i tag args k)
                    let _ =
                        let s_fun = indent s_fun
                        line s_fun $"UH{i} * x = {malloc}(sizeof(UH{i}));"
                        line s_fun $"x->tag = {tag};"
                        line s_fun "x->refc = 1;"
                        if v.Length <> 0 then
                            v |> Array.map (fun (L(i,t)) -> $"x->case{tag}.v{i} = v{i};") |> line' s_fun
                        line s_fun $"return x;"
                    line s_fun "}"
                ) x.free_vars
            )
    and ustack : _ -> UnionRec = union_tmpl true
    and uheap : _ -> UnionRec = union_tmpl false
    and carray : _ -> ArrayRec =
        carray' (fun _ s_typ s_fun x ->
            let i, len_t, ptr_t = x.tag, prim size_t, tup_ty_tyvs x.tyvs
            line s_typ "typedef struct {"
            let _ =
                let s_typ = indent s_typ
                line s_typ "int refc;"
                line s_typ $"{len_t} len;"
                if ptr_t <> "void" then line s_typ $"{ptr_t} ptr[];" // flexible array member
            line s_typ (sprintf "} Array%i;" i)


            let print_body p s_fun q =
                let refcs = x.tyvs |> refc_change (fun i -> if 1 < x.tyvs.Length then $"v.v{i}" else "v") q
                if refcs.Length <> 0 then
                    p()
                    line s_fun (sprintf "for (%s i=0; i < len; i++){" len_t)
                    let _ =
                        let s_fun = indent s_fun
                        line s_fun $"{ptr_t} v = ptr[i];"
                        refcs |> line' s_fun
                    line s_fun "}"

            line s_fun (sprintf "static inline void ArrayDecrefBody%i(Array%i * x){" i i)
            let _ =
                let s_fun = indent s_fun
                print_body (fun () ->
                    line s_fun $"{len_t} len = x->len;"
                    line s_fun $"{ptr_t} * ptr = x->ptr;"
                    ) s_fun -1
            line s_fun "}"

            print_decref s_fun $"ArrayDecref{i}" $"Array{i}" $"ArrayDecrefBody{i}"
            
            line s_fun (sprintf "Array%i * ArrayCreate%i(%s len, bool init_at_zero){" i i len_t)
            let _ =
                let s_fun = indent s_fun
                match ptr_t with
                | "void" -> line s_fun $"{len_t} size = sizeof(Array{i});"
                | _ -> line s_fun $"{len_t} size = sizeof(Array{i}) + sizeof({ptr_t}) * len;"
                line s_fun $"Array{i} * x = {malloc}(size);"
                line s_fun "if (init_at_zero) { memset(x,0,size); }"
                line s_fun "x->refc = 1;"
                line s_fun "x->len = len;"
                line s_fun "return x;"
            line s_fun "}"

            line s_fun (sprintf "Array%i * ArrayLit%i(%s len, %s * ptr){" i i len_t ptr_t)
            let _ =
                let s_fun = indent s_fun
                line s_fun $"Array{i} * x = ArrayCreate{i}(len, false);"
                if ptr_t <> "void" then 
                    line s_fun $"memcpy(x->ptr, ptr, sizeof({ptr_t}) * len);"
                    print_body (fun () -> ()) (indent s_fun) 1
                line s_fun "return x;"
            line s_fun "}"
            )
    and cstring : unit -> unit =
        cstring' (fun _ s_typ s_fun () ->
            let char = YPrim CharT
            let size_t, ptr_t, tag = prim size_t, tyv char, (carray char).tag
            line s_typ $"typedef Array{tag} String;"

            line s_fun "static inline void StringDecref(String * x){"
            line (indent s_fun) $"return ArrayDecref{tag}(x);"
            line s_fun "}"

            line s_fun (sprintf "static inline String * StringLit(%s len, %s * ptr){" size_t ptr_t)
            line (indent s_fun) $"return ArrayLit{tag}(len, ptr);"
            line s_fun "}"
            )

    match binds_last_data x |> data_term_vars |> term_vars_to_tys with
    | [|YPrim Int32T|] ->
        import "stdbool.h"
        import "stdint.h"
        import "stdio.h"
        import "stdlib.h"

        let main_defs = {text=StringBuilder(); indent=0}
        import "string.h" // for memcpy

        line main_defs (sprintf "%s main(){" (prim Int32T))
        binds_start [||] (indent main_defs) x
        line main_defs "}"

        let program = StringBuilder()

        globals |> Seq.iter (fun x -> program.AppendLine(x) |> ignore)
        fwd_dcls |> Seq.iter (fun x -> program.Append(x) |> ignore)
        types |> Seq.iter (fun x -> program.Append(x) |> ignore)
        functions |> Seq.iter (fun x -> program.Append(x) |> ignore)
        program.Append(main_defs.text).ToString()
    | _ ->
        raise_codegen_error "The return type of main in the C backend should be a 32-bit int."

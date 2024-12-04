module Spiral.Codegen.Python

open Spiral
open Spiral.Tokenize
open Spiral.Startup
open Spiral.BlockParsing
open Spiral.PartEval.Main
open Spiral.CodegenUtils

open System
open System.Text
open System.Collections.Generic

let private backend_name = "Python"

let lit = function
    | LitInt8 x -> sprintf "%i" x
    | LitInt16 x -> sprintf "%i" x
    | LitInt32 x -> sprintf "%i" x
    | LitInt64 x -> sprintf "%i" x
    | LitUInt8 x -> sprintf "%i" x
    | LitUInt16 x -> sprintf "%i" x
    | LitUInt32 x -> sprintf "%i" x
    | LitUInt64 x -> sprintf "%i" x
    | LitFloat32 x -> 
        if x = infinityf then "float('inf')"
        elif x = -infinityf then "float('-inf')"
        elif Single.IsNaN x then "float()"
        else x.ToString("R") |> add_dec_point
    | LitFloat64 x ->
        if x = infinity then "float('inf')"
        elif x = -infinity then "float('-inf')"
        elif Double.IsNaN x then "float()"
        else x.ToString("R") |> add_dec_point
    | LitString x -> 
        let strb = StringBuilder(x.Length+2)
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
    | LitChar x -> 
        match x with
        | '\b' -> @"\b"
        | '\n' -> @"\n"
        | '\t' -> @"\t"
        | '\r' -> @"\r"
        | '\\' -> @"\\"
        | ''' -> @"\'"
        | x -> string x
        |> sprintf "'%s'"
    | LitBool x -> if x then "True" else "False"

let type_lit = function
    | YLit x -> lit x
    | YSymbol x -> x
    | x -> raise_codegen_error "Compiler error: Expecting a type literal in the macro." 

let show_w = function WV(L(i,_)) -> sprintf "v%i" i | WLit a -> lit a
let args x = x |> Array.map (fun (L(i,_)) -> sprintf "v%i" i) |> String.concat ", "
let prim x = Infer.show_primt x
let cupy_ty x =
    match x with
    | [|L(_,x)|] ->
        match x with
        | YPrim x ->
            match x with
            | Int8T -> "cp.int8"
            | Int16T -> "cp.int16"
            | Int32T -> "cp.int32"
            | Int64T -> "cp.int64"
            | UInt8T -> "cp.uint8"
            | UInt16T -> "cp.uint16"
            | UInt32T -> "cp.uint32"
            | UInt64T -> "cp.uint64"
            | Float32T -> "cp.float32"
            | Float64T -> "cp.float64"
            | BoolT -> "cp.bool_"
            | _ -> "object"
        | _ -> "object"
    | _ -> "object"

type UnionRec = {tag : int; free_vars : Map<int * string, TyV[]>}
type LayoutRec = {tag : int; data : Data; free_vars : TyV[]; free_vars_by_key : Map<int * string, TyV[]>}
type MethodRec = {tag : int; free_vars : L<Tag,Ty>[]; range : Ty; body : TypedBind[]}
type ClosureRec = {tag : int; free_vars : L<Tag,Ty>[]; domain : Ty; domain_args : TyV[]; range : Ty; body : TypedBind[]}

type BindsReturn =
    | BindsTailEnd
    | BindsLocal of TyV []

let line x s = if s <> "" then x.text.Append(' ', x.indent).AppendLine s |> ignore

let codegen' backend_handler (env : PartEvalResult) (x : TypedBind []) =
    let fwd_dcls = ResizeArray()
    let types = ResizeArray()
    let functions = ResizeArray()

    let global' =
        let has_added = HashSet env.globals
        fun x -> if has_added.Add(x) then env.globals.Add x

    let import x = global' $"import {x}"
    let from x = global' $"from {x}"

    let print is_type show r =
        let s = {text=StringBuilder(); indent=0}
        show s r
        let text = s.text.ToString()
        if is_type then types.Add(text) else functions.Add(text)

    let union show =
        let dict = Dictionary(HashIdentity.Reference)
        let f (a : Union) : UnionRec =
            let free_vars = a.Item.cases |> Map.map (fun _ -> env.ty_to_data >> data_free_vars)
            {free_vars=free_vars; tag=dict.Count}
        fun x ->
            let mutable dirty = false
            let r = Utils.memoize dict (fun x -> dirty <- true; f x) x
            if dirty then print true show r
            r

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
            | _ -> raise_codegen_error $"Compiler error: Expected a layout type (5).\nGot: %s{show_ty x}"
        fun x ->
            let mutable dirty = false
            let r = Utils.memoize dict (Utils.memoize dict' (fun x -> dirty <- true; f x)) x
            if dirty then print true show r
            r

    let jp is_type f show =
        let dict = Dictionary(HashIdentity.Structural)
        let f x = f (x, dict.Count)
        fun x ->
            let mutable dirty = false
            let r = Utils.memoize dict (fun x -> dirty <- true; f x) x
            if dirty then print is_type show r
            r

    let cupy_ty x = env.ty_to_data x |> data_free_vars |> cupy_ty
    let rec binds_start (args : TyV []) (s : CodegenEnv) (x : TypedBind []) = binds (RefCounting.refc_prepass Set.empty (Set args) x).g_decr s BindsTailEnd x
    and binds g_decr (s : CodegenEnv) (ret : BindsReturn) (stmts : TypedBind []) = 
        let s_len = s.text.Length
        let tup_destruct (a,b) =
            if 0 < Array.length a then
                let a = args a
                let b = Array.map show_w (data_term_vars b) |> String.concat ", "
                sprintf "%s = %s" a b |> line s
        Array.iter (fun x ->
            let _ =
                let f (g : Dictionary<_,_>) = match g.TryGetValue(x) with true, x -> Seq.toArray x | _ -> [||]
                match args (f g_decr) with "" -> () | x -> sprintf "del %s" x |> line s
            match x with
            | TyLet(d,trace,a) ->
                try op g_decr s (BindsLocal (data_free_vars d)) a
                with :? CodegenError as e -> raise_codegen_error' trace (e.Data0,e.Data1)
            | TyLocalReturnOp(trace,a,_) ->
                try op g_decr s ret a
                with :? CodegenError as e -> raise_codegen_error' trace (e.Data0,e.Data1)
            | TyLocalReturnData(d,trace) ->
                try match ret with
                    | BindsLocal l -> tup_destruct (l, d) 
                    | BindsTailEnd -> line s $"return {tup_data' d}"
                with :? CodegenError as e -> raise_codegen_error' trace (e.Data0,e.Data1)
            ) stmts
        if s.text.Length = s_len then line s "pass"
    and tup_data' x = 
        match Array.map show_w (data_term_vars x) with
        | [||] -> ""
        | [|x|] -> x
        | args -> String.concat ", " args
    and tup_data x = 
        match Array.map show_w (data_term_vars x) with
        | [||] -> "None"
        | [|x|] -> x
        | args -> sprintf "(%s)" (String.concat ", " args)
    and tyv = function
        | YUnion a ->
            match a.Item.layout with
            | UHeap -> sprintf "UH%i" (uheap a).tag
            | UStack -> sprintf "US%i" (ustack a).tag
        | YLayout(_,lay) as a -> 
            match lay with
            | Heap -> sprintf "Heap%i" (heap a).tag
            | HeapMutable -> sprintf "Mut%i" (mut a).tag
            | StackMutable -> raise_codegen_error "Compiler error: The Python backend doesn't support stack mutable layout types."
        | YMacro [Text "backend_switch "; Type (YRecord r)] ->
            match r |> Map.tryPick (fun (_, k) v -> if k = backend_name then Some v else None) with
            | Some x -> tup_ty x
            | None -> raise_codegen_error $"In the backend_switch, expected a record with the '{backend_name}' field."
        | YMacro a ->
            a
            |> List.map (function
                | Text a -> a
                | Type a -> tup_ty a
                | TypeLit a -> type_lit a
            )
            |> String.concat ""
        | YPrim a -> prim a
        | YArray a -> "(cp if cuda else np).ndarray"
        | YFun(a,b,FT_Vanilla) -> 
            let a = env.ty_to_data a |> data_free_vars |> Array.map (fun (L(_,t)) -> tyv t) |> String.concat ", "
            $"Callable[[{a}], {tup_ty b}]"
        | YExists -> raise_codegen_error "Existentials are not supported at runtime. They are a compile time feature only."
        | YForall -> raise_codegen_error "Foralls are not supported at runtime. They are a compile time feature only."
        | a -> raise_codegen_error $"Complier error: Type not supported in the codegen.\nGot: %A{a}"
    and tup_ty x =
        match env.ty_to_data x |> data_free_vars |> Array.map (fun (L(_,t)) -> tyv t) with
        | [||] -> "None"
        | [|x|] -> x
        | x -> String.concat ", " x |> sprintf "Tuple[%s]"
    and op g_decr s (ret : BindsReturn) a =
        let return' (x : string) =
            match ret with
            | BindsTailEnd -> line s $"return {x}"
            | BindsLocal ret -> line s (if ret.Length = 0 then x else sprintf "%s = %s" (args ret) x)
        let jp (a,b) =
            let args = args b
            match a with
            | JPMethod(a,b) -> sprintf "method%i(%s)" (method (a,b)).tag args
            | JPClosure(a,b) -> sprintf "Closure%i(%s)" (closure (a,b)).tag args
        let layout_index i x' =
            x' |> Array.map (fun (L(i',_)) -> $"v{i}.v{i'}")
            |> String.concat ", "
            |> return'

        match a with
        | TySizeOf t -> raise_codegen_error $"The following type in `sizeof` is not supported in the Python back end.\nGot: {show_ty t}"
        | TyMacro a ->
            // System.Console.WriteLine $"CodegenPython.TyMacro / a: %A{a}"
            a
            |> List.map (function
                | CMText x when x |> Lib.SpiralSm.starts_with "$\"" ->
                    let x = x |> Lib.SpiralSm.replace "%A{" "{"
                    $"f\"{x.[2..^0]}"
                | CMText x -> x
                | CMTerm x -> tup_data x
                | CMType x -> tup_ty x
                | CMTypeLit a -> type_lit a
            )
            |> String.concat ""
            |> return'
        | TyIf(cond,tr,fl) ->
            line s (sprintf "if %s:" (tup_data cond))
            binds g_decr (indent s) ret tr
            line s "else:"
            binds g_decr (indent s) ret fl
        | TyJoinPoint(a,args) -> return' (jp (a, args))
        | TyBackend(a,b,c) -> return' (backend_handler (a,b,c))
        | TyWhile(a,b) ->
            line s (sprintf "while %s:" (jp a))
            binds g_decr (indent s) (BindsLocal [||]) b
        | TyDo a ->
            binds g_decr s ret a
        | TyIndent a ->
            binds g_decr (indent s) ret a
        | TyIntSwitch(L(v_i,_),on_succ,on_fail) ->
            Array.iteri (fun i x ->
                if i = 0 then line s $"if v{v_i} == {i}:"
                else line s $"elif v{v_i} == {i}:"
                binds g_decr (indent s) ret x
                ) on_succ
            line s "else:"
            binds g_decr (indent s) ret on_fail
        | TyUnionUnbox(is,x,on_succs,on_fail) ->
            let case_tags = x.Item.tags
            line s (sprintf "match %s:" (is |> List.map (fun (L(i,_)) -> $"v{i}") |> String.concat ", "))
            let s = indent s
            let prefix = 
                match x.Item.layout with
                | UHeap -> sprintf "UH%i" (uheap x).tag
                | UStack -> sprintf "US%i" (ustack x).tag
            Map.iter (fun k (a,b) ->
                let i = case_tags.[k]
                let cases = 
                    a |> List.map (fun a ->
                        let x = data_free_vars a
                        let g_decr' = Utils.get_default g_decr (Array.head b) (fun () -> Set.empty)
                        let x,g_decr' = Array.mapFold (fun g_decr (L(i,_) as v) -> if Set.contains v g_decr then "_", Set.remove v g_decr else sprintf "v%i" i, g_decr) g_decr' x
                        g_decr.[Array.head b] <- g_decr'
                        sprintf "%s_%i(%s)" prefix i (String.concat ", " x)
                        )
                    |> String.concat ", "
                line s (sprintf "case %s: # %s" cases k)
                binds g_decr (indent s) ret b
                ) on_succs
            line s "case t:"
            match on_fail with
            | Some b -> binds g_decr (indent s) ret b
            | None -> line (indent s) "raise Exception(f'Pattern matching miss. Got: {t}')"
        | TyUnionBox(a,b,c') ->
            let c = c'.Item
            let i = c.tags.[a]
            let vars = tup_data' b
            match c.layout with
            | UHeap -> sprintf "UH%i_%i(%s)" (uheap c').tag i vars
            | UStack -> sprintf "US%i_%i(%s)" (ustack c').tag i vars
            |> return'
        | TyToLayout(a,b) -> 
            match b with
            | YLayout(_,layout) -> 
                match layout with
                | Heap -> sprintf "Heap%i(%s)" (heap b).tag (tup_data' a)
                | HeapMutable -> sprintf "Mut%i(%s)" (mut b).tag (tup_data' a)
                | StackMutable -> raise_codegen_error "The Python backend doesn't support stack mutable layout types."
            | _ -> raise_codegen_error "Compiler error: Expected a layout type (6)."
            |> return'
        | TyLayoutIndexAll(L(i,YLayout(_,lay) & a)) -> 
            match lay with
            | Heap -> heap a 
            | HeapMutable -> mut a
            | StackMutable -> raise_codegen_error "The Python backend doesn't support indexing into stack mutable layout types."
            |> fun x -> x.free_vars |> layout_index i
        | TyLayoutIndexByKey(L(i,YLayout(_,lay) & a),key) ->
            match lay with
            | Heap -> heap a 
            | HeapMutable -> mut a
            | StackMutable -> raise_codegen_error "The Python backend doesn't support indexing into stack mutable layout types."
            |> fun x ->
                x.free_vars_by_key
                |> Map.tryPick (fun (_, k) v -> if k = key then Some v else None)
                |> Option.iter (layout_index i)
        | TyLayoutIndexAll _ | TyLayoutIndexByKey _ -> raise_codegen_error "Compiler error: Expected the TyV in layout index to be a layout type."
        | TyLayoutMutableSet(L(i,t),b,c) ->
            let a = List.fold (fun s k ->
                match s with
                | DRecord l -> l |> Map.pick (fun (_,k') v -> if k = k' then Some v else None)
                | _ -> raise_codegen_error "Compiler error: Expected a record.") (mut t).data b
            Array.iter2 (fun (L(i',_)) b -> line s $"v{i}.v{i'} = {show_w b}") (data_free_vars a) (data_term_vars c)
        | TyArrayLiteral(a,b) -> return' <| sprintf "(cp if cuda else np).array([%s],dtype=%s)" (List.map tup_data' b |> String.concat ", ") (cupy_ty a)
        | TyArrayCreate(a,b) -> return' $"(cp if cuda else np).empty({tup_data b},dtype={cupy_ty a})" 
        | TyFailwith(a,b) -> line s $"raise Exception({tup_data' b})"
        | TyConv(a,b) -> return' $"{tyv a}({tup_data b})"
        | TyApply(L(i,_),b) -> return' $"v{i}({tup_data' b})"
        | TyArrayLength(a,b) -> return' $"{tup_data b}.__len__()"
        | TyStringLength(a,b) -> return' $"len({tup_data b})"
        | TyOp(Global,[DLit (LitString x)]) -> global' x
        | TyOp(op,l) ->
            match op, l with
            | ToPythonRecord,[DRecord x] -> Map.foldBack (fun k v l -> $"'{k}': {tup_data v}" :: l) x [] |> String.concat ", " |> sprintf "{%s}"
            | ToPythonNamedTuple,[n;DRecord x] -> 
                import "collections"
                let field_names = Map.foldBack (fun k v l -> $"'{k}'" :: l) x [] |> String.concat ", "
                let args = Map.foldBack (fun k v l -> tup_data v :: l) x [] |> String.concat ", "
                $"collections.namedtuple({tup_data n},[{field_names}])({args})"
            | Dyn,[a] -> tup_data a
            | TypeToVar, _ -> raise_codegen_error "The use of `` should never appear in generated code."
            | StringIndex, [a;b] -> sprintf "%s[%s]" (tup_data a) (tup_data b)
            | StringSlice, [a;b;c] -> sprintf "%s[%s:%s]" (tup_data a) (tup_data b) (tup_data c)
            | ArrayIndex, [a;b] -> sprintf "%s[%s].item()" (tup_data a) (tup_data b)
            | ArrayIndexSet, [a;b;c] -> 
                match tup_data' c with
                | "" -> "pass # void array set"
                | c -> sprintf "%s[%s] = %s" (tup_data a) (tup_data b) c
            // Math
            | Add, [a;b] -> sprintf "%s + %s" (tup_data a) (tup_data b)
            | Sub, [a;b] -> sprintf "%s - %s" (tup_data a) (tup_data b)
            | Mult, [a;b] -> sprintf "%s * %s" (tup_data a) (tup_data b)
            | Div, [(DV(L(_,YPrim (Float32T | Float64T))) | DLit(LitFloat32 _ | LitFloat64 _)) & a;b] -> sprintf "%s / %s" (tup_data a) (tup_data b)
            | Div, [a;b] -> sprintf "%s // %s" (tup_data a) (tup_data b)
            | Mod, [a;b] -> sprintf "%s %% %s" (tup_data a) (tup_data b)
            | Pow, [a;b] -> sprintf "pow(%s,%s)" (tup_data a) (tup_data b)
            | LT, [a;b] -> sprintf "%s < %s" (tup_data a) (tup_data b)
            | LTE, [a;b] -> sprintf "%s <= %s" (tup_data a) (tup_data b)
            | EQ, [a;b] -> sprintf "%s == %s" (tup_data a) (tup_data b)
            | NEQ, [a;b] -> sprintf "%s != %s" (tup_data a) (tup_data b)
            | GT, [a;b] -> sprintf "%s > %s" (tup_data a) (tup_data b)
            | GTE, [a;b] -> sprintf "%s >= %s" (tup_data a) (tup_data b)
            | BoolAnd, [a;b] -> sprintf "%s and %s" (tup_data a) (tup_data b)
            | BoolOr, [a;b] -> sprintf "%s or %s" (tup_data a) (tup_data b)
            | BitwiseAnd, [a;b] -> sprintf "%s & %s" (tup_data a) (tup_data b)
            | BitwiseOr, [a;b] -> sprintf "%s | %s" (tup_data a) (tup_data b)
            | BitwiseXor, [a;b] -> sprintf "%s ^ %s" (tup_data a) (tup_data b)
            | BitwiseComplement, [a] -> sprintf "~%s" (tup_data a)

            | ShiftLeft, [a;b] -> sprintf "%s << %s" (tup_data a) (tup_data b)
            | ShiftRight, [a;b] -> sprintf "%s >> %s" (tup_data a) (tup_data b)

            | Neg, [x] -> sprintf "-%s" (tup_data x)
            | Log, [x] -> import "math"; sprintf "math.log(%s)" (tup_data x)
            | Exp, [x] -> import "math"; sprintf "math.exp(%s)" (tup_data x)
            | Tanh, [x] -> import "math"; sprintf "math.tanh(%s)" (tup_data x)
            | Sqrt, [x] -> import "math"; sprintf "math.sqrt(%s)" (tup_data x)
            | Sin, [x] -> import "math"; sprintf "math.sin(%s)" (tup_data x)
            | Cos, [x] -> import "math"; sprintf "math.cos(%s)" (tup_data x)
            | NanIs, [x] -> import "math"; sprintf "math.isnan(%s)" (tup_data x)
            | UnionTag, [DUnion(_,l) | DV(L(_,YUnion l)) as x] -> sprintf "%s.tag" (tup_data x) 
            | _ -> raise_codegen_error <| sprintf "Compiler error: %A with %i args not supported" op l.Length
            |> return'
    and uheap : _ -> UnionRec = union (fun s x ->
        let cases = Array.init x.free_vars.Count (fun i -> $"\"UH{x.tag}_{i}\"") |> function [|x|] -> x | x -> x |> String.concat ", " |> sprintf "Union[%s]"
        fwd_dcls.Add $"UH{x.tag} = {cases}"
        let mutable i = 0
        x.free_vars |> Map.iter (fun k a ->
            line s $"class UH{x.tag}_{i}(NamedTuple): # {k}"
            let s = indent s
            a |> Array.iter (fun (L(i,t)) -> line s $"v{i} : {tyv t}")
            line s $"tag = {i}"
            i <- i+1
            )
        )
    and ustack : _ -> UnionRec = union (fun s x ->
        let mutable i = 0
        x.free_vars |> Map.iter (fun k a ->
            line s $"class US{x.tag}_{i}(NamedTuple): # {k}"
            let s = indent s
            a |> Array.iter (fun (L(i,t)) -> line s $"v{i} : {tyv t}")
            line s $"tag = {i}"
            i <- i+1
            )
        let cases = Array.init x.free_vars.Count (fun i -> $"US{x.tag}_{i}") |> function [|x|] -> x | x -> x |> String.concat ", " |> sprintf "Union[%s]"
        line s $"US{x.tag} = {cases}"
        )
    and heap : _ -> LayoutRec = layout (fun s x -> 
        line s $"class Heap{x.tag}(NamedTuple):"
        let s = indent s
        if x.free_vars.Length = 0 then line s "pass" 
        else x.free_vars |> Array.iter (fun (L(i,t)) -> line s $"v{i} : {tyv t}")
        )
    and mut : _ -> LayoutRec = layout (fun s x -> 
        line s "@dataclass"
        line s $"class Mut{x.tag}:"
        let s = indent s
        if x.free_vars.Length = 0 then line s "pass" 
        else x.free_vars |> Array.iter (fun (L(i,t)) -> line s $"v{i} : {tyv t}")
        )
    and method : _ -> MethodRec =
        jp false (fun ((jp_body,key & (C(args,_))),i) ->
            match (fst env.join_point_method.[jp_body]).[key] with
            | Some a, Some range, _ -> {tag=i; free_vars=rdata_free_vars args; range=range; body=a}
            | _ -> raise_codegen_error "Compiler error: The method dictionary is malformed"
            ) (fun s x ->
            let method_args = x.free_vars |> Array.map (fun (L(i,t)) -> $"v{i} : {tyv t}") |> String.concat ", "
            line s $"def method{x.tag}({method_args}) -> {tup_ty x.range}:"
            binds_start x.free_vars (indent s) x.body
            )
    and closure : _ -> ClosureRec =
        jp true (fun ((jp_body,key & (C(args,_,fun_ty))),i) ->
            match fun_ty with
            | YFun(domain,range,FT_Vanilla) ->
                match (fst env.join_point_closure.[jp_body]).[key] with
                | Some(domain_args, body) -> {tag=i; free_vars=rdata_free_vars args; domain=domain; domain_args=data_free_vars domain_args; range=range; body=body}
                | _ -> raise_codegen_error "Compiler error: The method dictionary is malformed"
            | YFun _ -> raise_codegen_error "Non-standard functions are not supported in the Python backend."
            | _ -> raise_codegen_error "Compiler error: Unexpected type in the closure join point."
            ) (fun s x ->
            let env_args = x.free_vars |> Array.map (fun (L(i,t)) -> $"env_v{i} : {tyv t}") |> String.concat ", "
            line s $"def Closure{x.tag}({env_args}):"
            let s = indent s
            let inner_args = x.domain_args |> Array.map (fun (L(i,t)) -> $"v{i} : {tyv t}") |> String.concat ", "
            line s $"def inner({inner_args}) -> {tup_ty x.range}:"
            let _ =
                let s = indent s
                if x.free_vars.Length > 0 then 
                    let nonlocal_args = x.free_vars |> Array.map (fun (L(i,t)) -> $"env_v{i}") |> String.concat ", "
                    line s $"nonlocal {nonlocal_args}"
                    x.free_vars |> Array.map (fun (L(i,t)) -> $"v{i} = env_v{i}") |> String.concat "; " |> line s
                binds_start x.free_vars s x.body
            line s "return inner"
            )

    import "cupy as cp"
    import "numpy as np"
    from "dataclasses import dataclass"
    from "typing import NamedTuple, Union, Callable, Tuple"
    env.globals.Add "i8 = int; i16 = int; i32 = int; i64 = int; u8 = int; u16 = int; u32 = int; u64 = int; f32 = float; f64 = float; char = str; string = str"
    env.globals.Add "cuda = False"
    env.globals.Add ""

    let main = StringBuilder()
    let s = {text=main; indent=0}
    
    line s "def main_body():"
    binds_start [||] (indent s) x
    s.text.AppendLine() |> ignore

    line s "def main():"
    line (indent s) "r = main_body()"
    line (indent s) "if cuda: cp.cuda.get_current_stream().synchronize() # This line is here so the `__trap()` calls on the kernel aren't missed."
    line (indent s) "return r"
    s.text.AppendLine() |> ignore

    line s "if __name__ == '__main__': result = main(); None if result is None else print(result)"

    let program = StringBuilder()
    env.globals |> Seq.iter (fun x -> program.AppendLine(x) |> ignore)
    fwd_dcls |> Seq.iter (fun x -> program.AppendLine(x) |> ignore)
    types |> Seq.iter (fun x -> program.Append(x) |> ignore)
    functions |> Seq.iter (fun x -> program.Append(x) |> ignore)
    program.Append(main).ToString()

let codegen (default_env : Startup.DefaultEnv) env x = 
    let cuda_kernels = StringBuilder().AppendLine("kernel = r\"\"\"")
    let g = Dictionary(HashIdentity.Structural)
    let globals, fwd_dcls, types, functions, main_defs as ars = ResizeArray(), ResizeArray(), ResizeArray(), ResizeArray(), ResizeArray()

    let codegen = Cuda.codegen default_env ars env
    let python_code =
        codegen' (fun (jp_body,key,r') ->
            let backend_name = (fst jp_body).node
            match backend_name with
            | "Cuda" -> 
                Utils.memoize g (fun (jp_body,key & (C(args,_))) ->
                    let args = rdata_free_vars args
                    match (fst env.join_point_method.[jp_body]).[key] with
                    | Some a, Some _, _ -> codegen args a
                    | _ -> raise_codegen_error "Compiler error: The method dictionary is malformed"
                    string g.Count
                    ) (jp_body,key)
            | x -> raise_codegen_error_backend r' $"The Python + Cuda backend does not support the {x} backend."
            ) env x

    globals |> Seq.iter (fun x -> cuda_kernels.AppendLine(x) |> ignore)
    fwd_dcls |> Seq.iter (fun x -> cuda_kernels.Append(x) |> ignore)
    types |> Seq.iter (fun x -> cuda_kernels.Append(x) |> ignore)
    functions |> Seq.iter (fun x -> cuda_kernels.Append(x) |> ignore)
    main_defs |> Seq.iter (fun x -> cuda_kernels.Append(x) |> ignore)

    cuda_kernels
        .AppendLine("\"\"\"")
        .AppendLine(IO.File.ReadAllText(IO.Path.Join(AppDomain.CurrentDomain.BaseDirectory, "reference_counting.py")))
        .Append(python_code).ToString()

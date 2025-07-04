module Spiral.Codegen.Fsharp
#nowarn 40

open Spiral
open Spiral.Tokenize
open Spiral.Startup
open Spiral.BlockParsing
open Spiral.PartEval.Main
open Spiral.CodegenUtils
open System
open System.Text
open System.Collections.Generic

let private backend_name = "Fsharp"

let lit = function
    | LitInt8 x -> sprintf "%iy" x
    | LitInt16 x -> sprintf "%is" x
    | LitInt32 x -> sprintf "%i" x
    | LitInt64 x -> sprintf "%iL" x
    | LitUInt8 x -> sprintf "%iuy" x
    | LitUInt16 x -> sprintf "%ius" x
    | LitUInt32 x -> sprintf "%iu" x
    | LitUInt64 x -> sprintf "%iUL" x
    | LitFloat32 x -> 
        if x = infinityf then "infinityf"
        elif x = -infinityf then "-infinityf"
        elif Single.IsNaN x then "nanf"
        else x.ToString("R") |> add_dec_point |> sprintf "%sf"
    | LitFloat64 x ->
        if x = infinity then "infinity"
        elif x = -infinity then "-infinity"
        elif Double.IsNaN x then "nan"
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
        | x -> string x
        |> sprintf "'%s'"
    | LitBool x -> if x then "true" else "false"

let prim = function
    | Int8T -> "int8"
    | Int16T -> "int16"
    | Int32T -> "int32"
    | Int64T -> "int64"
    | UInt8T -> "uint8"
    | UInt16T -> "uint16"
    | UInt32T -> "uint32"
    | UInt64T -> "uint64"
    | Float32T -> "float32"
    | Float64T -> "float"
    | BoolT -> "bool"
    | StringT -> "string"
    | CharT -> "char"

let type_lit = function
    | YLit x -> lit x
    | YSymbol x -> x
    | x -> raise_codegen_error "Compiler error: Expecting a type literal in the macro." 

type UnionRec = {tag : int; free_vars : Map<int * string, TyV[]>}
type LayoutRec = {tag : int; data : Data; free_vars : TyV[]; free_vars_by_key : Map<int * string, TyV[]>}
type MethodRec = {tag : int; free_vars : L<Tag,Ty>[]; range : Ty; body : TypedBind[]}
type ClosureRec = {tag : int; free_vars : L<Tag,Ty>[]; domain_args : TyV[]; range : Ty; body : TypedBind[]}

let codegen (env : PartEvalResult) (x : TypedBind []) =
    let types = ResizeArray()
    let functions = ResizeArray()

    let print is_type show r =
        let s = {text=StringBuilder(); indent=0}
        show s r
        let text = s.text.ToString()
        if is_type then types.Add(text) else functions.Add(text)

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
            | _ -> raise_codegen_error $"Compiler error: Expected a layout type (3).\nGot: %s{show_ty x}"
        fun x ->
            let mutable dirty = false
            let r = Utils.memoize dict (Utils.memoize dict' (fun x -> dirty <- true; f x)) x
            if dirty then print true show r
            r

    let union show =
        let dict = Dictionary(HashIdentity.Reference)
        let f (a : Map<int * string,Ty>) : UnionRec = {free_vars=a |> Map.map (fun _ -> env.ty_to_data >> data_free_vars); tag=dict.Count}
        fun x ->
            let mutable dirty = false
            let r = Utils.memoize dict (fun x -> dirty <- true; f x) x
            if dirty then print true show r
            r

    let jp f show =
        let dict = Dictionary(HashIdentity.Structural)
        let f x = f (x, dict.Count)
        fun x ->
            let mutable dirty = false
            let r = Utils.memoize dict (fun x -> dirty <- true; f x) x
            if dirty then print false show r
            r

    let args x = x |> Array.map (fun (L(i,_)) -> sprintf "v%i" i) |> String.concat ", "
    let show_w = function WV (L(i,_)) -> sprintf "v%i" i | WLit a -> lit a
    let args' x = x |> data_term_vars |> Array.map show_w |> String.concat ", "

    let global' =
        let has_added = HashSet env.globals
        fun x -> if has_added.Add(x) then env.globals.Add x

    let rec tyv = function
        | YUnion a -> 
            let a = a.Item
            match a.layout with
            | UHeap -> sprintf "UH%i" (uheap a.cases).tag
            | UStack -> sprintf "US%i" (ustack a.cases).tag
        | YLayout(_,lay) as a -> 
            match lay with
            | Heap -> sprintf "Heap%i" (heap a).tag
            | HeapMutable -> sprintf "Mut%i" (mut a).tag
            | StackMutable -> raise_codegen_error "Compiler error: The F# backend doesn't support stack mutable layout types."
        | YMacro [Text "backend_switch "; Type (YRecord r)] ->
            match r |> Map.tryPick (fun (_, k) v -> if k = backend_name then Some v else None) with
            | Some x -> tup_ty x
            | None -> raise_codegen_error $"In the backend_switch, expected a record with the '{backend_name}' field."
        | YMacro a -> a |> List.map (function Text a -> a | Type a -> tup_ty a | TypeLit a -> type_lit a) |> String.concat ""
        | YPrim a -> prim a
        | YArray a -> sprintf "(%s [])" (tup_ty a)
        | YFun(a,b,FT_Vanilla) -> sprintf "(%s -> %s)" (tup_ty a) (tup_ty b)
        | YExists -> raise_codegen_error "Existentials are not supported at runtime. They are a compile time feature only."
        | YForall -> raise_codegen_error "Foralls are not supported at runtime. They are a compile time feature only."
        | a -> raise_codegen_error $"Type not supported in the codegen.\nGot: %A{a}"
    and args_tys x = x |> Array.map (fun (L(i,t)) -> sprintf "v%i : %s" i (tup_ty t)) |> String.concat ", "
    and binds (s : string_builder_env) (x : TypedBind []) =
        Array.iter (function
            | TyLet(d,trace,a) -> try op s (Some d) a with :? CodegenError as e -> raise_codegen_error' trace (e.Data0,e.Data1)
            | TyLocalReturnOp(trace,a,_) -> try op s None a with :? CodegenError as e -> raise_codegen_error' trace (e.Data0,e.Data1)
            | TyLocalReturnData(d,trace) -> try line s (tup d) with :? CodegenError as e -> raise_codegen_error' trace (e.Data0,e.Data1)
            ) x
    and tup x =
        match data_term_vars x with
        | [||] -> "()"
        | [|x|] -> show_w x
        | x -> Array.map show_w x |> String.concat ", " |> sprintf "struct (%s)"
    and tup_ty x =
        match env.ty_to_data x |> data_free_vars |> Array.map (fun (L(_,x)) -> tyv x) with
        | [||] -> "unit"
        | [|x|] -> x
        | x -> String.concat " * " x |> sprintf "struct (%s)"
    and op s d a =
        let jp (a, b) =
            let args = args b
            match a with
            | JPMethod(a,b) -> sprintf "method%i(%s)" (method (a,b)).tag args
            | JPClosure(a,b) -> sprintf "closure%i(%s)" (closure (a,b)).tag args
        let free_vars do_annot x =
            let f (L(i,t)) = if do_annot then sprintf "v%i : %s" i (tyv t) else sprintf "v%i" i
            match data_free_vars x with
            | [||] -> "()"
            | [|x|] -> f x
            | x -> Array.map f x |> String.concat ", " |> sprintf "struct (%s)"
        let simple x = 
            match d with
            | None -> x
            | Some d -> match free_vars true d with "()" -> x | d -> sprintf "let %s = %s" d x
            |> line s
        let complex f =
            match d with
            | None -> f s : unit
            | Some d -> match free_vars true d with "()" -> f s | d -> line s (sprintf "let %s =" d); f (indent s)
        let layout_vars a =
            let f i x =
                match x with
                | WV(L(i',_)) -> sprintf "l%i = v%i" i i'
                | WLit x -> sprintf "l%i = %s" i (lit x)
            a |> data_term_vars |> Array.mapi f |> String.concat "; "
        let layout_index i x =
            x |> Array.map (fun (L(i',_)) -> sprintf "v%i.l%i" i i')
            |> String.concat ", "
            |> function "" -> () | x -> simple x
        let length (a,b) =
            match a with
            | YPrim UInt8T -> sprintf "System.Convert.ToByte %s.Length" (tup b)
            | YPrim UInt16T -> sprintf "System.Convert.ToUInt16 %s.Length" (tup b)
            | YPrim UInt32T -> sprintf "System.Convert.ToUInt32 %s.Length" (tup b)
            | YPrim UInt64T -> sprintf "System.Convert.ToUInt64 %s.Length" (tup b)
            | YPrim Int8T -> sprintf "System.Convert.ToSByte %s.Length" (tup b)
            | YPrim Int16T -> sprintf "System.Convert.ToInt16 %s.Length" (tup b)
            | YPrim Int32T -> sprintf "%s.Length" (tup b)
            | YPrim Int64T -> sprintf "System.Convert.ToInt64 %s.Length" (tup b)
            | _ -> raise_codegen_error "Compiler error: Expected an int in length"
            |> simple
        match a with
        | TyMacro a -> a |> List.map (function CMText x -> x | CMTerm (x,inl) -> (if inl then args' x else tup x) | CMType x -> tup_ty x | CMTypeLit x -> type_lit x) |> String.concat "" |> simple
        | TySizeOf t -> simple $"sizeof<{tup_ty t}>"
        | TyIf(cond,tr,fl) ->
            complex <| fun s ->
            line s (sprintf "if %s then" (tup cond))
            binds (indent s) tr
            match fl with
            | [|TyLocalReturnData(DB,_)|] -> ()
            | _ ->
                line s "else"
                binds (indent s) fl
        | TyJoinPoint(a,args) -> simple (jp (a, args))
        | TyBackend(_,_,r) -> raise_codegen_error_backend r "The F# backend does not support nesting other backends."
        | TyWhile(a,b) ->
            complex <| fun s ->
            line s (sprintf "while %s do" (jp a))
            binds (indent s) b
        | TyDo a ->
            complex <| fun s ->
            line s "do"
            binds (indent s) a
        | TyIndent a ->
            complex <| fun s ->
            binds (indent s) a
        | TyIntSwitch(L(i,_),on_succ,on_fail) ->
            complex <| fun s ->
            line s (sprintf "match v%i with" i)
            Array.iteri (fun i x ->
                line s (sprintf "| %i ->" i)
                binds (indent s) x
                ) on_succ
            line s "| _ ->"
            binds (indent s) on_fail
        | TyUnionUnbox(is,x,on_succs,on_fail) ->
            complex <| fun s ->
            let case_tags = x.Item.tags
            line s (sprintf "match %s with" (is |> List.map (fun (L(i,_)) -> $"v{i}") |> String.concat ", "))
            let prefix = 
                let x = x.Item
                match x.layout with
                | UHeap -> sprintf "UH%i" (uheap x.cases).tag
                | UStack -> sprintf "US%i" (ustack x.cases).tag
            Map.iter (fun k (a,b) ->
                let i = case_tags.[k]
                let cases = 
                    a |> List.map (fun a ->
                        match data_free_vars a with
                        | [||] -> ""
                        | x -> sprintf "(%s)" (args x)
                        |> sprintf "%s_%i%s" prefix i
                        )
                    |> String.concat ", "
                line s (sprintf "| %s -> (* %s *)" cases k)
                binds (indent s) b
                ) on_succs
            on_fail |> Option.iter (fun b ->
                line s "| _ ->"
                binds (indent s) b
                )
        | TyUnionBox(a,b,c) ->
            let c = c.Item
            let i = c.tags.[a]
            let vars =
                match data_term_vars b with
                | [||] -> ""
                | x -> Array.map show_w x |> String.concat ", " |> sprintf "(%s)"
            match c.layout with
            | UHeap -> sprintf "UH%i_%i%s" (uheap c.cases).tag i vars
            | UStack -> sprintf "US%i_%i%s" (ustack c.cases).tag i vars
            |> simple
        | TyToLayout(a,b) -> 
            let a = layout_vars a
            match b with
            | YLayout(_,layout) -> 
                match layout with
                | Heap -> if a = "" then sprintf "Heap%i()" (heap b).tag else sprintf "{%s} : Heap%i" a (heap b).tag
                | HeapMutable -> if a = "" then sprintf "Mut%i()" (mut b).tag else sprintf "{%s} : Mut%i" a (mut b).tag
                | StackMutable -> raise_codegen_error "The F# backend doesn't support stack mutable layout types."
            | _ -> raise_codegen_error $"Compiler error: Expected a layout type (4).\nGot: %s{show_ty b}"
            |> simple
        | TyLayoutIndexAll(L(i,YLayout(_,lay) & a)) -> 
            match lay with
            | Heap -> heap a 
            | HeapMutable -> mut a 
            | StackMutable -> raise_codegen_error "The F# backend doesn't support indexing into stack mutable layout types."
            |> fun x -> x.free_vars |> layout_index i
        | TyLayoutIndexByKey(L(i,YLayout(_,lay) & a),key) -> 
            match lay with
            | Heap -> heap a
            | HeapMutable -> mut a 
            | StackMutable -> raise_codegen_error "The F# backend doesn't support indexing into stack mutable layout types."
            |> fun x ->
                x.free_vars_by_key
                |> Map.tryPick (fun (_, k) v -> if k = key then Some v else None)
                |> Option.iter (layout_index i)
        | TyLayoutIndexAll _ | TyLayoutIndexByKey _ -> raise_codegen_error "Compiler error: Expected the TyV in layout index to be a layout type."
        | TyLayoutMutableSet(L(i,t),b,c) ->
            let a = List.fold (fun s k ->
                match s with
                | DRecord l -> l |> Map.pick (fun (_,k') v -> if k' = k then Some v else None)
                | _ -> raise_codegen_error "Compiler error: Expected a record.") (mut t).data b
            Array.iter2 (fun (L(i',_)) b ->
                line s (sprintf "v%i.l%i <- %s" i i' (show_w b))
                ) (data_free_vars a) (data_term_vars c)
        | TyArrayLiteral(a,b) -> simple <| sprintf "[|%s|]" (List.map tup b |> String.concat "; ")
        | TyArrayCreate(a,b) ->
            match b with
            | DLit(LitInt32 _) | DV(L(_,YPrim Int32T)) -> simple (sprintf "Array.zeroCreate<%s> (%s)" (tup_ty a) (tup b))
            | _ -> simple (sprintf "Array.zeroCreate<%s> (System.Convert.ToInt32(%s))" (tup_ty a) (tup b))
        | TyArrayLength(a,b) -> length (a,b)
        | TyStringLength(a,b) -> length (a,b)
        | TyFailwith(a,b) -> simple (sprintf "failwith<%s> %s" (tup_ty a) (tup b))
        | TyConv(a,b) ->
            let b = tup b
            match a with
            | YPrim Int8T -> $"int8 {b}"
            | YPrim Int16T -> $"int16 {b}"
            | YPrim Int32T -> $"int32 {b}"
            | YPrim Int64T -> $"int64 {b}"
            | YPrim UInt8T -> $"uint8 {b}"
            | YPrim UInt16T -> $"uint16 {b}"
            | YPrim UInt32T -> $"uint32 {b}"
            | YPrim UInt64T -> $"uint64 {b}"
            | YPrim Float32T -> $"float32 {b}"
            | YPrim Float64T -> $"float {b}"
            | _ -> raise_codegen_error $"Compiler error: Unexpected type in Conv. Got: {show_ty a}"
            |> simple
        | TyApply(L(i,_),b) -> sprintf "v%i %s" i (tup b) |> simple
        | TyOp(Global, [DLit (LitString x)]) -> global' x
        | TyOp(op,l) ->
            match op, l with
            | Dyn,[a] -> tup a
            | TypeToVar, _ -> raise_codegen_error "The use of `` should never appear in generated code."
            | StringIndex, [a;b] -> sprintf "%s.[int %s]" (tup a) (tup b)
            | StringSlice, [a;b;c] -> sprintf "%s.[int %s..int %s]" (tup a) (tup b) (tup c)
            | ArrayIndex, [a;b] -> sprintf "%s.[int %s]" (tup a) (tup b)
            | ArrayIndexSet, [a;b;c] -> sprintf "%s.[int %s] <- %s" (tup a) (tup b) (tup c) 

            // Math
            | Add, [a;b] -> sprintf "%s + %s" (tup a) (tup b)
            | Sub, [a;b] -> sprintf "%s - %s" (tup a) (tup b)
            | Mult, [a;b] -> sprintf "%s * %s" (tup a) (tup b)
            | Div, [a;b] -> sprintf "%s / %s" (tup a) (tup b)
            | Mod, [a;b] -> sprintf "%s %% %s" (tup a) (tup b)
            | Pow, [a;b] -> sprintf "%s ** %s" (tup a) (tup b)
            | LT, [a;b] -> sprintf "%s < %s" (tup a) (tup b)
            | LTE, [a;b] -> sprintf "%s <= %s" (tup a) (tup b)
            | EQ, [a;b] -> sprintf "%s = %s" (tup a) (tup b)
            | NEQ, [a;b] -> sprintf "%s <> %s" (tup a) (tup b)
            | GT, [a;b] -> sprintf "%s > %s" (tup a) (tup b)
            | GTE, [a;b] -> sprintf "%s >= %s" (tup a) (tup b)
            | BoolAnd, [a;b] -> sprintf "%s && %s" (tup a) (tup b)
            | BoolOr, [a;b] -> sprintf "%s || %s" (tup a) (tup b)
            | BitwiseAnd, [a;b] -> sprintf "%s &&& %s" (tup a) (tup b)
            | BitwiseOr, [a;b] -> sprintf "%s ||| %s" (tup a) (tup b)
            | BitwiseXor, [a;b] -> sprintf "%s ^^^ %s" (tup a) (tup b)
            | BitwiseComplement, [a] -> sprintf "~~~%s" (tup a)

            | ShiftLeft, [a;b] -> sprintf "%s <<< %s" (tup a) (tup b)
            | ShiftRight, [a;b] -> sprintf "%s >>> %s" (tup a) (tup b)

            | Neg, [x] -> sprintf " -%s" (tup x)
            | Log, [x] -> sprintf "log %s" (tup x)
            | Exp, [x] -> sprintf "exp %s" (tup x)
            | Tanh, [x] -> sprintf "tanh %s" (tup x)
            | Sqrt, [x] -> sprintf "sqrt %s" (tup x)
            | Sin, [x] -> sprintf "sin %s" (tup x)
            | Cos, [x] -> sprintf "cos %s" (tup x)
            | NanIs, [x] -> 
                match x with
                | DLit(LitFloat32 _) | DV(L(_,YPrim Float32T)) -> sprintf "System.Single.IsNaN(%s)" (tup x)
                | DLit(LitFloat64 _) | DV(L(_,YPrim Float64T)) -> sprintf "System.Double.IsNaN(%s)" (tup x)
                | _ -> raise_codegen_error "Compiler error: Invalid type in NanIs."
            | UnionTag, [DV(L(i,YUnion h))] -> 
                let h = h.Item
                let ty =
                    match h.layout with
                    | UHeap -> sprintf "UH%i" (uheap h.cases).tag
                    | UStack -> sprintf "US%i" (ustack h.cases).tag
                let items =
                    h.cases
                    |> Seq.map (fun (KeyValue ((i, _), _)) ->
                        $"{ty}_{i}, {i}"
                    )
                    |> String.concat "; "
                $"[ {items} ] |> Map |> Map.find v{i}"
            | _ -> raise_codegen_error <| sprintf "Compiler error: %A with %i args not supported" op l.Length
            |> simple
    and heap : _ -> LayoutRec = layout (fun s x ->
        let b = x.free_vars |> Array.map (fun (L(i,t)) -> sprintf "l%i : %s" i (tyv t)) |> String.concat "; "
        if b = "" then line s (sprintf "Heap%i() = class end" x.tag)
        else line s (sprintf "Heap%i = {%s}" x.tag b)
        )
    and mut : _ -> LayoutRec = layout (fun s x ->
        let b = x.free_vars |> Array.map (fun (L(i,t)) -> sprintf "mutable l%i : %s" i (tyv t)) |> String.concat "; "
        if b = "" then line s (sprintf "Mut%i() = class end" x.tag)
        else line s (sprintf "Mut%i = {%s}" x.tag b)
        )
    and uheap : _ -> UnionRec = union (fun s x ->
        line s (sprintf "UH%i =" x.tag)
        let mutable i = 0
        x.free_vars |> Map.iter (fun _ a ->
            match a with
            | [||] -> line (indent s) (sprintf "| UH%i_%i" x.tag i)
            | a -> line (indent s) (sprintf "| UH%i_%i of %s" x.tag i (a |> Array.map (fun (L(_,t)) -> tyv t) |> String.concat " * "))
            i <- i+1
            )
        )
    and ustack : _ -> UnionRec = union (fun s x ->
        line s (sprintf "[<Struct>] US%i =" x.tag)
        let mutable i = 0
        x.free_vars |> Map.iter (fun _ a ->
            match a with
            | [||] -> line (indent s) (sprintf "| US%i_%i" x.tag i)
            | a -> line (indent s) (sprintf "| US%i_%i of %s" x.tag i (a |> Array.mapi (fun i' (L(_,t)) -> sprintf "f%i_%i : %s" i i' (tyv t)) |> String.concat " * "))
            i <- i+1
            )
        )
    and method : _ -> MethodRec =
        jp (fun ((jp_body,key & (C(args,_))),i) ->
            match (fst env.join_point_method.[jp_body]).[key] with
            | Some a, Some range, _ -> {tag=i; free_vars=rdata_free_vars args; range=range; body=a}
            | _ -> raise_codegen_error "Compiler error: The method dictionary is malformed"
            ) (fun s x ->
            line s (sprintf "method%i (%s) : %s =" x.tag (args_tys x.free_vars) (tup_ty x.range))
            binds (indent s) x.body
            )
    and closure : _ -> ClosureRec =
        jp (fun ((jp_body,key & (C(args,_,fun_ty))),i) ->
            match fun_ty with
            | YFun(domain,range,FT_Vanilla) ->
                match (fst env.join_point_closure.[jp_body]).[key] with
                | Some(domain_args, body) -> {tag=i; free_vars=rdata_free_vars args; domain_args=data_free_vars domain_args; range=range; body=body}
                | _ -> raise_codegen_error "Compiler error: The method dictionary is malformed"
            | YFun(_,_,_) -> raise_codegen_error "Non-standard functions are not supported in the F# backend."
            | _ -> raise_codegen_error "Compiler error: Unexpected type in the closure join point."
            ) (fun s x ->
            let domain = 
                match x.domain_args |> Array.map (fun (L(i,t)) -> sprintf "v%i : %s" i (tyv t)) with
                | [||] -> "()"
                | [|x|] -> sprintf "(%s)" x
                | x -> String.concat ", " x |> sprintf "struct (%s)"
            line s (sprintf "closure%i (%s) %s : %s =" x.tag (args_tys x.free_vars) domain (tup_ty x.range))
            binds (indent s) x.body
            )

    let main = StringBuilder()
    binds {text=main; indent=0} x

    let program = StringBuilder()
    env.globals |> Seq.iter (fun (x : string) -> program.AppendLine(x) |> ignore)
    types |> Seq.iteri (fun i x -> program.Append(if i = 0 then "type " else "and ").Append(x) |> ignore)
    functions |> Seq.iteri (fun i x -> program.Append(if i = 0 then "let rec " else "and ").Append(x) |> ignore)
    program.Append(main).ToString()

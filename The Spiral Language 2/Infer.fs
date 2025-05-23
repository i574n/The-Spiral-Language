module Spiral.Infer

open VSCTypes
open Spiral.Startup
open Spiral.BlockParsing
open Polyglot.Common

type [<ReferenceEquality>] 'a ref' = {mutable contents' : 'a}
type TT =
    | KindType
    | KindFun of TT * TT
    | KindMetavar of TT option ref'

type Constraint =
    | CUInt
    | CSInt
    | CInt
    | CFloat
    | CNumber
    | CPrim
    | CSymbol
    | CRecord
    | CPrototype of GlobalId

type ConstraintOrModule = C of Constraint | M of Map<string,ConstraintOrModule>

type [<ReferenceEquality>] Var = {
    scope : int
    constraints : Constraint Set // Must be stated up front and needs to be static in forall vars
    kind : TT // Is not supposed to have metavars.
    name : string // Is what gets printed.
    }

type [<ReferenceEquality>] MVar = {
    mutable scope : int
    mutable constraints : Constraint Set // Must be stated up front and needs to be static in forall vars
    kind : TT // Has metavars, and so is mutable.
    }

type TM =
    | TMText of string
    | TMVar of T
    | TMLitVar of T
and T =
    | TyB
    | TyLit of Tokenize.Literal
    | TyPrim of PrimitiveType
    | TySymbol of string
    | TyPair of T * T
    | TyRecord of Map<int * string, T>
    | TyModule of Map<string, T>
    | TyComment of Comments * T
    | TyFun of T * T * FunType
    | TyArray of T
    | TyNominal of GlobalId
    | TyUnion of Map<int * string,bool * T> * BlockParsing.UnionLayout // The boolean arg determines whether the union case is generalized.
    | TyApply of T * T * TT // Regular type functions (TyInl) get reduced, while this represents the staged reduction of nominals.
    | TyInl of Var * T
    | TyForall of Var * T
    | TyExists of Var list * T
    | TyMetavar of MVar * T option ref
    | TyVar of Var * T option ref
    | TyMacro of TM list
    | TyLayout of T * BlockParsing.Layout

let tyvar x = TyVar(x, ref None)

type TypeError =
    | KindError of TT * TT
    | KindErrorInConstraint of TT * TT
    | ExpectedSymbolAsRecordKey of T
    | ExpectedSymbolAsModuleKey of T
    | UnboundVariable of string
    | UnboundModule
    | ModuleIndexFailedInOpen
    | ModuleIndexWouldShadowLocalVars of string []
    | TermError of T * T
    | TypeVarScopeError of string * T * T
    | RecursiveMetavarsNotAllowed of T * T
    | RecursiveTypevarsNotAllowed of T * T
    | ForallVarConstraintError of string * Constraint Set * Constraint Set
    | MetavarsNotAllowedInRecordWith
    | ExpectedRecord of T
    | ExpectedExistentialInTerm of T
    | ExpectedExistentialInPattern of T
    | UnexpectedNumberOfArgumentsInExistsPattern of got: int * expected: int
    | UnexpectedNumberOfArgumentsInExistsBody of got: int * expected: int
    | ExistsShouldntHaveMetavars of T list
    | ExpectedRecordInsideALayout of T
    | UnionsCannotBeApplied
    | ExpectedNominalInApply of T
    | MalformedNominal
    | LayoutSetMustBeAnnotated
    | ExpectedMutableLayout of T
    | ExpectedRecordAsResultOfIndex of T
    | RecordIndexFailed of string
    | ModuleIndexFailed of string
    | ExpectedModule of T
    | ExpectedSymbol' of T
    | ExpectedSymbolInRecordWith of T
    | RealFunctionInTopDown
    | ModuleMustBeImmediatelyApplied
    | MissingRecordFieldsInPattern of T * string list
    | CasePatternNotFoundForType of GlobalId * string
    | CasePatternNotFound of string
    | CannotInferCasePatternFromTermInEnv of T
    | NominalInPatternUnbox of GlobalId
    | TypeInEnvIsNotNominal of T
    | UnionInPatternNominal of GlobalId
    | ConstraintError of Constraint * T
    | PrototypeConstraintCannotPropagateToMetavar of GlobalId * T
    | PrototypeConstraintCannotPropagateToVar of GlobalId * T
    | ExpectedAnnotation
    | ExpectedSinglePattern
    | RecursiveAnnotationHasMetavars of T
    | ValueRestriction of T
    | DuplicateRecInlName
    | DuplicateKeyInRecUnion
    | ExpectedConstraintInsteadOfModule
    | InstanceNotFound of prototype: GlobalId * instance: GlobalId
    | ExpectedPrototypeConstraint of Constraint
    | ExpectedPrototypeInsteadOfModule
    | ExpectedHigherOrder of T
    | InstanceArityError of prototype_arity: int * instance_arity: int
    | InstanceCoreVarsShouldMatchTheArityDifference of got: int * expected: int
    | InstanceKindError of TT * TT
    | KindNotAllowedInInstanceForall
    | InstanceVarShouldNotMatchAnyOfPrototypes
    | MissingBody
    | MacroIsMissingAnnotation
    | ArrayIsMissingAnnotation
    | ExistsIsMissingAnnotation
    | ShadowedForall
    | ShadowedExists
    | UnionTypesMustHaveTheSameLayout
    | OrphanInstance
    | ShadowedInstance
    | UnusedTypeVariable of string list
    | CompilerError of string
    | IncorrectGADTConstructorType
    | IncorrectRecursiveNominal

let inline shorten'<'a> (x : 'a) link next = 
    let x' : 'a = next x
    if System.Object.ReferenceEquals(x,x') = false then link.contents' <- Some x'
    x'
let rec visit_tt = function
    | KindMetavar({contents'=Some x} & link) -> shorten' x link visit_tt
    | a -> a

let inline shorten<'a> (x : 'a) link next = 
    let x' : 'a = next x 
    if System.Object.ReferenceEquals(x,x') = false then link := Some x'
    x'
let rec visit_t_mvar = function
    | TyComment(_,a) -> visit_t_mvar a
    | TyMetavar(_,{contents=Some x} & link) -> shorten x link visit_t_mvar
    | a -> a

let rec visit_t x = 
    match visit_t_mvar x with
    | TyVar(_,{contents=Some x}) -> visit_t x
    | x -> x

exception TypeErrorException of (VSCRange * TypeError) list

let rec metavars = function
    | RawTTypecase _ | RawTExists _ | RawTFilledNominal _ | RawTMacro _ | RawTVar _ | RawTTerm _ 
    | RawTPrim _ | RawTWildcard _ | RawTLit _ | RawTB _ | RawTSymbol _ -> Set.empty
    | RawTMetaVar(_,a) -> Set.singleton a
    | RawTArray(_,a) | RawTLayout(_,a,_) | RawTForall(_,_,a) -> metavars a
    | RawTPair(_,a,b) | RawTApply(_,a,b) | RawTFun(_,a,b,_) -> metavars a + metavars b
    | RawTUnion(_,l,_,this) -> Map.fold (fun s _ (_,v) -> s + metavars v) (metavars this) l
    | RawTRecord(_,l) -> Map.fold (fun s _ v -> s + metavars v) Set.empty l

type TopEnv = {
    nominals_next_tag : int
    nominals_aux : Map<GlobalId, {|name : string; kind : TT|}>
    nominals : Map<GlobalId, {|vars : Var list; body : T|}>
    prototypes_next_tag : int
    prototypes_instances : Map<GlobalId * GlobalId, Constraint Set list>
    prototypes : Map<GlobalId, {|name : string; signature: T; kind : TT|}>
    ty : Map<string,T>
    term : Map<string,T>
    constraints : Map<string,ConstraintOrModule>
    }

let top_env_empty = {
    nominals_next_tag = 0
    nominals_aux = Map.empty
    nominals = Map.empty
    prototypes_next_tag = 0
    prototypes_instances = Map.empty
    prototypes = Map.empty
    ty = Map.empty
    term = Map.empty
    constraints = Map.empty
    }

let union small big = {
    nominals_next_tag = max small.nominals_next_tag big.nominals_next_tag
    nominals_aux = Map.foldBack Map.add small.nominals_aux big.nominals_aux
    nominals = Map.foldBack Map.add small.nominals big.nominals
    prototypes_next_tag = max small.prototypes_next_tag big.prototypes_next_tag
    prototypes_instances = Map.foldBack Map.add small.prototypes_instances big.prototypes_instances
    prototypes = Map.foldBack Map.add small.prototypes big.prototypes
    ty =
        Map.foldBack (fun k v s ->
            let v =
                match v, s |> Map.tryFind k with
                | TyModule x, Some (TyModule x') -> Map.foldBack Map.add x x' |> TyModule
                | _ -> v
            s |> Map.add k v
        ) small.ty big.ty
    term =
        Map.foldBack (fun k v s ->
            let v =
                match v, s |> Map.tryFind k with
                | TyModule x, Some (TyModule x') -> Map.foldBack Map.add x x' |> TyModule
                | _ -> v
            s |> Map.add k v
        ) small.term big.term
    constraints =
        Map.foldBack (fun k v s ->
            let v =
                match v, s |> Map.tryFind k with
                | M x, Some (M x') -> Map.foldBack Map.add x x' |> M
                | _ -> v
            s |> Map.add k v
        ) small.constraints big.constraints
    }

let in_module m a =
    {a with 
        ty = Map.add m (TyModule a.ty) Map.empty
        term = Map.add m (TyModule a.term) Map.empty
        constraints = Map.add m (M a.constraints) Map.empty
        }

type Env = { ty : Map<string,T>; term : Map<string,T>; constraints : Map<string,ConstraintOrModule> }

let kind_get x = 
    let rec loop = function
        | KindFun(a,b) -> a :: loop b
        | a -> [a]
    let l = loop x 
    {|arity=List.length l; args=l|}

let prototype_init_forall_kind = function
    | TyForall(a,_) -> a.kind
    | _ -> failwith "Compiler error: The prototype should have at least one forall."

let show_primt = function
    | UInt8T -> "u8"
    | UInt16T -> "u16"
    | UInt32T -> "u32"
    | UInt64T -> "u64"
    | Int8T -> "i8"
    | Int16T -> "i16"
    | Int32T -> "i32"
    | Int64T -> "i64"
    | Float32T -> "f32"
    | Float64T -> "f64"
    | BoolT -> "bool"
    | StringT -> "string"
    | CharT -> "char"

let rec constraint_name (env : TopEnv) = function
    | CSInt -> "sint" | CUInt -> "uint" | CInt -> "int"
    | CFloat -> "float" | CNumber -> "number" | CPrim -> "prim"
    | CSymbol -> "symbol" | CRecord -> "record"
    | CPrototype i -> env.prototypes.[i].name

let constraint_kind (env : TopEnv) = function
    | CSInt | CUInt | CInt | CFloat | CNumber | CPrim | CSymbol | CRecord -> KindType
    | CPrototype i -> env.prototypes.[i].kind

let rec tt (env : TopEnv) = function
    | TyComment(_,x) | TyMetavar(_,{contents=Some x}) -> tt env x
    | TyNominal i -> env.nominals_aux.[i].kind
    | TyApply(_,_,x) | TyMetavar({kind=x},_) | TyVar({kind=x},_) -> x
    | TyExists _ | TyLit _ | TyUnion _ | TyLayout _ | TyMacro _ | TyB | TyPrim _ | TyForall _ | TyFun _ | TyRecord _ | TyModule _ | TyPair _ | TySymbol _ | TyArray _ -> KindType
    | TyInl(v,a) -> KindFun(v.kind,tt env a)

let rec has_metavars x =
    let f = has_metavars
    match visit_t x with
    | TyMetavar _ -> true
    | TyVar _ | TyNominal _ | TyB | TyLit _ | TyPrim _ | TySymbol _ | TyModule _ -> false
    | TyExists(_,a) | TyComment(_,a) | TyLayout(a,_) | TyForall(_,a) | TyInl(_,a) | TyArray a -> f a
    | TyApply(a,b,_) | TyFun(a,b,_) | TyPair(a,b) -> f a || f b
    | TyUnion(l,_) -> Map.exists (fun _ -> snd >> f) l
    | TyRecord l -> Map.exists (fun _ -> f) l
    | TyMacro a -> List.exists (function TMVar x -> has_metavars x | _ -> false) a

// Eliminates the metavars in the type if possible.
let term_subst a =
    let h = System.Collections.Generic.HashSet(HashIdentity.Reference)
    // 'a = 'b = ('c = int * 'd = float)
    // visit_t shortens to:
    // 'a = ('c = int * 'd = float)
    // visit_t returns:
    // ('c = int * 'd = float)
    // term_subst returns:
    // int * float
    let inline g a f =
        let _ = h.Add(a)
        let x = f()
        let _ = h.Remove(a)
        x
    let rec f a =
        match visit_t a with
        | TyMetavar _ | TyNominal _ | TyB | TyLit _ | TyPrim _ | TySymbol _ as x -> x
        | TyVar(x,r) -> TyVar(x, if h.Contains x then ref None else r)
        | TyComment(a,b) -> TyComment(a,f b)
        | TyPair(a,b) -> TyPair(f a, f b)
        | TyRecord a -> TyRecord(Map.map (fun _ -> f) a)
        | TyModule a -> TyModule(Map.map (fun _ -> f) a)
        | TyUnion(a,b) -> TyUnion(Map.map (fun _ (is_gadt, x) -> is_gadt, f x) a,b)
        | TyFun(a,b,t) -> TyFun(f a, f b, t)
        | TyForall(a,b) -> g a <| fun () -> TyForall(a,f b)
        | TyExists(a,b) -> TyExists(a,f b)
        | TyArray a -> TyArray(f a)
        | TyApply(a,b,c) -> TyApply(f a, f b, c)
        | TyInl(a,b) -> g a <| fun () -> TyInl(a,f b)
        | TyMacro a -> TyMacro(List.map (function TMVar x -> TMVar(f x) | x -> x) a)
        | TyLayout(a,b) -> TyLayout(f a,b)
    f a

type HoverTypes() =
    // This is to allocate less trash for code that doesn't use GADTs. 
    // Unfortunately, we cannot use memoization instead as term_subst is not functionally pure.
    let rec has_substituted_tvars x = 
        let f = has_substituted_tvars
        match x with
        | TyMetavar(_,{contents=Some _}) -> true
        | TyVar (_, {contents=Some x}) | TyComment(_,x) -> f x
        | TyVar _ | TyMetavar _ | TyNominal _ | TyB | TyLit _ | TyPrim _ | TySymbol _ | TyModule _ -> false
        | TyExists(_,a) | TyComment(_,a) | TyLayout(a,_) | TyForall(_,a) | TyInl(_,a) | TyArray a -> f a
        | TyApply(a,b,_) | TyFun(a,b,_) | TyPair(a,b) -> f a || f b
        | TyUnion(l,_) -> Map.exists (fun _ -> snd >> f) l
        | TyRecord l -> Map.exists (fun _ -> f) l
        | TyMacro a -> List.exists (function TMVar x -> has_metavars x | _ -> false) a
    let hover_types = ResizeArray()
    member _.AddHover(r,(x,com)) = hover_types.Add(r,((if has_substituted_tvars x then term_subst x else x), com))
    member _.ToArray() = hover_types.ToArray()

let module_open (hover_types : HoverTypes option) (top_env : Env) (local_env_ty : Map<string,T>) (r : VSCRange) b l =
    let tryFind env x =
        match Map.tryFind x env.term, Map.tryFind x env.ty, Map.tryFind x env.constraints with
        | Some (TyModule a), Some (TyModule b), Some (M c) -> ValueSome {term=a; ty=b; constraints=c}
        | _ -> ValueNone
    match tryFind top_env b with
    | ValueNone -> Error(r, UnboundModule)
    | ValueSome env ->
        hover_types |> Option.iter (fun hover_types -> hover_types.AddHover(r,(TyModule env.term,"")))
        let rec loop env = function
            | (r,x) :: x' ->
                match tryFind env x with
                | ValueSome env -> 
                    hover_types |> Option.iter (fun hover_types -> hover_types.AddHover(r,(TyModule env.term,"")))
                    loop env x'
                | _ -> Error(r, ModuleIndexFailedInOpen)
            | [] -> Ok env
        loop env l |> Result.bind (fun env ->
            let h = ResizeArray()
            local_env_ty |> Map.iter (fun k _ -> if env.ty.ContainsKey k then h.Add k)
            if h.Count > 0 then Error(r, ModuleIndexWouldShadowLocalVars(h.ToArray()))
            else Ok env
            )


let validate_bound_vars (top_env : Env) constraints term ty x =
    let errors = ResizeArray()
    let check_term term (a,b) = if Set.contains b term = false && Map.containsKey b top_env.term = false then errors.Add(a,UnboundVariable b)
    let check_ty ty (a,b) = if Set.contains b ty = false && Map.containsKey b top_env.ty = false then errors.Add(a,UnboundVariable b)
    let check_cons constraints (a,b) = 
        match Map.tryFind b constraints |> Option.orElseWith (fun () -> Map.tryFind b top_env.constraints) with
        | Some (C _) -> ()
        | Some (M _) -> errors.Add(a,ExpectedConstraintInsteadOfModule)
        | None -> errors.Add(a,UnboundVariable b)
    let rec cterm constraints (term, ty) x =
        match x with
        | RawSymbol _ | RawDefaultLit _ | RawLit _ | RawB _ -> ()
        | RawV(a,b,_) -> check_term term (a,b)
        | RawType(_,x) -> ctype constraints term ty x
        | RawMatch(_,body,l) -> cterm constraints (term, ty) body; List.iter (fun (a,b) -> cterm constraints (cpattern constraints term ty a) b) l
        | RawFun(_,l) -> List.iter (fun (a,b) -> cterm constraints (cpattern constraints term ty a) b) l
        | RawForall(_,((_,(a,_)),l),b) -> List.iter (check_cons constraints) l; cterm constraints (term, Set.add a ty) b
        | RawFilledForall _ -> failwith "Compiler error: Should not appear during variable validation."
        | RawRecBlock(_,l,on_succ) -> 
            let term = List.fold (fun s ((_,x),_) -> Set.add x s) term l
            List.iter (fun (_,x) -> cterm constraints (term, ty) x) l
            cterm constraints (term, ty) on_succ
        | RawRecordWith(_,a,b,c) ->
            List.iter (cterm constraints (term, ty)) a
            List.iter (function
                | RawRecordWithSymbol(_,e) | RawRecordWithSymbolModify(_,e) -> cterm constraints (term, ty) e
                | RawRecordWithInjectVar(v,e) | RawRecordWithInjectVarModify(v,e) -> check_term term v; cterm constraints (term, ty) e
                ) b
            List.iter (function RawRecordWithoutSymbol _ -> () | RawRecordWithoutInjectVar (a,b) -> check_term term (a,b)) c
        | RawOp(_,_,l) -> List.iter (cterm constraints (term, ty)) l
        | RawReal(_,x) | RawJoinPoint(_,_,x,_) -> cterm constraints (term, ty) x
        | RawExists(_,(_,a),b) -> Option.iter (List.iter (ctype constraints term ty)) a; cterm constraints (term, ty) b
        | RawAnnot(_,RawMacro(_,a),b) -> cmacro constraints term ty a; ctype constraints term ty b
        | RawMacro(r,a) -> errors.Add(r,MacroIsMissingAnnotation); cmacro constraints term ty a
        | RawAnnot(_,RawArray(_,a),b) -> List.iter (cterm constraints (term, ty)) a; ctype constraints term ty b
        | RawArray(r,a) -> errors.Add(r,ArrayIsMissingAnnotation); List.iter (cterm constraints (term, ty)) a
        | RawAnnot(_,a,b) -> cterm constraints (term, ty) a; ctype constraints term ty b
        | RawTypecase(_,a,b) -> 
            ctype constraints term ty a
            List.iter (fun (a,b) -> 
                ctype constraints term ty a
                cterm constraints (term, ty + metavars a) b
                ) b
        | RawOpen(_,(a,b),l,on_succ) ->
            match module_open None top_env Map.empty a b l with
            | Ok x ->
                let combine e m = Map.fold (fun s k _ -> Set.add k s) e m
                cterm (Map.foldBack Map.add x.constraints constraints) (combine term x.term, combine ty x.ty) on_succ
            | Error e -> errors.Add(e)
        | RawHeapMutableSet(_,a,b,c) -> cterm constraints (term, ty) a; List.iter (cterm constraints (term, ty)) b; cterm constraints (term, ty) c
        | RawSeq(_,a,b) | RawPair(_,a,b) | RawIfThen(_,a,b) | RawApply(_,a,b) -> cterm constraints (term, ty) a; cterm constraints (term, ty) b
        | RawIfThenElse(_,a,b,c) -> cterm constraints (term, ty) a; cterm constraints (term, ty) b; cterm constraints (term, ty) c
        | RawMissingBody r -> errors.Add(r,MissingBody)
    and cmacro constraints term ty a =
        List.iter (function
            | RawMacroText _ -> ()
            | RawMacroTerm(r,a,_) -> cterm constraints (term, ty) a
            | RawMacroType(r,a) | RawMacroTypeLit(r,a) -> ctype constraints term ty a
            ) a
    and ctype constraints term ty x =
        match x with
        | RawTFilledNominal(_,_) | RawTPrim _ | RawTWildcard _ | RawTLit _ | RawTB _ | RawTSymbol _ | RawTMetaVar _ -> ()
        | RawTTypecase(_,a,b) -> 
            ctype constraints term ty a
            List.iter (fun (a,b) -> 
                ctype constraints term ty a
                ctype constraints term (ty + metavars a) b
                ) b
        | RawTVar(a,b) -> check_ty ty (a,b)
        | RawTArray(_,a) | RawTLayout(_,a,_) -> ctype constraints term ty a
        | RawTPair(_,a,b) | RawTApply(_,a,b) | RawTFun(_,a,b,_) -> ctype constraints term ty a; ctype constraints term ty b
        | RawTUnion(_,l,_,this) -> Map.iter (fun _ (_,x) -> ctype constraints term ty x) l; ctype constraints term ty this
        | RawTRecord(_,l) -> Map.iter (fun _ -> ctype constraints term ty) l
        | RawTForall(_,((_,(a,_)),l),b) -> List.iter (check_cons constraints) l; ctype constraints term (Set.add a ty) b
        | RawTExists(_,a,b) -> 
            let ty =
                List.fold (fun ty ((_,(a,_)),l) ->
                    List.iter (check_cons constraints) l
                    Set.add a ty
                    ) ty a
            ctype constraints term ty b
        | RawTTerm (_,a) -> cterm constraints (term, ty) a
        | RawTMacro(_,a) -> cmacro constraints term ty a
    and cpattern constraints term ty x =
        //let is_first = System.Collections.Generic.HashSet()
        let rec loop (term, ty) x = 
            let f = loop (term, ty)
            match x with
            | PatDefaultValue _ | PatFilledDefaultValue _ | PatValue _ | PatSymbol _ | PatB _ | PatE _ -> term, ty
            | PatExists(_,a,b) ->
                let ty = List.fold (fun s (_,x) -> Set.add x s) ty a
                loop (term, ty) b
            | PatVar(_,b) -> 
                //if is_first.Add b then () // TODO: I am doing it like this so I can reuse this code later for variable highlighting.
                Set.add b term, ty
            | PatDyn(_,x) | PatUnbox(_,_,x) -> f x
            | PatPair(_,a,b) -> loop (f a) b
            | PatRecordMembers(_,l) ->
                List.fold (fun s -> function
                    | PatRecordMembersSymbol(_,x) -> loop s x
                    | PatRecordMembersInjectVar((a,b),x) -> check_term term (a,b); loop s x
                    ) (term, ty) l
            | PatAnd(_,a,b) | PatOr(_,a,b) -> loop (loop (term, ty) a) b
            | PatAnnot(_,a,b) -> ctype constraints term ty b; f a
            | PatWhen(_,a,b) -> let r = f a in cterm constraints r b; r
            | PatNominal(_,(r,a),_,b) -> check_ty ty (r,a); f b
            | PatArray(_,a) -> List.fold loop (term, ty) a
        loop (term, ty) x

    match x with
    | Choice1Of2 x -> cterm constraints (term, ty) x
    | Choice2Of2 x -> ctype constraints term ty x
    errors

let assert_bound_vars (top_env : Env) constraints term ty x =
    let errors = validate_bound_vars top_env constraints term ty x
    if 0 < errors.Count then raise (TypeErrorException (Seq.toList errors))

let rec subst (m : (Var * T) list) x =
    let f = subst m
    if List.isEmpty m then x 
    else 
        match x with
        | TyComment(_,x)
        | TyMetavar(_,{contents=Some x}) 
        | TyVar (_,{contents=Some x}) -> f x // Don't do path shortening here.
        | TyMetavar _ | TyNominal _ | TyB | TyLit _ | TyPrim _ | TySymbol _ -> x
        | TyPair(a,b) -> TyPair(f a, f b)
        | TyRecord a -> TyRecord(Map.map (fun _ -> f) a)
        | TyModule a -> TyModule(Map.map (fun _ -> f) a)
        | TyUnion(a,b) -> TyUnion(Map.map (fun _ (is_gadt,body) -> is_gadt, f body) a,b)
        | TyFun(a,b,t) -> TyFun(f a, f b, t)
        | TyArray a -> TyArray(f a)
        | TyApply(a,b,c) -> TyApply(f a, f b, c)
        | TyVar (a,_) -> List.tryPick (fun (v,x) -> if a = v then Some x else None) m |> Option.defaultValue x
        | TyForall(a,b) -> TyForall(a, f b)
        | TyExists(a,b) -> TyExists(a, f b)
        | TyInl(a,b) -> TyInl(a, f b)
        | TyMacro a -> TyMacro(List.map (function TMVar x -> TMVar(f x) | x -> x) a)
        | TyLayout(a,b) -> TyLayout(f a,b)

let type_apply_split x =
    let rec loop s x =
        match visit_t x with
        | TyApply(a,b,_) -> loop (b :: s) a
        | x -> x, s
    loop [] x

let rec kind_subst = function
    | KindMetavar ({contents'=Some x} & link) -> shorten' x link kind_subst
    | KindMetavar _ | KindType as x -> x
    | KindFun(a,b) -> KindFun(kind_subst a,kind_subst b)

let rec foralls_get = function
    | RawForall(_,a,b) -> let a', b = foralls_get b in a :: a', b
    | b -> [], b

let rec foralls_ty_get = function
    | TyForall(a,b) -> let a', b = foralls_ty_get b in a :: a', b
    | b -> [], b

let rec kind_force = function
    | KindMetavar ({contents'=Some x} & link) -> shorten' x link kind_force
    | KindMetavar link -> let x = KindType in link.contents' <- Some x; x
    | KindType as x -> x
    | KindFun(a,b) -> KindFun(kind_force a,kind_force b)

let p prec prec' x = if prec < prec' then x else sprintf "(%s)" x
let show_kind x =
    let rec f prec x =
        let p = p prec
        match x with
        | KindMetavar {contents'=Some x} -> f prec x
        | KindMetavar _ -> "?"
        | KindType -> "*"
        | KindFun(a,b) -> p 20 (sprintf "%s -> %s" (f 20 a) (f 19 b))
    f -1 x

let show_constraints env x = Set.toList x |> List.map (constraint_name env) |> String.concat "; " |> sprintf "{%s}"
let show_nominal (env : TopEnv) i = match Map.tryFind i env.nominals_aux with Some x -> x.name | None -> "?"
let show_layout_type = function Heap -> "heap" | HeapMutable -> "mut" | StackMutable -> "stack_mut"

let show_t (env : TopEnv) x =
    let show_var (a : Var) =
        let n = match a.kind with KindType -> a.name | _ -> sprintf "(%s : %s)" a.name (show_kind a.kind)
        if Set.isEmpty a.constraints then n
        else sprintf "%s %s" n (show_constraints env a.constraints)
    let rec f prec x =
        let p = p prec
        match x with
        | TyVar (_, {contents=Some x}) | TyComment(_,x) | TyMetavar(_,{contents=Some x}) -> f prec x
        | TyMetavar _ -> "_"
        | TyVar (a, _) -> a.name
        | TyNominal i -> 
            match Map.tryFind i env.nominals_aux with
            | Some x when prec < 0 -> sprintf "(%s : %s)" x.name (show_kind x.kind)
            | Some x -> x.name
            | _ -> "?"
        | TyB -> "()"
        | TyLit x -> Tokenize.show_lit x
        | TyPrim x -> show_primt x
        | TySymbol x -> sprintf ".%s" x
        | TyExists(a,b) -> 
            let a = List.map show_var a |> String.concat " "
            p 0 (sprintf "exists %s. %s" a (f -1 b))
        | TyForall _ ->
            let a, b =
                let rec loop = function
                    | TyForall(a,b) -> let a',b = loop b in (a :: a'), b
                    | b -> [], b
                loop x
            let a = List.map show_var a |> String.concat " "
            p 0 (sprintf "forall %s. %s" a (f -1 b))
        | TyInl _ -> 
            let a, b =
                let rec loop = function
                    | TyInl(a,b) -> let a',b = loop b in (a :: a'), b
                    | b -> [], b
                loop x
            let a = List.map show_var a |> String.concat " "
            p 0 (sprintf "%s => %s" a (f -1 b))
        | TyArray a -> p 30 (sprintf "array_base %s" (f 30 a))
        | TyApply(a,b,_) -> p 30 (sprintf "%s %s" (f 29 a) (f 30 b))
        | TyPair(a,b) -> p 25 (sprintf "%s * %s" (f 25 a) (f 24 b))
        | TyFun(a,b,FT_Vanilla) -> p 20 (sprintf "%s -> %s" (f 20 a) (f 19 b))
        | TyFun(a,b,FT_Pointer) -> p 20 (sprintf "fptr (%s -> %s)" (f 20 a) (f 19 b))
        | TyFun(a,b,FT_Closure) -> p 20 (sprintf "closure (%s -> %s)" (f 20 a) (f 19 b))
        | TyModule l -> 
            if prec = -2 then 
                l |> Map.toList |> List.map (fun (k,v) -> 
                    let a,b = k, f -1 v
                    match v with
                    | TyComment(com,_) -> sprintf "%s : %s\n---\n%s\n---\n" a b com
                    | _ -> sprintf "%s : %s" a b
                    )
                |> String.concat "\n"
            else "module"
        | TyRecord l -> sprintf "{%s}" (l |> Map.toList |> List.map (fun ((_,k),v) -> sprintf "%s : %s" k (f -1 v)) |> String.concat "; ")
        | TyUnion(l,_) -> sprintf "{%s}" (l |> Map.toList |> List.map (fun ((_,k),(_,v)) -> sprintf "%s : %s" k (f -1 v)) |> String.concat "| ")
        | TyMacro a -> p 30 (List.map (function TMLitVar a | TMVar a -> f -1 a | TMText a -> a) a |> String.concat "")
        | TyLayout(a,b) -> p 30 (sprintf "%s %s" (show_layout_type b) (f 30 a))

    f -2 x

let show_type_error (env : TopEnv) x =
    let f = show_t env
    match x with
    | IncorrectRecursiveNominal -> "The non-recursive nominals should not use their own type in the clause."
    | IncorrectGADTConstructorType -> "The GADT case in the union has to result in an instance of the union being constructed. Any type other than the self being in the range of the union is not allowed."
    | ExistsShouldntHaveMetavars a -> sprintf "The variables of the exists body shouldn't have metavariables left over in them.\nGot: [%s]"  (List.map f a |> String.concat ", ")
    | ExpectedExistentialInPattern a -> sprintf "The variable being destructured in the pattern match need to be explicitly annotated and with an existential type.\nGot: %s" (f a)
    | UnexpectedNumberOfArgumentsInExistsPattern(got,exp) -> sprintf "The number of arguments in the existential pattern doesn't match the one in the type.\nGot: %i\nExpected: %i" got exp
    | UnexpectedNumberOfArgumentsInExistsBody(got,exp) -> sprintf "The number of arguments in the existential body doesn't match the one in the type.\nGot: %i\nExpected: %i" got exp
    | ExpectedExistentialInTerm a -> sprintf "The body of `expects` need to be explicitly annotated and with an existential type.\nGot: %s" (f a)
    | UnionsCannotBeApplied -> "Unions cannot be applied."
    | ExpectedNominalInApply a -> sprintf "Expected a nominal.\nGot: %s" (f a)
    | MalformedNominal -> "Malformed nominal."
    | ModuleMustBeImmediatelyApplied -> "Module must be immediately applied."
    | ExpectedSymbol' a -> sprintf "Expected a symbol.\nGot: %s" (f a)
    | KindError(a,b) -> sprintf "Kind unification failure.\nGot:      %s\nExpected: %s" (show_kind a) (show_kind b)
    | KindErrorInConstraint(a,b) -> sprintf "Kind unification failure when propagating them from constraints.\nGot:      %s\nExpected: %s" (show_kind a) (show_kind b)
    | TermError(a,b) -> sprintf "Unification failure.\nGot:      %s\nExpected: %s" (f a) (f b)
    | RecursiveMetavarsNotAllowed(a,b) -> sprintf "Recursive metavariables are not allowed. A metavar cannot be unified with a type that has itself.\nGot:      %s\nExpected: %s" (f a) (f b)
    | RecursiveTypevarsNotAllowed(a,b) -> sprintf "Recursive type variables are not allowed. A type variable cannot be unified with a type that has itself.\nGot:      %s\nExpected: %s" (f a) (f b)
    | ExpectedSymbolAsRecordKey a -> sprintf "Expected symbol as a record key.\nGot: %s" (f a)
    | ExpectedSymbolAsModuleKey a -> sprintf "Expected symbol as a module key.\nGot: %s" (f a)
    | UnboundVariable x -> sprintf "Unbound variable: %s." x
    | UnboundModule -> sprintf "Unbound module."
    | ModuleIndexFailedInOpen -> sprintf "Module does not have a submodule with that key."
    | ModuleIndexWouldShadowLocalVars [|v|] -> $"The module open would shadow a local variable: {v}."
    | ModuleIndexWouldShadowLocalVars vars -> let v = String.concat ", " vars in $"The module open would shadow the local variables: {v}."
    | TypeVarScopeError(a,_,_) -> sprintf "Tried to unify the type variable %s with a metavar outside its scope." a
    | ForallVarConstraintError(n,a,b) -> sprintf "Metavariable's constraints must be a subset of the forall var %s's.\nGot: %s\nExpected: %s" n (show_constraints env a) (show_constraints env b)
    | MetavarsNotAllowedInRecordWith -> sprintf "In the top-down segment the record keys need to be fully known. Please add an annotation."
    | LayoutSetMustBeAnnotated -> sprintf "The layout type being set must be annotated."
    | ExpectedMutableLayout a -> sprintf "Expected a mutable layout type.\nGot: %s" (f a)
    | ExpectedRecord a -> sprintf "Expected a record.\nGot: %s" (f a)
    | ExpectedRecordInsideALayout a -> sprintf "Expected a record inside a layout type.\nGot: %s" (f a)
    | ExpectedRecordAsResultOfIndex a -> sprintf "Expected a record as result of index.\nGot: %s" (f a)
    | RecordIndexFailed a -> sprintf "The record does not have the key: %s" a
    | ModuleIndexFailed a -> sprintf "The module does not have the key: %s" a
    | ExpectedModule a -> sprintf "Expected a module.\nGot: %s" (f a)
    | ExpectedSymbolInRecordWith a -> sprintf "Expected a symbol.\nGot: %s" (f a)
    | RealFunctionInTopDown -> sprintf "Real segment functions are forbidden in the top-down segment. They can only be used in `real` expressions or .spir modules."
    | MissingRecordFieldsInPattern(a,b) -> sprintf "The record is missing the following fields: %s.\nGot: %s" (String.concat ", " b) (f a)
    | CasePatternNotFoundForType(i,n) -> sprintf "%s does not have the %s case." (show_nominal env i) n
    | CasePatternNotFound n -> sprintf "Cannot find a function with the same name as the %s case in the environment." n
    | CannotInferCasePatternFromTermInEnv a -> sprintf "Cannot infer the higher order type that has this case from the following type.\nGot: %s" (f a)
    | NominalInPatternUnbox i -> sprintf "Expected an union type, but %s is a nominal." (show_nominal env i)
    | TypeInEnvIsNotNominal a -> sprintf "Expected a nominal type.\nGot: %s" (f a)
    | UnionInPatternNominal i -> sprintf "Expected a nominal type, but %s is an union." (show_nominal env i)
    | ConstraintError(a,b) -> sprintf "Constraint satisfaction error.\nGot: %s\nFails to satisfy: %s" (f b) (constraint_name env a)
    | ExpectedAnnotation -> sprintf "Recursive functions with foralls must be fully annotated."
    | ExpectedSinglePattern -> sprintf "Recursive functions with foralls must not have multiple clauses in their patterns."
    | RecursiveAnnotationHasMetavars a -> sprintf "Recursive functions with foralls must not have metavars.\nGot: %s" (f a)
    | ValueRestriction a -> sprintf "Metavars that are not part of the enclosing function's signature are not allowed. They need to be values.\nGot: %s" (f a)
    | DuplicateRecInlName -> "Shadowing of functions by the members of the same mutually recursive block is not allowed."
    | DuplicateKeyInRecUnion -> "Mutually recursive unions should not have overlapping keys."
    | ExpectedConstraintInsteadOfModule -> sprintf "Expected a constraint instead of module."
    | InstanceNotFound(prot,ins) -> sprintf "The higher order type instance %s does not have the prototype %s." (show_nominal env ins) env.prototypes.[prot].name
    | ExpectedPrototypeConstraint a -> sprintf "Expected a prototype constraint.\nGot: %s" (constraint_name env a)
    | PrototypeConstraintCannotPropagateToMetavar(a,b) -> sprintf "Cannot propagate the %s prototype constraint to the applied metavariable as the kinds would not match. If this is not intended to be a type var, provide a type annotation to a concrete type.\nGot: %s" env.prototypes.[a].name (f b)
    | PrototypeConstraintCannotPropagateToVar(a,b) -> sprintf "Cannot propagate the %s prototype constraint to the applied type variable as the kinds would not match.\nGot: %s" env.prototypes.[a].name (f b)
    | ExpectedPrototypeInsteadOfModule -> "Expected a prototype instead of module."
    | ExpectedHigherOrder a -> sprintf "Expected a higher order type.\nGot: %s" (f a)
    | InstanceArityError(prot,ins) -> sprintf "The arity of the instance must be greater or equal to that of the prototype.\nInstance arity:  %i\nPrototype arity: %i" ins prot
    | InstanceCoreVarsShouldMatchTheArityDifference(num_vars,expected) -> sprintf "The number of forall variables in the instance needs to be specified so it equals the difference between the instance arity and the prototype arity.\nInstance variables specified: %i\nExpected:                     %i" num_vars expected
    | InstanceKindError(a,b) -> sprintf "The kinds of the instance foralls are incompatible with those of the prototype.\nGot:      %s\nExpected: %s" (show_kind a) (show_kind b)
    | KindNotAllowedInInstanceForall -> "Kinds should not be explicitly stated in instance foralls."
    | InstanceVarShouldNotMatchAnyOfPrototypes -> "Instance forall must not have the same name as any of the prototype foralls."
    | MissingBody -> "The function body is missing."
    | MacroIsMissingAnnotation -> "The macro needs an annotation."
    | ArrayIsMissingAnnotation -> "The array needs an annotation."
    | ExistsIsMissingAnnotation -> "The existential type needs an annotation."
    | ShadowedForall -> "Shadowing of foralls is not allowed in the top-down segment."
    | ShadowedExists -> "Shadowing of existential type variables is not allowed in the top-down segment."
    | UnionTypesMustHaveTheSameLayout -> "The two union types must have the same layout."
    | OrphanInstance -> "The instance has to be defined in the same package as either the prototype or the nominal."
    | ShadowedInstance -> "The instance cannot be defined twice."
    | UnusedTypeVariable [x] -> sprintf "The type variable %s is unused in the function's type signature." x
    | UnusedTypeVariable vars -> sprintf "The type variables %s are unused in the function's type signature." (vars |> String.concat ", ")
    | CompilerError x -> x

let loc_env (x : TopEnv) = {term=x.term; ty=x.ty; constraints=x.constraints}
let names_of vars = List.map (fun x -> x.name) vars |> Set

open Spiral.Tokenize
let lit = function
    | LitUInt8 _ -> TyPrim UInt8T
    | LitUInt16 _ -> TyPrim UInt16T
    | LitUInt32 _ -> TyPrim UInt32T
    | LitUInt64 _ -> TyPrim UInt64T
    | LitInt8 _ -> TyPrim Int8T
    | LitInt16 _ -> TyPrim Int16T
    | LitInt32 _ -> TyPrim Int32T
    | LitInt64 _ -> TyPrim Int64T
    | LitFloat32 _ -> TyPrim Float32T
    | LitFloat64 _ -> TyPrim Float64T
    | LitBool _ -> TyPrim BoolT
    | LitString _ -> TyPrim StringT
    | LitChar _ -> TyPrim CharT

let autogen_name_in_fun (i : int) = let x = char i + 'a' in if 'z' < x then sprintf "'%i" i else sprintf "'%c" x
let autogen_name_in_typecase (i : int) = let x = char i + 'a' in if 'z' < x then sprintf "'t%i" i else sprintf "'t%c" x
let trim_kind = function KindFun(_,k) -> k | _ -> failwith "impossible"

// Similar to BundleTop except with type annotations and type application filled in.
type FilledTop =
    | FType of VSCRange * RString * HoVar list * RawTExpr
    | FNominal of VSCRange * RString * HoVar list * RawTExpr
    | FNominalRec of (VSCRange * RString * HoVar list * RawTExpr) list
    | FInl of VSCRange * RString * RawExpr
    | FRecInl of (VSCRange * RString * RawExpr) list
    | FPrototype of VSCRange * RString * RString * TypeVar list * RawTExpr
    | FInstance of VSCRange * RGlobalId * RGlobalId * RawExpr
    | FOpen of VSCRange * RString * RString list

type 'a AdditionType =
    | AOpen of 'a
    | AInclude of 'a

type Scope = int

open System.Collections.Generic
type [<ReferenceEquality>] InferResult = {
    filled_top : FilledTop Hopac.Promise
    top_env_additions : TopEnv AdditionType
    offset : int
    hovers : RString []
    errors : RString list
    }

let dispose_gadt_links gadt_links = Seq.iter (fun x -> x := None) gadt_links

let assert_foralls_used' (errors : (VSCRange * TypeError) ResizeArray) outside_foralls r x =
    let h = HashSet()
    let rec f x =
        match visit_t x with
        | TyVar (v,_) -> Set.singleton v.name
        | TyExists(v,a) ->
            List.fold (fun a v -> 
                if Set.contains v.name a = false then h.Add(v.name) |> ignore; a
                else Set.remove v.name a
                ) (f a) v
        | TyForall(v,a) ->
            let a = f a
            if Set.contains v.name a = false then h.Add(v.name) |> ignore; a
            else Set.remove v.name a
        | TyUnion _ | TyModule _ | TyMetavar _ | TyNominal _ | TyB | TyLit _ | TyPrim _ | TySymbol _ -> Set.empty
        | TyPair(a,b) | TyApply(a,b,_) | TyFun(a,b,_) -> f a + f b
        | TyRecord a -> Map.fold (fun s _ x -> Set.union s (f x)) Set.empty a
        | TyComment(_,a) | TyLayout(a,_) | TyInl(_,a) | TyArray a -> f a
        | TyMacro a -> 
            List.fold (fun s x ->
                match x with
                | TMLitVar a | TMVar a -> f a 
                | TMText _ -> Set.empty
                ) Set.empty a
    let used_vars = f x
    Seq.iter (h.Add >> ignore) (outside_foralls - used_vars)
    if 0 < h.Count then
        errors.Add(r, UnusedTypeVariable (Seq.toList h))

let assert_foralls_used errors r x = assert_foralls_used' errors Set.empty r x

let validate_nominal (errors : _ ResizeArray) global_id body v =
    // Stack union types and regular nominals must not be recursive.
    // Unlike in the previous version of Spiral which simply didn't put the nominal type in the environment
    // this one has to do it becaus the GADT constructors need access to it.
    let rec assert_nominal_non_recursive v =
        let f = assert_nominal_non_recursive
        match visit_t v with
        | TyNominal global_id' -> if global_id = global_id' then errors.Add(range_of_texpr_gadt_body body, IncorrectRecursiveNominal)
        | TyMetavar _ | TyVar _ | TyModule _ | TyUnion _ | TyB | TyLit _ | TyPrim _ | TySymbol _ -> ()
        | TyPair(a,b) | TyApply(a,b,_) | TyFun(a,b,_) -> f a; f b
        | TyRecord a -> Map.iter (fun _ -> f) a
        | TyExists(_,a) | TyComment(_,a) | TyLayout(a,_) | TyForall(_,a) | TyInl(_,a) | TyArray a -> f a
        | TyMacro a -> List.iter (function TMLitVar a | TMVar a -> f a | TMText _ -> ()) a
    match v with // Validates the union type.
    | TyUnion(a,b) ->
        a |> Map.iter (fun name (is_gadt, v) -> 
            let body =
                match body with
                | RawTUnion(_,a,_,_) -> Map.find name a |> snd
                | _ -> failwith "Compiler error: Expected an union."
            let is_stack = b = UStack

            // Makes sure that the GADT constructor is resulting in its own type.
            // Also make sure that it's not using an instance of itself in its constructor other than in first position.
            let rec assert_gadt_has_proper_specialized_constructor = function
                | TyNominal global_id' -> if global_id <> global_id' then errors.Add(range_of_texpr_gadt_constructor body, IncorrectGADTConstructorType)
                | TyApply(a,b,_) -> 
                    assert_gadt_has_proper_specialized_constructor a
                    if is_stack then assert_nominal_non_recursive b
                | _ -> errors.Add(range_of_texpr_gadt_constructor body, IncorrectGADTConstructorType)

            let assert_gadt_is_valid v =
                let rec find_gadt_constructor outside_foralls = function
                    | TyForall(n,t) -> find_gadt_constructor (Set.add n.name outside_foralls) t
                    | TyFun(a,b,_) -> 
                        if is_stack then assert_nominal_non_recursive a
                        assert_gadt_has_proper_specialized_constructor b
                        assert_foralls_used errors (range_of_texpr_gadt_body body) a
                        assert_foralls_used' errors outside_foralls (range_of_texpr_gadt_constructor body) b
                    | b ->
                        assert_gadt_has_proper_specialized_constructor b
                        assert_foralls_used' errors outside_foralls (range_of_texpr_gadt_constructor body) b
                        
                find_gadt_constructor Set.empty v
                    
            if is_gadt then assert_gadt_is_valid v
            else
                if is_stack then assert_nominal_non_recursive v
                assert_foralls_used errors (range_of_texpr body) v // We need to assert that the foralls in regular union bodies are checked.
            )
    | _ ->
        assert_nominal_non_recursive v

let rec kind_to_rawkind (x : TT) : RawKindExpr =
    match visit_tt x with
    | KindMetavar _ | KindType -> RawKindStar
    | KindFun(a,b) -> RawKindFun(kind_to_rawkind a, kind_to_rawkind b)
    
let var_to_hovar r (x : Var) : HoVar = r,(x.name,kind_to_rawkind x.kind)
let var_to_typevar r (x : Var) : TypeVar = var_to_hovar r x, [] // In the bottom up segment the constrains aren't checked anywhere so we'll put an empty list here.

open Spiral.BlockBundling
let infer package_id module_id (top_env' : TopEnv) expr =
    let at_tag i = {package_id=package_id; module_id=module_id; tag=i}
    let mutable top_env = top_env' // Is mutated only in two places at the top level. During actual inference can otherwise be thought of as immutable.
    let errors = ResizeArray()
    let generalized_statements = Dictionary(HashIdentity.Reference)
    let type_apply_args = Dictionary(HashIdentity.Reference)
    let module_type_apply_args = Dictionary(HashIdentity.Reference)
    let annotations = Dictionary<obj,_>(HashIdentity.Reference)
    let exists_vars = Dictionary<obj,_>(HashIdentity.Reference)
    let gadt_typecases = Dictionary<obj,_>(HashIdentity.Reference)
    let mutable autogened_forallvar_count_in_typecase = 0
    let mutable autogened_forallvar_count_in_funs = 0
    let hover_types = HoverTypes()

    /// Fills in the type applies and annotations, and generalizes statements. Also strips annotations from terms and patterns.
    /// Dealing with recursive statement type applies requires some special consideration.
    let fill r rec_term expr =
        assert (0 = errors.Count)
        let t_to_rawtexpr r vars_to_metavars expr =
            let rec f x = 
                match visit_t x with
                | TyUnion _ | TyMetavar _  | TyInl _  | TyModule _ as x -> failwithf "Compiler error: These cases should not appear in fill.\nGot: %A" x
                | TyForall(a,b) -> RawTForall(r,var_to_typevar r a,f b)
                | TyComment(_,x) -> f x
                | TyB -> RawTB r
                | TyLit x -> RawTLit(r,x)
                | TyPrim x -> RawTPrim(r,x)
                | TySymbol x -> RawTSymbol(r,x)
                | TyPair(a,b) -> RawTPair(r,f a,f b)
                | TyRecord l -> RawTRecord(r,Map.map (fun _ -> f) l)
                | TyFun(a,b,t) -> RawTFun(r,f a,f b,t)
                | TyExists(a,b) -> RawTExists(r,a |> List.map (fun n -> (r,(n.name,RawKindWildcard)),[]), f b)
                | TyArray a -> RawTArray(r,f a)
                | TyNominal i -> RawTFilledNominal(r,i)
                | TyApply(a,b,_) -> RawTApply(r,f a,f b)
                | TyVar (a,_) -> 
                    let is_typecase_metavar = List.tryFind (function TyVar(b,_) -> a = b | _ -> failwith "Compiler error: Expected a TyVar.") vars_to_metavars |> Option.isSome
                    if is_typecase_metavar then RawTMetaVar(r,a.name) else RawTVar(r,a.name)
                | TyMacro l -> l |> List.map (function TMText x -> RawMacroText(r,x) | TMVar x -> RawMacroType(r,f x) | TMLitVar x -> RawMacroTypeLit(r,f x)) |> fun l -> RawTMacro(r,l)
                | TyLayout(a,b) -> RawTLayout(r,f a,b)
            f expr
        let annot r x = t_to_rawtexpr r [] (snd annotations.[x])
        let rec fill_typecases rec_term x =
            match gadt_typecases.TryGetValue(x) with
            | true, typecase_data ->
                Seq.foldBack (fun (typecase_cond,forall_vars,typecase_constructor) typecase_body ->
                    let r = range_of_expr typecase_body
                    RawTypecase(r, t_to_rawtexpr r [] typecase_cond, [t_to_rawtexpr r forall_vars typecase_constructor, typecase_body])
                    ) typecase_data (term rec_term x)
            | _ ->
                term rec_term x
        and fill_foralls r rec_term body = 
            let _,body = foralls_get body
            let l,_ = foralls_ty_get generalized_statements.[body]
            List.foldBack (fun (x : Var) s -> RawFilledForall(r,x.name,s)) l (term rec_term body)
        and term rec_term x = 
            let f = term rec_term
            let clauses l = 
                List.map (fun (a, b) -> 
                    let rec_term,a = pattern rec_term a 
                    a,fill_typecases rec_term b
                    ) l
            match x with
            | RawFilledForall _ | RawMissingBody _ | RawType _ as x -> failwithf "Compiler error: These cases should not appear in fill. It is intended to be called on top level statements only.\nGot: %A" x
            | RawTypecase _
            | RawSymbol _ | RawB _ | RawLit _ | RawOp _ -> x
            | RawReal(_,x) -> x
            | RawV(r,n,false) -> x
            | RawV(r,n,true) ->
                match type_apply_args.TryGetValue(n) with
                | true, type_apply_args ->
                    match Map.tryFind n rec_term with
                    | None -> fst type_apply_args
                    | Some t -> t |> snd type_apply_args
                    |> List.fold (fun s x -> RawApply(r,s,RawType(r,t_to_rawtexpr r [] x))) x
                | _ -> x
            | RawDefaultLit(r,_) -> RawAnnot(r,x,annot r x)
            | RawForall(r,a,b) -> RawForall(r,a,f b)
            | RawMatch(r'',(RawForall _ | RawFun _) & body,[PatVar(r,name), on_succ]) ->
                let _,body = foralls_get body
                RawMatch(r'',fill_foralls r rec_term body,[PatVar(r,name), fill_typecases (Map.remove name rec_term) on_succ])
            | RawMatch(r,a,b) -> RawMatch(r,f a,clauses b)
            | RawFun(r,a) -> RawAnnot(r,RawFun(r,clauses a),annot r x)
            | RawExists(r,(r',a),b) -> RawExists(r,(r',Some(Option.defaultWith (fun () -> List.map (t_to_rawtexpr r []) exists_vars.[x]) a)),f b)
            | RawRecBlock(r,l,on_succ) ->
                let has_foralls = List.exists (function (_,RawForall _) -> true | _ -> false) l
                if has_foralls then RawRecBlock(r,List.map (fun (a,b) -> a, f b) l,f on_succ)
                else
                    let rec_term = List.fold (fun s ((_,name),b) -> Map.add name generalized_statements.[foralls_get b |> snd] s) rec_term l
                    let l = List.map (fun (a,b) -> a, fill_foralls (fst a) rec_term b) l
                    RawRecBlock(r,l,f on_succ)
            | RawRecordWith(r,a,b,c) ->
                let b = b |> List.map (function
                    | RawRecordWithSymbol(a,b) -> RawRecordWithSymbol(a,f b)
                    | RawRecordWithSymbolModify(a,b) -> RawRecordWithSymbolModify(a,f b)
                    | RawRecordWithInjectVar(a,b) -> RawRecordWithInjectVar(a,f b)
                    | RawRecordWithInjectVarModify(a,b) -> RawRecordWithInjectVarModify(a,f b)
                    )
                RawRecordWith(r,List.map f a,b,c)
            | RawJoinPoint(r,q,a,b) -> RawAnnot(r,RawJoinPoint(r,q,f a,b),annot r x)
            | RawAnnot(r,a,_) -> f a
            | RawOpen(r,a,b,c) ->
                let f = function TyModule s -> s | _ -> failwith "Compiler error: Module open should always succeed in fill."
                List.fold (fun s x -> (f s).[snd x]) top_env.term.[snd a] b |> f
                |> Map.fold (fun s k _ -> Map.remove k s) rec_term
                |> fun rec_term -> RawOpen(r,a,b,term rec_term c)
            | RawApply(r,a,b) ->
                let q = RawApply(r,f a,f b)
                match module_type_apply_args.TryGetValue(x) with
                | true, typevars -> List.fold (fun a b -> RawApply(r,a,RawType(r,t_to_rawtexpr r [] b))) q typevars
                | _ -> q
            | RawIfThenElse(r,a,b,c) -> RawIfThenElse(r,f a,f b,f c)
            | RawIfThen(r,a,b) -> RawIfThen(r,f a,f b)
            | RawPair(r,a,b) -> RawPair(r,f a,f b)
            | RawSeq(r,a,b) -> RawSeq(r,f a,f b)
            | RawHeapMutableSet(r,a,b,c) -> RawHeapMutableSet(r,f a,List.map f b,f c)
            | RawMacro(r,l) -> 
                let l = l |> List.map (function RawMacroTerm(r,x,b) -> RawMacroTerm(r,f x,b) | x -> x )
                RawAnnot(r,RawMacro(r,l),annot r x)
            | RawArray(r,a) -> RawAnnot(r,RawArray(r,List.map f a),annot r x)
        and pattern recbterm x' =
            let mutable rec_term = rec_term
            let rec f = function
                | PatFilledDefaultValue _ -> failwith "Compiler error: PatDefaultValueFilled should not appear in fill."
                | PatValue _ | PatSymbol _ | PatE _ | PatB _ as x -> x
                | PatVar(r,name) as x -> rec_term <- Map.remove name rec_term; x
                | PatDyn(r,a) -> PatDyn(r,f a)
                | PatUnbox(r,q,a) -> PatUnbox(r,q,f a)
                | PatExists(r,q,a) -> PatExists(r,q,f a)
                | PatAnnot(_,a,_) -> f a
                | PatPair(r,a,b) -> PatPair(r,f a,f b)
                | PatRecordMembers(r,a) ->
                    let a = a |> List.map (function
                        | PatRecordMembersSymbol(a,b) -> PatRecordMembersSymbol(a,f b)
                        | PatRecordMembersInjectVar(a,b) -> PatRecordMembersInjectVar(a,f b)
                        )
                    PatRecordMembers(r,a)
                | PatOr(r,a,b) -> PatOr(r,f a,f b)
                | PatAnd(r,a,b) -> PatAnd(r,f a,f b)
                | PatDefaultValue(r,a) as x -> PatFilledDefaultValue(r,a,annot r x)
                | PatWhen(r,a,b) -> PatWhen(r,f a,term rec_term b)
                | PatNominal(r,a,b,c) -> PatNominal(r,a,b,f c)
                | PatArray(r,a) -> PatArray(r,List.map f a)
            rec_term, f x'

        let x = fill_foralls r rec_term expr
        assert (0 = errors.Count)
        x

    let fresh_kind () = KindMetavar {contents'=None}
    let fresh_var'' x = TyMetavar (x, ref None)
    let fresh_var' scope kind = fresh_var'' {scope=scope; constraints=Set.empty; kind=kind}
    let fresh_subst_var scope cons kind = fresh_var'' {scope=scope; constraints=cons; kind=kind}
    let forall_subst_all scope x =
        let rec loop m x = 
            match visit_t x with
            | TyForall(a,b) -> 
                let v = fresh_subst_var scope a.constraints a.kind
                let type_apply_args,b = loop ((a, v) :: m) b
                v :: type_apply_args, b
            | x -> [], subst m x
        loop [] x

    let exists_subst_term scope (l : Var list, body) =
        let vars = l |> List.map (fun a -> fresh_subst_var scope a.constraints a.kind)
        vars, subst (List.zip l vars) body

    let assert_exists_hasnt_metavars r vars =
        if List.exists has_metavars vars then errors.Add(r, ExistsShouldntHaveMetavars vars)

    let generalize r scope (forall_vars : Var list) (body : T) =
        let h = HashSet(HashIdentity.Reference)
        List.iter (h.Add >> ignore) forall_vars
        let generalized_metavars = ResizeArray()
        let rec replace_metavars x =
            let f = replace_metavars
            match x with
            | TyVar (_,{contents=Some x})
            | TyMetavar(_,{contents=Some x}) -> f x
            | TyMetavar(x, link) when scope = x.scope ->
                let v = tyvar {scope=x.scope; constraints=x.constraints; kind=kind_force x.kind; name=autogen_name_in_fun autogened_forallvar_count_in_funs}
                autogened_forallvar_count_in_funs <- autogened_forallvar_count_in_funs+1
                link := Some v
                replace_metavars v
            // This scheme with the HashSet is so generalize works for mutually recursive statements.
            | TyVar (v,_) -> if scope = v.scope && h.Add(v) then generalized_metavars.Add(v)
            | TyMetavar _ | TyNominal _ | TyB | TyLit _ | TyPrim _ | TySymbol _ -> ()
            | TyPair(a,b) | TyApply(a,b,_) | TyFun(a,b,_) -> f a; f b
            | TyUnion(a,_) -> Map.iter (fun _ -> snd >> f) a
            | TyRecord a -> Map.iter (fun _ -> f) a
            | TyExists(v,a) -> List.iter (h.Add >> ignore) v; f a
            | TyForall(v,a) -> (h.Add >> ignore) v; f a
            | TyComment(_,a) | TyLayout(a,_) | TyArray a -> f a
            | TyMacro a -> List.iter (function TMLitVar a | TMVar a -> f a | TMText _ -> ()) a
            | TyModule _ | TyInl _ -> ()

        let f x s = TyForall(x,s)
        replace_metavars body
        let x = Seq.foldBack f generalized_metavars body |> List.foldBack f forall_vars |> term_subst
        if List.isEmpty forall_vars = false then assert_foralls_used errors r x
        x

    let gadt_extract scope (v : T) =
        let forall_subst_all_gadt x =
            let rec loop m x = 
                match visit_t x with
                | TyForall(a,b) ->
                    let v = tyvar {a with name=autogen_name_in_typecase autogened_forallvar_count_in_typecase; scope=scope}
                    autogened_forallvar_count_in_typecase <- autogened_forallvar_count_in_typecase+1
                    let type_apply_args,b = loop ((a, v) :: m) b
                    v :: type_apply_args, b
                | x -> [], subst m x
            loop [] x
        let forall_vars,v = forall_subst_all_gadt v
        match v with
        | TyFun(a,b,_) -> forall_vars,a,b
        | b -> forall_vars,TyB,b

    let inline unify_kind' er r got expected =
        let rec loop (a'',b'') =
            match visit_tt a'', visit_tt b'' with
            | KindType, KindType -> ()
            | KindFun(a,a'), KindFun(b,b') -> loop (a,b); loop (a',b')
            | KindMetavar a, KindMetavar b & b' -> if a <> b then a.contents' <- Some b'
            | KindMetavar link, b | b, KindMetavar link -> link.contents' <- Some b
            | _ -> raise (TypeErrorException [r, er (got, expected)])
        loop (got, expected)
    let unify_kind r got expected = try unify_kind' KindError r got expected with :? TypeErrorException as e -> errors.AddRange e.Data0
    let unify_gadt (gadt_links : T option ref ResizeArray option) (r : VSCRange) (got : T) (expected : T) : unit =
        let unify_kind got expected = unify_kind' KindError r got expected
        let er () = raise (TypeErrorException [r, TermError(got, expected)])

        let rec constraint_process (con : Constraint Set) b =
            let unify_kind got expected = unify_kind' KindErrorInConstraint r got expected
            let body = function
                | CUInt, TyPrim (UInt8T | UInt16T | UInt32T | UInt64T)
                | CSInt, TyPrim (Int8T | Int16T | Int32T | Int64T)
                | CInt, TyPrim (UInt8T | UInt16T | UInt32T | UInt64T | Int8T | Int16T | Int32T | Int64T)
                | CFloat, TyPrim (Float32T | Float64T)
                | CNumber, TyPrim (UInt8T | UInt16T | UInt32T | UInt64T | Int8T | Int16T | Int32T | Int64T | Float32T | Float64T)
                | CPrim, TyPrim _
                | CSymbol, TySymbol _
                | CRecord, TyRecord _ -> []
                | con, TyMetavar(x,_) -> x.constraints <- Set.add con x.constraints; []
                | CPrototype prot & con, x ->
                    match type_apply_split x with
                    | TyNominal ins, x' ->
                        match Map.tryFind (prot,ins) top_env.prototypes_instances with
                        | Some cons -> 
                            try List.fold2 (fun ers con x -> List.append (constraint_process con (visit_t x)) ers) [] cons x'
                            with :? System.ArgumentException -> [] // This case can occur due when kind application overflows in a previous expression.
                        | None -> [InstanceNotFound(prot,ins)]
                    | TyMetavar _ & x, _ -> [PrototypeConstraintCannotPropagateToMetavar(prot,x)]
                    | TyVar _ & x, _ -> [PrototypeConstraintCannotPropagateToVar(prot,x)]
                    | _ -> [ConstraintError(con,x)]
                | con, x -> [ConstraintError(con,x)]

            match b with
            | TyVar (_,{contents=Some x}) -> constraint_process con x
            | TyVar (b,_) -> if con.IsSubsetOf b.constraints = false then [ForallVarConstraintError(b.name,con,b.constraints)] else []
            | b -> 
                let b_kind = tt top_env b 
                Set.fold (fun ers con -> 
                    unify_kind b_kind (constraint_kind top_env con)
                    List.append (body (con,b)) ers
                    ) [] con

        // Does occurs checking for recursive metavariables.
        // Does scope checking in forall vars.
        let validate_mvar_unification i x =
            let nested_tvars = HashSet(HashIdentity.Reference)
            let rec f x =
                match visit_t x with
                | TyModule _ | TyNominal _ | TyB | TyLit _ | TyPrim _ | TySymbol _ -> ()
                | TyMacro a -> a |> List.iter (function TMText _ -> () | TMLitVar a | TMVar a -> f a)
                | TyForall(v,a) | TyInl(v,a) ->
                    let _ = nested_tvars.Add(v)
                    f a
                    let _ = nested_tvars.Remove(v)
                    ()
                | TyExists(v,a) ->
                    v |> List.iter (nested_tvars.Add >> ignore)
                    f a
                    v |> List.iter (nested_tvars.Remove >> ignore)
                | TyComment(_,a) | TyArray a -> f a
                | TyApply(a,b,_) | TyFun(a,b,_) | TyPair(a,b) -> f a; f b
                | TyUnion(l,_) -> Map.iter (fun _ -> snd >> f) l
                | TyRecord l -> Map.iter (fun _ -> f) l
                | TyVar(b,_) -> if nested_tvars.Contains b = false && i.scope < b.scope then raise (TypeErrorException [r,TypeVarScopeError(b.name,got,expected)])
                | TyMetavar(x,_) -> if i = x then raise (TypeErrorException [r,RecursiveMetavarsNotAllowed(got,expected)]) elif i.scope < x.scope then x.scope <- i.scope
                | TyLayout(a,_) -> f a
            f x

        // Does occurs checking for recursive type variables.
        let rec validate_tvar_unification i x =
            let f = validate_tvar_unification i
            match visit_t x with
            | TyMetavar _ | TyModule _ | TyNominal _ | TyB | TyLit _ | TyPrim _ | TySymbol _ -> ()
            | TyMacro a -> a |> List.iter (function TMText _ -> () | TMLitVar a | TMVar a -> f a)
            | TyExists(_,a) | TyComment(_,a) | TyForall(_,a) | TyInl(_,a) | TyArray a -> f a
            | TyApply(a,b,_) | TyFun(a,b,_) | TyPair(a,b) -> f a; f b
            | TyUnion(l,_) -> Map.iter (fun _ -> snd >> f) l
            | TyRecord l -> Map.iter (fun _ -> f) l
            | TyVar(x,_) -> if i = x then raise (TypeErrorException [r,RecursiveTypevarsNotAllowed(got,expected)])
            | TyLayout(a,_) -> f a

        let rec loop (a'',b'') = 
            match visit_t a'', visit_t b'' with
            | TyComment(_,a), b | a, TyComment(_,b) -> loop (a,b)
            | TyMetavar(a,link), TyMetavar(b,_) & b' ->
                if a <> b then
                    unify_kind a.kind b.kind
                    b.scope <- min a.scope b.scope
                    b.constraints <- a.constraints + b.constraints
                    link := Some b'
            | TyMetavar(a,link), b | b, TyMetavar(a,link) ->
                validate_mvar_unification a b
                unify_kind a.kind (tt top_env b)
                match constraint_process a.constraints b with
                | [] -> link := Some b
                | constraint_errors -> raise (TypeErrorException (List.map (fun x -> r,x) constraint_errors))
            | TyVar (a,_), TyVar (b,_) when a = b -> ()
            | TyVar (a,link), b | b, TyVar (a,link) when gadt_links.IsSome ->
                validate_tvar_unification a b
                unify_kind a.kind (tt top_env b)
                match constraint_process a.constraints b with
                | [] -> link := Some b; gadt_links.Value.Add(link)
                | constraint_errors -> raise (TypeErrorException (List.map (fun x -> r,x) constraint_errors))
            | TyFun(a,a',ta), TyFun(b,b',tb) when ta = tb -> loop (a,b); loop (a',b')
            | TyPair(a,a'), TyPair(b,b') -> loop (a,b); loop (a',b')
            | TyApply(a,a',_), TyApply(b,b',_) -> loop (a',b'); loop (a,b)
            | TyUnion(l,q), TyUnion(l',q') -> 
                if q = q' then
                    let a,b = Map.toArray l, Map.toArray l'
                    if a.Length <> b.Length then er ()
                    else Array.iter2 (fun (ka,a) (kb,b) -> if ka = kb && fst a = fst b then loop (snd a,snd b) else er()) a b
                else raise (TypeErrorException [r,UnionTypesMustHaveTheSameLayout])
            | TyRecord l, TyRecord l' -> 
                let a,b = Map.toArray l, Map.toArray l'
                if a.Length <> b.Length then er ()
                else
                    let a = a |> Array.sortBy (fun ((_,k),_) -> k)
                    let b = b |> Array.sortBy (fun ((_,k),_) -> k)
                    Array.iter2 (fun (ka,a) (kb,b) ->
                        if (ka |> snd) = (kb |> snd)
                        then loop (a,b)
                        else er()
                    ) a b
            | TyNominal i, TyNominal i' when i = i' -> ()
            | TyB, TyB -> ()
            | TyPrim x, TyPrim x' when x = x' -> ()
            | TyLit a, TyLit b when a = b -> ()
            | TySymbol x, TySymbol x' when x = x' -> ()
            | TyArray a, TyArray b -> loop (a,b)
            // Note: Unifying these 3 only makes sense if the `expected` is fully inferred already.
            | TyExists(a,b), TyExists(a',b') when 
                    List.length a = List.length a'
                    && List.forall2 (fun (a : Var) (a' : Var) -> a.kind = a'.kind && a.constraints = a'.constraints) a a' -> 
                loop (b, subst (List.map2 (fun a a' -> a', tyvar a) a a') b')
            | TyForall(a,b), TyForall(a',b') 
            | TyInl(a,b), TyInl(a',b') when a.kind = a'.kind && a.constraints = a'.constraints -> loop (b, subst [a',tyvar a] b')
            | TyMacro a, TyMacro b -> 
                List.iter2 (fun a b -> 
                    match a,b with
                    | TMText a, TMText b when System.Object.ReferenceEquals(a,b) || a = b -> ()
                    | TMVar a, TMVar b -> loop(a,b)
                    | _ -> er ()
                    ) a b
            | TyLayout(a,a'), TyLayout(b,b') when a' = b' -> loop (a,b)
            | _ -> er ()

        try loop (got, expected)
        with :? TypeErrorException as e -> errors.AddRange e.Data0

    let unify range got expected = unify_gadt None range got expected

    let apply_record r s l x =
        match visit_t x with
        | TySymbol x ->
            match l |> Map.tryPick (fun (_, k) v -> if k = x then Some v else None) with
            | Some x -> 
                let com = match x with TyComment(com,_) -> com | _ -> ""
                unify r s x
                hover_types.AddHover (r,(x,com))
            | None -> errors.Add(r,RecordIndexFailed x)
        | x -> errors.Add(r,ExpectedSymbolAsRecordKey x)

    let assert_bound_vars env a =
        let keys_of m = Map.fold (fun s k _ -> Set.add k s) Set.empty m
        validate_bound_vars (loc_env top_env) env.constraints (keys_of env.term) (keys_of env.ty) (Choice1Of2 a) |> errors.AddRange

    let fresh_var scope = fresh_var' scope KindType

    let v_cons env a = Map.tryFind a env |> Option.orElseWith (fun () -> Map.tryFind a top_env.constraints)
    let v env top_env a = Map.tryFind a env |> Option.orElseWith (fun () -> Map.tryFind a top_env) 
    let v_term env a = v env.term top_env.term a |> Option.map (function TyComment(com,x) -> com, visit_t x | x -> "", visit_t x)
    let v_ty env a = v env.ty top_env.ty a 

    let typevar_to_var scope cons (((_,(name,kind)),constraints) : TypeVar) : Var = 
        let rec typevar = function
            | RawKindWildcard -> fresh_kind()
            | RawKindStar -> KindType
            | RawKindFun(a,b) -> KindFun(typevar a, typevar b)
        let kind = typevar kind
        let cons =
            constraints |> List.choose (fun (r,x) ->
                match v_cons cons x with
                | Some (M _) -> errors.Add(r,ExpectedConstraintInsteadOfModule); None
                | Some (C x) -> unify_kind r kind (constraint_kind top_env x); Some x
                | None -> errors.Add(r,UnboundVariable x); None
                ) |> Set.ofList

        {scope=scope; constraints=cons; kind=kind_force kind; name=name}

    let typevars scope env (l : TypeVar list) =
        List.mapFold (fun s x ->
            let v = typevar_to_var scope env.constraints x
            v, Map.add v.name (tyvar v) s
            ) env.ty l

    let rec term scope env s x = term' scope false env s x
    and term' scope is_in_left_apply (env : Env) s x =
        let f = term scope env
        let f' x = let v = fresh_var scope in f v x; visit_t v
        let f'' x = let v = fresh_var scope in term' scope true env v x; visit_t v
        let inline rawv (r,name,is_tvar_applied) =
            match v_term env name with
            | None -> errors.Add(r,UnboundVariable name)
            | Some (_,TySymbol "<real>") -> errors.Add(r,RealFunctionInTopDown)
            | Some (com,TyModule _ & m) when is_in_left_apply = false -> 
                hover_types.AddHover(r,(m,com))
                errors.Add(r,ModuleMustBeImmediatelyApplied)
            | Some (com,a) -> 
                if is_tvar_applied then
                    match a with TyForall _ -> annotations.Add(x,(r,s)) | _ -> ()
                    let f a = let l,v = forall_subst_all scope a in unify r s v; l
                    let l = f a
                    hover_types.AddHover(r,(s,com))
                    type_apply_args.Add(name,(l,f))
                else
                    unify r s a
                    hover_types.AddHover(r,(s,com))
        let match_clause (q,w) (a,b) =
            let gadt_links, gadt_typecases', (scope, env) = pattern scope env q a
            term scope env w b
            dispose_gadt_links gadt_links
            gadt_typecases.Add(b,gadt_typecases')
        match x with
        | RawB r -> unify r s TyB
        | RawV(r,a,is_tvar_applied) -> rawv (r,a,is_tvar_applied)
        | RawDefaultLit(r,_) -> unify r s (fresh_subst_var scope (Set.singleton CNumber) KindType); hover_types.AddHover(r,(s,"")); annotations.Add(x,(r,s))
        | RawLit(r,a) -> unify r s (lit a)
        | RawSymbol(r,x) -> unify r s (TySymbol x)
        | RawIfThenElse(_,cond,tr,fl) -> f (TyPrim BoolT) cond; f s tr; f s fl
        | RawIfThen(r,cond,tr) -> f (TyPrim BoolT) cond; unify r s TyB; f TyB tr
        | RawPair(r,a,b) ->
            let q,w = fresh_var scope, fresh_var scope
            unify r s (TyPair(q, w))
            f q a; f w b
        | RawSeq(_,a,b) -> f TyB a; f s b
        | RawReal(_,a) -> assert_bound_vars env a
        | RawOp(_,_,l) -> List.iter (assert_bound_vars env) l
        | RawJoinPoint(r,None,a,_) -> annotations.Add(x,(r,s)); f s a
        | RawJoinPoint(r,Some _,a,_) -> 
            unify r s (TySymbol "tuple_of_free_vars")
            let s = fresh_var scope
            annotations.Add(x,(r,s))
            f s a
        | RawApply(r,a',b) ->
            let rec loop = function
                | TyNominal _ | TyApply _ as a -> 
                    match type_apply_split a with
                    | TyNominal i, l ->
                        let n = top_env.nominals.[i]
                        match n.body with
                        | TyUnion _ -> errors.Add(r,UnionsCannotBeApplied)
                        | _ -> 
                            match Utils.list_try_zip n.vars l with
                            | Some l -> loop (subst l n.body)
                            | None -> errors.Add(r,MalformedNominal)
                    | _ -> errors.Add(r,ExpectedNominalInApply a)
                | TyLayout(a,_) ->
                    match visit_t a with
                    | TyRecord l -> apply_record r s l (f' b)
                    | a -> errors.Add(r,ExpectedRecordInsideALayout a)
                | TyRecord l -> apply_record r s l (f' b)
                | TyModule l -> 
                    match f' b with
                    | TySymbol n ->
                        match Map.tryFind n l with
                        | Some (TyModule _ as a) ->
                            if is_in_left_apply then 
                                match b with RawSymbol(r,_) -> hover_types.AddHover(r,(a,"")) | _ -> ()
                                unify r s a
                            else errors.Add(r,ModuleMustBeImmediatelyApplied)
                        | Some a' -> 
                            let typevars,a = forall_subst_all scope a'
                            if List.isEmpty typevars = false then 
                                annotations.Add(x,(r,s))
                                module_type_apply_args.Add(x,typevars)
                            unify r s a
                            match b with 
                            | RawSymbol(r,_) -> 
                                let com = match a' with TyComment(com,_) -> com | _ -> ""
                                hover_types.AddHover(r,(a,com))
                            | _ -> ()
                        | None -> errors.Add(r,ModuleIndexFailed n)
                    | b -> errors.Add(r,ExpectedSymbolAsModuleKey b)
                | TyFun(domain,range,_) -> unify (range_of_expr a') range s; f domain b
                | a -> let v = fresh_var scope in unify (range_of_expr a') a (TyFun(v,s,FT_Vanilla)); f v b
            loop (f'' a')
        | RawAnnot(r,a,b) ->  ty_init scope env s b; f s a
        | RawOpen(_,(r,a),l,on_succ) ->
            match module_open (Some hover_types) (loc_env top_env) env.ty r a l with
            | Ok x ->
                let combine big small = Map.foldBack Map.add small big
                term scope {term = combine env.term x.term; ty = combine env.ty x.ty; constraints = combine env.constraints x.constraints} s on_succ
            | Error e -> errors.Add(e)
        | RawRecordWith(r,l,withs,withouts) ->
            let i = errors.Count
            let withouts,fields =
                List.foldBack (fun x (l,s as state) ->
                    match x with
                    | RawRecordWithoutSymbol(r,a) -> {|range=r; symbol = a|} :: l, Set.add a s
                    | RawRecordWithoutInjectVar(r,a) ->
                        match v_term env a with
                        | Some (com, TySymbol a & x) -> hover_types.AddHover(r,(x,com)); {|range=r; symbol = a|} :: l, Set.add a s
                        | Some (_,x) -> errors.Add(r, ExpectedSymbolAsRecordKey x); state
                        | None -> errors.Add(r, UnboundVariable a); state
                    ) withouts ([],Set.empty)
            let withs,_ =
                List.foldBack (fun x (l,s as state) ->
                    let with_symbol ((r,a),b) = {|range=r; symbol = a; is_blocked=Set.contains a s; is_modify=false; var=fresh_var scope; body=b|} :: l, Set.add a s
                    let with_symbol_modify ((r,a),b) = {|range=r; symbol = a; is_blocked=Set.contains a s; is_modify=true; var=TyFun(fresh_var scope,fresh_var scope,FT_Vanilla); body=b|} :: l, Set.add a s
                    let inline with_inject next ((r,a),b) =
                        match v_term env a with
                        | Some (com, TySymbol a & x) -> hover_types.AddHover(r,(x,com)); next ((r,a),b)
                        | Some (_, x) -> errors.Add(r, ExpectedSymbolAsRecordKey x); f' b |> ignore; state
                        | None -> errors.Add(r, UnboundVariable a); f' b |> ignore; state
                    match x with
                    | RawRecordWithSymbol(a,b) -> with_symbol (a,b)
                    | RawRecordWithSymbolModify(a,b) -> with_symbol_modify (a,b)
                    | RawRecordWithInjectVar(a,b) -> with_inject with_symbol (a,b)
                    | RawRecordWithInjectVarModify(a,b) -> with_inject with_symbol_modify (a,b)
                    ) withs ([],fields)

            let eval m =
                let m = (m,withs) ||> List.fold (fun m x ->
                    if x.is_modify then
                        let i, q =
                            match m |> Map.tryPick (fun (i, k) v -> if k = x.symbol then Some (i, v) else None) with
                            | Some q -> q
                            | None -> errors.Add(x.range,RecordIndexFailed x.symbol); m.Count, fresh_var scope
                        let w = fresh_var scope
                        unify x.range (TyFun(q,w,FT_Vanilla)) x.var
                        f x.var x.body
                        Map.add (i, x.symbol) w m
                    else
                        f x.var x.body
                        let i =
                            m
                            |> Map.tryPick (fun (i, k) v -> if k = x.symbol then Some i else None)
                            |> Option.defaultValue m.Count
                        Map.add (i, x.symbol) x.var m
                    )
                withouts |> List.fold (fun m x -> m |> Map.filter (fun (_, k) _ -> k <> x.symbol)) m

            let bind s = withs |> List.iter (fun x ->
                if x.is_blocked = false then
                    if x.is_modify then
                        s
                        |> Map.tryPick (fun (i, k') v -> if k' = x.symbol then Some (i, v) else None)
                        |> Option.iter (fun (_, k) -> unify x.range x.var (TyFun(fresh_var scope,k,FT_Vanilla)))
                    else
                        s
                        |> Map.tryPick (fun (i, k') v -> if k' = x.symbol then Some (i, v) else None)
                        |> Option.iter (fun (_, k) -> k |> unify x.range x.var)
                )

            let rec tail' m = function
                | x :: xs ->
                    match f' x with
                    | TySymbol k ->
                        match m |> Map.tryPick (fun (i, k') v -> if k' = k then Some (i, v) else None) with
                        | Some (i, m) ->
                            match visit_t m with
                            | TyRecord m -> tail' m xs
                            | m -> errors.Add(range_of_expr x, ExpectedRecordAsResultOfIndex m); eval Map.empty
                        | _ -> errors.Add(range_of_expr x, RecordIndexFailed k); eval Map.empty
                        |> fun v ->
                            let i = m |> Map.tryPick (fun (i, k') v -> if k' = k then Some (i, v) else None)
                            match i with
                            | Some (i, _) -> Map.add (i, k) (TyRecord v) m
                            | None -> Map.add (m.Count, k) (TyRecord v) m
                    | TyMetavar _ -> errors.Add(range_of_expr x, MetavarsNotAllowedInRecordWith); eval Map.empty
                    | a -> errors.Add(range_of_expr x, ExpectedSymbolInRecordWith a); eval Map.empty
                | [] -> eval m

            let rec tail (m,s) = function
                | x :: xs ->
                    match f' x with
                    | TySymbol k ->
                        match
                            m |> Map.tryPick (fun (i, k') v -> if k' = k then Some (i, v) else None),
                            s |> Map.tryPick (fun (i, k') v -> if k' = k then Some (i, v) else None)
                        with
                        | Some (i,m), Some (_i',s) ->
                            match visit_t m, visit_t s with
                            | TyRecord m, TyRecord s -> i, tail (m,s) xs
                            | TyRecord m, _ -> i, tail' m xs
                            | m, _ -> errors.Add(range_of_expr x, ExpectedRecordAsResultOfIndex m); i, eval Map.empty
                        | Some (i,m), None ->
                            match visit_t m with
                            | TyRecord m -> i, tail' m xs
                            | m -> errors.Add(range_of_expr x, ExpectedRecordAsResultOfIndex m); i, eval Map.empty
                        | _ -> errors.Add(range_of_expr x, RecordIndexFailed k); i, eval Map.empty
                        |> fun (i,v) -> Map.add (i,k) (TyRecord v) m
                    | TyMetavar _ -> errors.Add(range_of_expr x, MetavarsNotAllowedInRecordWith); eval Map.empty
                    | a -> errors.Add(range_of_expr x, ExpectedSymbolInRecordWith a); eval Map.empty
                | [] -> bind s; eval m

            match l with
            | [] -> 
                match visit_t s with TyRecord s -> bind s | _ -> ()
                eval Map.empty
            | m :: l ->
                match f' m, visit_t s with
                | TyRecord m, TyRecord s -> tail (m,s) l
                | TyRecord m, _ -> tail' m l
                | TyMetavar _, _ -> errors.Add(range_of_expr x, MetavarsNotAllowedInRecordWith); eval Map.empty
                | a,_ -> errors.Add(range_of_expr x, ExpectedRecord a); eval Map.empty
            |> fun v -> if errors.Count = i then unify r (TyRecord v) s
        | RawExists(r,(r',l),body) ->
            match visit_t s with
            | TyExists(type_vars,type_body) ->
                let vars, s = exists_subst_term scope (type_vars,type_body)
                l |> Option.iter (fun l ->
                    let l1,l2 = vars.Length, l.Length
                    if l1 = l2 then List.iter2 (ty_init scope env) vars l
                    else errors.Add(r', UnexpectedNumberOfArgumentsInExistsBody(l1,l2))
                    )
                term scope env s body
                assert_exists_hasnt_metavars (range_of_expr x) vars
                exists_vars.Add(x,vars)
            | s -> errors.Add(r, ExpectedExistentialInTerm s); f (fresh_var scope) body
        | RawFun(r,l) ->
            annotations.Add(x,(r,s))
            let q,w = fresh_var scope, fresh_var scope
            unify r s (TyFun(q,w,FT_Vanilla))
            List.iter (match_clause (q,w)) l
        | RawForall _ -> failwith "Compiler error: Should be handled in let statements."
        | RawMatch(_,(RawForall _ | RawFun _) & body,[PatVar(r,name), on_succ]) -> term scope (inl scope env ((r, name), body)) s on_succ
        | RawRecBlock(_,l',on_succ) -> term scope (rec_block scope env l') s on_succ
        | RawMatch(_,body,l) ->
            let body_var = fresh_var scope
            f body_var body
            List.iter (match_clause (body_var,s)) l
        | RawMissingBody r -> errors.Add(r,MissingBody)
        | RawMacro(r,a) ->
            annotations.Add(x,(r,s))
            List.iter (function
                | RawMacroText _ -> ()
                | RawMacroTerm(_,a,_) -> term scope env (fresh_var scope) a
                | RawMacroType(_,a) | RawMacroTypeLit(_,a) -> ty_init scope env (fresh_var scope) a
                ) a
        | RawHeapMutableSet(r,a,b,c) ->
            unify r s TyB
            try let v = fresh_var scope
                let i = errors.Count
                f v a
                match visit_t v with
                | TyMetavar _ -> raise (TypeErrorException [r, LayoutSetMustBeAnnotated])
                | TyLayout(v,lay) ->
                    match lay with
                    | HeapMutable | StackMutable ->
                        if i <> errors.Count then raise (TypeErrorException [])
                        let b = List.map (fun x -> range_of_expr x, f' x) b
                        List.fold (fun (r,a') (r',b') ->
                            match visit_t a' with
                            | TyRecord a ->
                                match b' with
                                | TySymbol b ->
                                    match a |> Map.tryPick (fun (_, k) v -> if k = b then Some v else None) with
                                    | Some x -> r', x
                                    | _ -> raise (TypeErrorException [r, RecordIndexFailed b])
                                | b -> raise (TypeErrorException [r', ExpectedSymbol' b])
                            | a -> raise (TypeErrorException [r, ExpectedRecord a])
                            ) (range_of_expr a, v) b |> snd
                    | Heap -> raise (TypeErrorException [r, ExpectedMutableLayout v])
                | v -> raise (TypeErrorException [r, ExpectedMutableLayout v])
            with :? TypeErrorException as e -> errors.AddRange e.Data0; fresh_var scope
            |> fun v -> f v c
        | RawArray(r,a) ->
            annotations.Add(x,(r,s))
            let v = fresh_var scope
            unify r s (TyArray v)
            List.iter (f v) a
        | RawFilledForall _ -> failwith "Compiler error: Should not manifest during type inference."
        | RawType _ -> failwith "Compiler error: RawType should not appear in the top down segment."
        | RawTypecase _ -> failwith "Compiler error: `typecase` should not appear in the top down segment."
    and inl scope env ((r, name), body) =
        let scope = scope + 1
        let vars,body = foralls_get body
        vars |> List.iter (fun ((r,(name,_)),_) -> if Map.containsKey name env.ty then errors.Add(r,ShadowedForall))
        let vars,env_ty = typevars scope env vars
        let body_var = fresh_var scope
        term scope {env with ty = env_ty} body_var body
        let t = generalize r scope vars body_var
        generalized_statements.Add(body,t)
        hover_types.AddHover(r,(t,""))
        {env with term = Map.add name t env.term }
    and rec_block scope env l' =
        let rec term_annotations scope env x =
            let f t = 
                let i = errors.Count
                let v = fresh_var scope
                ty_init scope env v t
                if i = errors.Count && has_metavars v then errors.Add(range_of_texpr t, RecursiveAnnotationHasMetavars v)
                v
            match x with
            | RawFun(_,[(PatAnnot(_,_,t) | PatDyn(_,PatAnnot(_,_,t))),body]) -> TyFun(f t, term_annotations scope env body,FT_Vanilla)
            | RawFun(_,[pat,body]) -> errors.Add(range_of_pattern pat, ExpectedAnnotation); TyFun(fresh_var scope, term_annotations scope env body,FT_Vanilla)
            | RawFun(r,_) -> errors.Add(r, ExpectedSinglePattern); TyFun(fresh_var scope, fresh_var scope, FT_Vanilla)
            | RawJoinPoint(_,_,RawAnnot(_,_,t),_) | RawAnnot(_,_,t) -> f t
            | x -> errors.Add(range_of_expr x,ExpectedAnnotation); fresh_var scope
        let scope = scope + 1
        let has_foralls = List.exists (function (_,RawForall _) -> true | _ -> false) l'
        let l,m =
            if has_foralls then
                List.mapFold (fun s ((r,name),body) ->
                    let vars,body = foralls_get body
                    vars |> List.iter (fun x -> if Map.containsKey (typevar_name x) env.ty then errors.Add(range_of_typevar x,ShadowedForall))
                    let vars, env_ty = typevars scope env vars
                    let body_var = term_annotations scope {env with ty = env_ty} body
                    let term env = term scope {env with ty = env_ty} body_var body
                    let gen env : Env = 
                        let t = generalize r scope vars body_var
                        generalized_statements.Add(body,t)
                        hover_types.AddHover(r,(t,""))
                        {env with term = Map.add name t env.term}
                    let ty = List.foldBack (fun x s -> TyForall(x,s)) vars body_var |> term_subst
                    (term, gen), Map.add name ty s
                    ) env.term l'
            else 
                List.mapFold (fun s ((r,name),body) -> 
                    let body_var = fresh_var scope
                    let term env = term scope env body_var body
                    let gen env : Env = 
                        let t = generalize r scope [] body_var
                        generalized_statements.Add(body,t)
                        hover_types.AddHover(r,(t,""))
                        {env with term = Map.add name t env.term}
                    (term, gen), Map.add name body_var s
                    ) env.term l'
        let _ =
            let env = {env with term = m}
            List.iter (fun (term, _) -> term env) l
        List.fold (fun env (_, gen) -> gen env) env l
    and ty_init scope env s x = 
        ty scope env s x
        assert_foralls_used errors (range_of_texpr x) s
    and ty scope env s x = ty' scope false env s x
    and ty' scope is_in_left_apply (env : Env) s x =
        let f s x = ty scope env s x
        match x with
        | RawTTypecase _ -> failwith "Compiler error: Type level typecase should not appear in the top down segment."
        | RawTWildcard r -> hover_types.AddHover(r,(s,""))
        | RawTArray(r,a) -> 
            let v = fresh_var scope
            unify r s (TyArray v)
            f v a
        | RawTVar(r,x) ->
            match v_ty env x with
            | Some (TyModule _ & m) when is_in_left_apply = false -> hover_types.AddHover(r,(m,"")); errors.Add(r,ModuleMustBeImmediatelyApplied)
            | Some x -> unify r s x; hover_types.AddHover(r,(x,""))
            | None -> errors.Add(r, UnboundVariable x)
        | RawTB r -> unify r s TyB
        | RawTLit(r,x) -> unify r s (TyLit x)
        | RawTSymbol(r,x) -> unify r s (TySymbol x)
        | RawTPrim(r,x) -> unify r s (TyPrim x)
        | RawTPair(r,a,b) -> 
            let q,w = fresh_var scope, fresh_var scope
            unify r s (TyPair(q,w))
            f q a; f w b
        | RawTFun(r,a,b,t) ->
            let q,w = fresh_var scope, fresh_var scope
            unify r s (TyFun(q,w,t))
            f q a; f w b
        | RawTRecord(r,l) ->
            let l' = Map.map (fun _ _ -> fresh_var scope) l
            unify r s (TyRecord l')
            Map.iter (fun k s -> f s l.[k]) l'
        | RawTUnion(r,l,lay,_) ->
            let l' = Map.map (fun _ (is_gadt,_) -> is_gadt, fresh_var scope) l
            unify r s (TyUnion(l',lay))
            Map.iter (fun k (is_gadt,s) -> let x = snd l.[k] in if is_gadt then ty scope {env with ty = Map.empty} s x else f s x) l'
        | RawTExists(r,a,b) ->
            let a = List.map (typevar_to_var scope env.constraints) a
            let body_var = fresh_var scope
            ty scope {env with ty = List.fold (fun s a -> Map.add a.name (tyvar a) s) env.ty a} body_var b
            unify r s (TyExists(a, body_var))
        | RawTForall(r,a,b) ->
            let a = typevar_to_var scope env.constraints a
            let body_var = fresh_var scope
            ty scope {env with ty = Map.add a.name (tyvar a) env.ty} body_var b
            unify r s (TyForall(a, body_var))
        | RawTApply(r,a',b) ->
            let f' b k x = let v = fresh_var' scope k in ty' scope b env v x; visit_t v
            match f' true (fresh_kind ()) a' with
            | TyModule l -> 
                match f' false KindType b with
                | TySymbol x ->
                    match Map.tryFind x l with
                    | Some (TyModule _ as a) ->
                        if is_in_left_apply then 
                            unify r s a
                            match b with RawTSymbol(r,_) -> hover_types.AddHover(r,(a,"")) | _ -> ()
                        else errors.Add(r,ModuleMustBeImmediatelyApplied)
                    | Some a -> 
                        unify r s a
                        match b with
                        | RawTSymbol(r,_) -> 
                            let com = match a with TyComment(com,_) -> com | _ -> ""
                            hover_types.AddHover(r,(a,com))
                        | _ -> ()
                    | None -> errors.Add(r,ModuleIndexFailed x)
                | b -> errors.Add(r,ExpectedSymbolAsRecordKey b)
            | TyInl(a,body) -> let v = fresh_var' scope a.kind in f v b; unify r s (subst [a,v] body)
            | a -> 
                let q,w = fresh_kind(), fresh_kind()
                unify_kind (range_of_texpr a') (tt top_env a) (KindFun(q,w))
                let x = fresh_var' scope q
                f x b
                unify r s (TyApply(a,x,w))
        | RawTTerm(r,a) -> assert_bound_vars env a; unify r s (TySymbol "<term>")
        | RawTMacro(r,a) ->
            List.map (function
                | RawMacroText(_,a) -> TMText a
                | RawMacroTerm _ -> failwith "Compiler error: Term vars should never appear at the type level."
                | RawMacroType(r,a) | RawMacroTypeLit(r,a) -> let v = fresh_var scope in f v a; TMVar v
                ) a
            |> TyMacro |> unify r s
        | RawTLayout(r,a,b) -> 
            let v = fresh_var scope
            unify r s (TyLayout(v,b))
            f v a
        | RawTFilledNominal _ -> failwith "Compiler error: RawTNominal should be filled in by the inferencer."
        | RawTMetaVar _ -> failwith "Compiler error: This particular metavar is only for typecase's clauses. This happens during the bottom-up segment."
    and pattern (scope : Scope) (env : Env) s a : T option ref ResizeArray * (T * T list * T) ResizeArray * (Scope * Env) = 
        let gadt_links = ResizeArray()
        let gadt_typecases = ResizeArray()
        let term_vars = Dictionary(HashIdentity.Structural)
        let ty_vars = Dictionary(HashIdentity.Structural)
        let mutable scope = scope
        let update_env () =
            scope,
            {env with
                ty = (env.ty,ty_vars) ||> Seq.fold (fun s x -> Map.add x.Key x.Value s)
                term = (env.term,term_vars) ||> Seq.fold (fun s x -> Map.add x.Key x.Value s)
                }
        let ho_make (i : GlobalId) (l : Var list) =
            let h = TyNominal i
            let l' = List.map (fun (x : Var) -> x, fresh_subst_var scope x.constraints x.kind) l
            List.fold (fun s (_,x) -> match tt top_env s with KindFun(_,k) -> TyApply(s,x,k) | _ -> failwith "impossible") h l', l'
        let rec ho_index x =
            match visit_t x with
            | TyApply(a,_,_) -> ho_index a 
            | TyNominal i -> ValueSome i
            | _ -> ValueNone
        let rec ho_fun x = 
            match visit_t x with
            | TyFun(_,a,_) | TyForall(_,a) -> ho_fun a
            | a -> ho_index a
        let rec loop s x : unit =
            let f = loop
            match x with
            | PatFilledDefaultValue _ -> failwith "Compiler error: PatDefaultValueFilled should not appear during inference."
            | PatB r -> unify r s TyB
            | PatE r -> hover_types.AddHover(r,(s,""))
            | PatVar(r,a) ->
                match term_vars.TryGetValue(a) with
                | true, v -> unify r s v
                | _ -> term_vars.Add(a,s)
                hover_types.AddHover(r,(s,""))
            | PatDyn(_,a) -> f s a
            | PatAnnot(_,a,b) -> ty_init scope env s b; f s a
            | PatWhen(_,a,b) -> 
                f s a
                let scope,env = update_env()
                term scope env (TyPrim BoolT) b
            | PatPair(r,a,b) ->
                let q,w = fresh_var scope, fresh_var scope
                unify r s (TyPair(q,w))
                loop q a; loop w b
            | PatSymbol(r,a) -> unify r s (TySymbol a)
            | PatOr(_,a,b) | PatAnd(_,a,b) -> loop s a; loop s b
            | PatValue(r,a) -> unify r s (lit a)
            | PatDefaultValue(r,_) -> 
                annotations.Add(x,(r,s))
                unify r s (fresh_subst_var scope (Set.singleton CNumber) KindType)
                hover_types.AddHover(r,(s,""))
            | PatRecordMembers(r,l) ->
                let l =
                    List.choose (function
                        | PatRecordMembersSymbol((r,a),b) -> Some (a,b)
                        | PatRecordMembersInjectVar((r,a),b) ->
                            match v_term env a with
                            | Some (com,TySymbol a & x) -> hover_types.AddHover(r,(x,com)); Some (a,b)
                            | Some (_,x) -> errors.Add(r, ExpectedSymbolAsRecordKey x); None
                            | None -> errors.Add(r, UnboundVariable a); None
                        ) l
                match visit_t s with
                | TyRecord l' as s ->
                    let l, missing =
                        List.mapFoldBack (fun (a,b) missing ->
                            match l' |> Map.tryPick (fun (i, k) v -> if k = a then Some (i, v) else None) with
                            | Some (_,x) -> (x,b), missing
                            | None -> (fresh_var scope,b), a :: missing
                            ) l []
                    if List.isEmpty missing = false then errors.Add(r, MissingRecordFieldsInPattern(s, missing))
                    List.iter (fun (a,b) -> loop a b) l
                | s ->
                    let l =
                        List.mapi (fun i (a,b) -> 
                            let v = fresh_var scope
                            loop v b
                            (i, a), v
                            ) l
                    unify r s (l |> Map |> TyRecord)
            | PatExists(r,l,p) ->
                l |> List.iter (fun (r,name) -> if Map.containsKey name env.ty then errors.Add(r,ShadowedExists))
                match visit_t s with
                | TyExists(type_var_list,type_body) ->
                    if l.Length = type_var_list.Length then
                        scope <- scope + 1
                        let vars = (l, type_var_list) ||> List.map2 (fun (_,name) l -> 
                            Utils.memoize ty_vars (fun name -> tyvar {l with scope=scope; name=name}) name
                            )
                        loop (subst (List.zip type_var_list vars) type_body) p
                    else
                       errors.Add(r, UnexpectedNumberOfArgumentsInExistsPattern(l.Length,type_var_list.Length))
                | s -> errors.Add(r, ExpectedExistentialInPattern s)
            | PatUnbox(r,name,a) ->
                let assume i =
                    let n = top_env.nominals.[i]
                    match n.body with
                    | TyUnion(cases,_) ->
                        let x,m = ho_make i n.vars
                        unify r s x
                        match Map.tryPick (fun (_, name') v -> if name = name' then Some v else None) cases with
                        | Some (is_gadt, v) -> 
                            if is_gadt then 
                                scope <- scope + 1
                                let forall_vars,body,specialized_constructor = gadt_extract scope v
                                gadt_typecases.Add(s, forall_vars, specialized_constructor)
                                match a with PatE r' when r = r' -> () | _ -> loop body a // This check for PatE is so the hovers for it don't overwrite the main pattern.
                                unify_gadt (Some gadt_links) r s specialized_constructor
                            else
                                match a with PatE r' when r = r' -> () | _ -> f (subst m v) a
                            hover_types.AddHover(r,(s,""))
                        | None -> errors.Add(r,CasePatternNotFoundForType(i,name)); f (fresh_var scope) a
                    | _ -> errors.Add(r,NominalInPatternUnbox i); f (fresh_var scope) a
                match ho_index s with
                | ValueSome i -> assume i
                | ValueNone ->
                    match v_term env name with
                    | Some (_,x) -> 
                        match ho_fun x with
                        | ValueSome i -> assume i
                        | ValueNone -> errors.Add(r,CannotInferCasePatternFromTermInEnv x); f (fresh_var scope) a
                    | None -> errors.Add(r,CasePatternNotFound name); f (fresh_var scope) a
            | PatNominal(_,(r,name),l,a) ->
                match v_ty env name with
                | Some x -> 
                    let rec loop r x = function
                        | (r,name) :: l ->
                            match x with
                            | TyModule x ->
                                match Map.tryFind name x with
                                | Some x -> loop r x l
                                | None -> errors.Add(r,ModuleIndexFailed name); f (fresh_var scope) a
                            | _ ->
                                errors.Add(r,ExpectedModule x); f (fresh_var scope) a
                        | [] ->
                            match ho_index x with
                            | ValueSome i ->
                                let n = top_env.nominals.[i]
                                match n.body with
                                | TyUnion _ -> errors.Add(r,UnionInPatternNominal i); f (fresh_var scope) a
                                | _ -> let x,m = ho_make i n.vars in unify r s x; f (subst m n.body) a
                            | ValueNone -> errors.Add(r,TypeInEnvIsNotNominal x); f (fresh_var scope) a
                    loop r x l
                | _ -> errors.Add(r,UnboundVariable name); f (fresh_var scope) a
            | PatArray(r,a) ->
                let v = fresh_var scope
                unify r s (TyArray v)
                List.iter (fun x -> loop v x) a
        loop s a
        gadt_links, gadt_typecases, update_env()

    let nominal_term global_id tt name vars v =
        let constructor body =
            let t,_ = List.fold (fun (a,k) b -> let k = trim_kind k in TyApply(a,tyvar b,k),k) (TyNominal global_id,tt) vars
            let x = match body with TyB -> t | _ -> TyFun(body,t,FT_Vanilla)
            List.foldBack (fun var ty -> TyForall(var,ty)) vars x
        match v with
        | TyUnion(l,_) -> Map.fold (fun s (_,name) (is_gadt,v) -> Map.add name (if is_gadt then v else constructor v) s) Map.empty l
        | _ -> Map.add name (constructor v) Map.empty

    let psucc = Hopac.Job.thunk >> Hopac.Hopac.memo
    let pfail = Hopac.Promise.Now.withFailure (System.Exception "Compiler error: Tried to read from a FilledTop that has errors.")

    let top_env_nominal top_env (global_id : GlobalId) tt name vars v =
        { top_env with
            nominals_next_tag = max top_env.nominals_next_tag global_id.tag + 1
            nominals_aux = Map.add global_id {|kind=tt; name=name|} top_env.nominals_aux
            nominals = Map.add global_id {|vars=vars; body=v|} top_env.nominals
            term = Map.foldBack Map.add (nominal_term global_id tt name vars v) top_env.term
            ty = Map.add name (TyNominal global_id) top_env.ty
            }

    let rec typevar = function
        | RawKindWildcard | RawKindStar -> KindType
        | RawKindFun(a,b) -> KindFun(typevar a, typevar b)
    let hovars (x : HoVar list) = 
        List.mapFold (fun s (_,(n,t)) -> 
            let v = {scope=0; kind=typevar t; name=n; constraints=Set.empty}
            v, Map.add n (tyvar v) s
            ) Map.empty x

    let scope = 0
    let bundle_nominal_rec l' =
        let l,_ =
            List.mapFold (fun i (_,name,vars,body) ->
                let l,env = hovars vars
                let tt = List.foldBack (fun (x : Var) s -> KindFun(x.kind,s)) l KindType
                (at_tag i,name,l,env,tt,body), i+1
                ) top_env.nominals_next_tag l'

        top_env <-
            {top_env with 
                nominals_aux = (top_env.nominals_aux, l) ||> List.fold (fun s (i,(_,name),_,_,tt,_) -> Map.add i {|name=name; kind=tt|} s)
                ty = (top_env.ty, l) ||> List.fold (fun s (i,(_,name),_,_,_,_) -> Map.add name (TyNominal i) s) 
                }
        
        List.fold (fun top_env (global_id,(r,name),vars,env_ty,tt,body) ->
            let v = fresh_var scope
            ty_init scope {term=Map.empty; ty=env_ty; constraints=Map.empty} v body
            let v = term_subst v
            validate_nominal errors global_id body v
            top_env_nominal top_env global_id tt name vars v
            ) top_env_empty l

    match expr with
    | BundleType(q,(r,name),vars',expr) ->
        let vars,env_ty = hovars vars'
        let v = fresh_var scope
        ty_init scope {term=Map.empty; ty=env_ty; constraints=Map.empty} v expr
        let t = List.foldBack (fun x s -> TyInl(x,s)) vars (term_subst v)
        hover_types.AddHover(r,(t,""))
        if 0 = errors.Count then psucc (fun () -> FType(q,(r,name),vars',expr)), AInclude {top_env_empty with ty = Map.add name t Map.empty}
        else pfail, AInclude top_env_empty
    | BundleNominal(q,(r,name),vars',expr) ->
        let x = bundle_nominal_rec [q,(r,name),vars',expr]
        if 0 = errors.Count then psucc (fun () -> FNominal(q,(r,name),vars',expr)), AInclude x
        else pfail, AInclude top_env_empty
    | BundleNominalRec l ->
        let _ = // Checks that mutually recursive unions do not have duplicates.
            let h = HashSet()
            l |> List.iter (fun (_,_,_,x) ->
                match x with
                | RawTUnion(_,l,_,_) -> l |> Map.iter (fun k v -> if h.Add k = false then errors.Add(range_of_texpr (snd v),DuplicateKeyInRecUnion))
                | _ -> ()
                )
        let x = bundle_nominal_rec l
        if 0 = errors.Count then psucc (fun () -> FNominalRec l), AInclude x
        else pfail, AInclude top_env_empty
    | BundlePrototype(com,r,(r',name),(w,var_init),vars',expr) ->
        let i = at_tag top_env'.prototypes_next_tag
        let cons = CPrototype i
        let scope = 0
        let vars,env_ty = typevars scope {term=Map.empty; constraints=Map.empty; ty=Map.empty} vars'
        let kind = List.foldBack (fun (k : Var) s -> KindFun(k.kind,s)) vars KindType
        let v' = {scope=scope; constraints=Set.singleton cons; name=var_init; kind=kind}
        let env_ty = Map.add var_init (tyvar v') env_ty
        let vars = v' :: vars
        let v = fresh_var scope
        ty_init scope {term=Map.empty; ty=env_ty; constraints=Map.empty} v expr
        let body = List.foldBack (fun a b -> TyForall(a,b)) vars (term_subst v)
        if 0 = errors.Count && (assert_foralls_used errors r' body; 0 = errors.Count) then
            let x =
                { top_env_empty with
                    prototypes_next_tag = i.tag + 1
                    prototypes = Map.add i {|name=name; signature=body; kind=v'.kind|} Map.empty
                    term = Map.add name (if com <> "" then TyComment(com,body) else body) Map.empty
                    constraints = Map.add name (C cons) Map.empty
                    }
            psucc (fun () -> FPrototype(r,(r',name),(w,var_init),vars',expr)), AInclude x
        else pfail, AInclude top_env_empty
    | BundleInl(com,q,(_,name as w),a,true) ->
        let env = inl scope {term=Map.empty; ty=Map.empty; constraints=Map.empty} (w,a)
        let term = 
            let x = env.term.[name]
            if com <> "" then TyComment(com, x) else x
        (if 0 = errors.Count then psucc (fun () -> FInl(q,w,fill q Map.empty a)) else pfail), 
        AInclude { top_env_empty with term = Map.add name term Map.empty}
    | BundleInl(com,q,(_,name as w),a,false) ->
        assert_bound_vars {term=Map.empty; ty=Map.empty; constraints=Map.empty} a
        (if 0 = errors.Count then psucc (fun () -> FInl(q,w,a)) else pfail),
        AInclude { top_env_empty with term = Map.add name (TySymbol "<real>") Map.empty }
    | BundleRecInl(l,is_top_down) ->
        let _ =
            let h = HashSet()
            List.iter (fun (_,_,(r,n),_) -> if h.Add n = false then errors.Add(r,DuplicateRecInlName)) l
        let env_term =
            if is_top_down then
                let l = List.map (fun (com,_,a,b) -> a,b) l
                (rec_block scope {term=Map.empty; ty=Map.empty; constraints=Map.empty} l).term
            else
                let env_term = List.fold (fun s (com,_,(_,a),_) -> Map.add a (TySymbol "<real>") s) Map.empty l
                l |> List.iter (fun (com,_,_,x) -> assert_bound_vars {term = env_term; ty = Map.empty; constraints=Map.empty} x)
                env_term
        let filled_top =
            if 0 = errors.Count then
                if is_top_down then psucc (fun () -> FRecInl(List.map (fun (_,a,b,c) -> a,b,fill a env_term c) l))
                else psucc (fun () -> FRecInl(List.map (fun (_,a,b,c) -> a,b,c) l))
            else pfail
        let env_term = 
            List.fold (fun env_term (com,_,(_,n),_) ->
                if com <> "" then Map.add n (TyComment(com, Map.find n env_term)) env_term else env_term
                ) env_term l
        filled_top, AInclude (Map.fold (fun s k v -> {s with term = Map.add k v s.term}) top_env_empty env_term)
    | BundleInstance(r,prot,ins,vars,body) ->
        let fail = pfail,AInclude top_env_empty
        let assert_no_kind x = x |> List.iter (fun ((r,(_,k)),_) -> match k with RawKindWildcard -> () | _ -> errors.Add(r,KindNotAllowedInInstanceForall))
        let assert_vars_count vars_count vars_expected = if vars_count <> vars_expected then errors.Add(r,InstanceCoreVarsShouldMatchTheArityDifference(vars_count,vars_expected))
        let assert_kind_compatibility got expected =
            try unify_kind' InstanceKindError r got expected
            with :? TypeErrorException as e -> errors.AddRange e.Data0
        let assert_kind_arity prot_kind_arity ins_kind_arity = if ins_kind_arity < prot_kind_arity then errors.Add(r,InstanceArityError(prot_kind_arity,ins_kind_arity))
        let assert_instance_forall_does_not_shadow_prototype_forall prot_forall_name = List.iter (fun ((r,(a,_)),_) -> if a = prot_forall_name then errors.Add(r,InstanceVarShouldNotMatchAnyOfPrototypes)) vars
        let assert_orphan_shadow_check (prot_id : GlobalId) (ins_id : GlobalId) =
            // if Map.containsKey (prot_id, ins_id) top_env.prototypes_instances
            // then errors.Add(r,ShadowedInstance)
            ()
        let assert_orphan_instance_check (prot_id : GlobalId) (ins_id : GlobalId) =
            // if (prot_id.package_id = package_id || ins_id.package_id = package_id) = false then errors.Add(r,OrphanInstance)
            ()
        let body prot_id ins_id = 
            let ins_kind' = top_env.nominals_aux.[ins_id].kind
            let guard next = if 0 = errors.Count then next () else fail
            let ins_kind = kind_get ins_kind'
            let prototype = top_env.prototypes.[prot_id]
            hover_types.AddHover(fst prot, (prototype.signature,"")) // TODO: Add the hover for the instance signature.
            let prototype_init_forall_kind = prototype_init_forall_kind prototype.signature
            let prot_kind = kind_get prototype_init_forall_kind
            assert_kind_arity prot_kind.arity ins_kind.arity
            guard <| fun () ->
            let vars_expected = ins_kind.arity - prot_kind.arity
            assert_kind_compatibility (List.skip vars_expected ins_kind.args |> List.reduceBack (fun a b -> KindFun(a,b))) prototype_init_forall_kind
            guard <| fun () ->
            assert_vars_count (List.length vars) vars_expected
            guard <| fun () ->
            assert_no_kind vars
            guard <| fun () ->
            let ins_vars, env_ty =
                List.mapFold (fun s (((r,_),_) & x,k) ->
                    let v = {typevar_to_var scope Map.empty x with kind = k}
                    let x = tyvar v
                    hover_types.AddHover(r,(x,""))
                    x, Map.add v.name x s
                    ) Map.empty (List.zip vars (List.take vars_expected ins_kind.args))
            let ins_constraints = ins_vars |> List.map (visit_t >> function TyVar (x,_) -> x.constraints | _ -> failwith "impossible")
            let ins_core, _ = List.fold (fun (a,k) (b : T) -> let k = trim_kind k in TyApply(a,b,k),k) (TyNominal ins_id,ins_kind') ins_vars
            let env_ty, prot_body =
                match foralls_ty_get prototype.signature with
                | (prot_core :: prot_foralls), prot_body ->
                    List.fold (fun ty x ->
                        assert_instance_forall_does_not_shadow_prototype_forall x.name
                        Map.add x.name (tyvar x) ty) env_ty prot_foralls,
                    let prot_body = subst [prot_core, ins_core] prot_body
                    let _ =
                        List.foldBack (fun x s -> TyForall(x,s)) prot_foralls prot_body
                        |> List.foldBack (fun x s -> match visit_t x with TyVar(x,_) -> TyForall(x,s) | _ -> failwith "impossible") ins_vars
                        |> fun x -> generalized_statements.Add(body,x)
                    prot_body
                | _ -> failwith "impossible"
            assert_orphan_shadow_check prot_id ins_id
            assert_orphan_instance_check prot_id ins_id
            guard <| fun () ->
            top_env <- {top_env with prototypes_instances = Map.add (prot_id,ins_id) ins_constraints top_env.prototypes_instances}
            term scope {term=Map.empty; ty=env_ty; constraints=Map.empty} prot_body body
            (if 0 = errors.Count then psucc (fun () -> FInstance(r,(fst prot, prot_id),(fst ins, ins_id),fill r Map.empty body)) else pfail),
            AInclude {top_env_empty with prototypes_instances = Map.add (prot_id,ins_id) ins_constraints Map.empty}
            
        let fake _ = fail
        let check_ins on_succ =
            match Map.tryFind (snd ins) top_env.ty with
            | None -> errors.Add(fst ins, UnboundVariable (snd ins)); fail
            | Some(TyNominal i') -> on_succ i'
            | Some x -> errors.Add(fst ins, ExpectedHigherOrder x); fail
        match Map.tryFind (snd prot) top_env.constraints with
        | None -> errors.Add(fst prot, UnboundVariable (snd prot)); check_ins fake
        | Some(C (CPrototype i)) -> check_ins (body i)
        | Some(C x) -> errors.Add(fst prot, ExpectedPrototypeConstraint x); check_ins fake
        | Some(M _) -> errors.Add(fst prot, ExpectedPrototypeInsteadOfModule); check_ins fake
    | BundleOpen(q,(r,a),b) ->
        match module_open (Some hover_types) (loc_env top_env) Map.empty r a b with
        | Ok x -> psucc (fun () -> FOpen(q,(r,a),b)), AOpen {top_env_empty with term=x.term; ty=x.ty; constraints=x.constraints}
        | Error er -> errors.Add(er); pfail, AOpen top_env_empty
    |> fun (filled_top, top_env_additions) -> 
        if 0 = errors.Count then
            annotations |> Seq.iter (fun (KeyValue(_,(r,x))) -> if has_metavars x then errors.Add(r, ValueRestriction x))
        {
        filled_top = filled_top
        top_env_additions = top_env_additions
        offset = bundle_range expr |> fst |> fun x -> x.line
        hovers = hover_types.ToArray() |> Array.map (fun (a,(b,com)) -> a, let b = show_t top_env b in if com <> "" then sprintf "%s\n---\n%s" b com else b)
        errors = errors |> Seq.toList |> List.map (fun (a,b) -> a, show_type_error top_env b)
        }

let base_types (default_env : Startup.DefaultEnv) =
    let var name = {scope=0; kind=KindType; constraints=Set.empty; name=name} 
    let inline inl f = let x = var "x" in TyInl(x,f x)
    let inline inl2 f = let x,y = var "x", var "y" in TyInl(x,TyInl(y,f x y))
    [
    "i8", TyPrim Int8T
    "i16", TyPrim Int16T
    "i32", TyPrim Int32T
    "i64", TyPrim Int64T
    "u8", TyPrim UInt8T
    "u16", TyPrim UInt16T
    "u32", TyPrim UInt32T
    "u64", TyPrim UInt64T
    "f32", TyPrim Float32T
    "f64", TyPrim Float64T
    "string", TyPrim StringT
    "bool", TyPrim BoolT
    "char", TyPrim CharT
    "array_base", inl (fun x -> TyArray(tyvar x))
    "heap", inl (fun x -> TyLayout(tyvar x,Layout.Heap))
    "mut", inl (fun x -> TyLayout(tyvar x,Layout.HeapMutable))
    "stack_mut", inl (fun x -> TyLayout(tyvar x,Layout.StackMutable))
    "fptr", inl2 (fun x y -> TyFun(tyvar x,tyvar y,FT_Pointer))
    "closure", inl2 (fun x y -> TyFun(tyvar x,tyvar y,FT_Closure))
    "int", TyPrim default_env.default_int
    "uint", TyPrim default_env.default_uint
    "float", TyPrim default_env.default_float
    ]

let top_env_default default_env : TopEnv = 
    // Note: `top_env_default` should have no nominals, prototypes or terms.
    {top_env_empty with 
        ty = Map.ofList (base_types default_env)
        constraints =
            [
            "uint", CUInt
            "sint", CSInt
            "int", CInt
            "float", CFloat
            "number", CNumber
            "prim", CPrim
            "record", CRecord
            "symbol", CSymbol
            ] |> Map.ofList |> Map.map (fun _ -> C)
        }
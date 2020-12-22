﻿module Spiral.StreamServer.Prepass

open System
open System.IO
open System.Collections.Generic
open FSharpx.Collections

open VSCTypes
open Spiral.PartEval
open Spiral.Infer
open Spiral.PartEval.Prepass
open Spiral.StreamServer.Main

open Hopac
open Hopac.Infixes
open Hopac.Extensions
open Hopac.Stream

type PrepassPackageEnv = {
    prototypes_instances : Map<int, Map<GlobalId * GlobalId,E>>
    nominals : Map<int, Map<GlobalId,{|body : T; name : string|}>>
    term : Map<string,E>
    ty : Map<string,T>
    has_errors : bool
    }

let union small big = {
    prototypes_instances = Map.foldBack Map.add small.prototypes_instances big.prototypes_instances
    nominals = Map.foldBack Map.add small.nominals big.nominals
    term = Map.foldBack Map.add small.term big.term
    ty = Map.foldBack Map.add small.ty big.ty
    has_errors = small.has_errors || big.has_errors
    }
    
let in_module m (a : PrepassPackageEnv) =
    {a with 
        ty = Map.add m (TModule a.ty) Map.empty
        term = Map.add m (EModule a.term) Map.empty
        }

let package_env_empty = {
    prototypes_instances = Map.empty
    nominals = Map.empty
    term = Map.empty
    ty = Map.empty
    has_errors = false
    }

let package_env_default = { package_env_empty with ty = top_env_default.ty }

let package_to_top (x : PrepassPackageEnv) = {
    nominals_next_tag = 0
    nominals = Map.foldBack (fun _ -> Map.foldBack Map.add) x.nominals Map.empty
    prototypes_next_tag = 0
    prototypes_instances = Map.foldBack (fun _ -> Map.foldBack Map.add) x.prototypes_instances Map.empty
    ty = x.ty
    term = x.term
    has_errors = x.has_errors
    }

let top_to_package package_id (small : PrepassTopEnv) (big : PrepassPackageEnv): PrepassPackageEnv = {
    nominals = Map.add package_id small.nominals big.nominals
    prototypes_instances = Map.add package_id small.prototypes_instances big.prototypes_instances
    ty = small.ty
    term = small.term
    has_errors = small.has_errors || big.has_errors
    }

type FileStream = EditorStream<InferResult Stream, PrepassTopEnv Promise>
let prepass package_id module_id path top_env =
    let rec main r =
        {new FileStream with
            member _.Run x = 
                let r = r()
                let rec loop top_env top_env_adds old_results = function
                    | Nil -> Job.result (top_env_adds, [])
                    | Cons(x : InferResult,xs) ->
                        if List.isEmpty x.errors then
                            x.filled_top >>= fun filled_top ->
                            match old_results with
                            | (filled_top',top_env,top_env_adds as r) :: rs when Object.ReferenceEquals(filled_top,filled_top') -> 
                                xs >>= loop top_env top_env_adds rs >>- fun (q,rs) -> q,r :: rs
                            | _ -> 
                                let top_env, top_env_adds =
                                    match (prepass package_id module_id path top_env).filled_top filled_top with
                                    | AOpen adds -> Prepass.union adds top_env, top_env_adds
                                    | AInclude adds -> Prepass.union adds top_env, Prepass.union adds top_env_adds
                                xs >>= loop top_env top_env_adds [] >>- fun (q,rs) -> q, (filled_top, top_env, top_env_adds) :: rs
                        else
                            Job.result ({top_env_adds with has_errors=true}, [])
                let l = 
                    top_env >>=* fun (top_env : PrepassTopEnv) ->
                    if top_env.has_errors then Job.result({top_env_empty with has_errors=true}, [])
                    else x >>= loop top_env top_env_empty r
                l >>-* fst, main (fun () -> if l.Full then Promise.Now.get l |> snd else r)
            }
    main (fun () -> [])

type ModuleId = int
type DiffableFileHierarchy = 
    DiffableFileHierarchyT<
        (PrepassTopEnv Promise * (ModuleId * PrepassTopEnv Promise)) option * InferResult Stream * FileStream option,
        (ModuleId * PrepassTopEnv Promise) option
        >
type ModuleTarget = string
type HasChanged = bool
type MultiFileStream = EditorStream<DiffableFileHierarchy list * ModuleTarget,PrepassTopEnv Promise option * PrepassTopEnv Promise>

let multi_file package_id top_env =
    let rec create files' =
        {new MultiFileStream with
            member _.Run((files,target)) =
                let files = diff_order_changed files' files
                let mutable res = None
                let on_res path r = if path = target then res <- Some r
                let x, files = multi_file_run on_res on_res top_env_empty prepass id Prepass.union Prepass.in_module package_id top_env files
                (res, x), create files
            }
    create []

type ModulePath = string
type PackageId = int
type PackageMultiFileLinks = (PackagePath * (PackageName option * PrepassPackageEnv Promise)) list
type PackageMultiFileStreamAux = EditorStream<DiffableFileHierarchy list * ModuleTarget, PrepassPackageEnv Promise option * PrepassPackageEnv Promise>
type PackageMultiFileStream = EditorStream<PackageId * PackageMultiFileLinks * (DiffableFileHierarchy list * ModuleTarget), PrepassPackageEnv Promise option * PrepassPackageEnv Promise>
type PackageStreamInput = PackageStreamInput of Map<PackageName,DiffableFileHierarchy list * PackageLinks * PackageId> * PackageName seq * ModuleTarget // Note: I get a 'Method name is too long.' exception unless I use this.
type PackageStream = EditorStream<PackageStreamInput, PrepassPackageEnv Promise option>

type PackageItem = {
    env_out : PrepassPackageEnv Promise
    links : (PackagePath * PackageName option) list
    stream : PackageMultiFileStream
    id : PackageId
    }
let package =
    let rec loop (s : Map<PackageName, PackageItem>) =
        {new PackageStream with
            member _.Run(PackageStreamInput(packages,order,target)) = 
                Seq.fold (fun (s,_) n ->
                    let old_package = Map.tryFind n s
                    let files, links, id = packages.[n]
                    let (target_res,env_out), stream =
                        let links = links |> List.map (fun (k, v) -> k, (v, s.[k].env_out))
                        let files = files
                        match old_package with
                        | Some p -> p.stream.Run(id,links,(files,target))
                        | None -> (package_multi_file Option.map multi_file package_env_default union in_module top_to_package package_to_top).Run(id,links,(files,target))
                    let s = Map.add n {env_out = env_out; stream = stream; id = id; links = links} s
                    s, target_res
                    ) (s,None) order
                |> fun (_,target_res) -> target_res, loop s
            }
    loop Map.empty
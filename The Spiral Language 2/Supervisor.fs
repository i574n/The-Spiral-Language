module Spiral.Supervisor

open VSCTypes
open Graph
open Spiral.ServerUtils

open System
open System.IO
open System.Collections.Generic

open Hopac
open Hopac.Infixes
open Hopac.Extensions
open Hopac.Stream

open Polyglot
open Polyglot.Common
open Lib
open SpiralFileSystem.Operators

type LocalizedErrors = {|uri : string; errors : RString list|}
type TracedError = {|trace : string list; message : string|}
type SupervisorErrorSources = {
    fatal : string Ch
    tokenizer : LocalizedErrors Ch
    parser : LocalizedErrors Ch
    typer : LocalizedErrors Ch
    package : LocalizedErrors Ch
    traced : TracedError Ch
    }

type SupervisorReq =
    | ProjectFileOpen of {|uri : string; spiprojText : string|}
    | ProjectFileChange of {|uri : string; spiprojText : string|}
    | ProjectFileLinks of {|uri : string|} * RString list IVar
    | ProjectCodeActions of {|uri : string|} * RAction list IVar
    | ProjectCodeActionExecute of {|uri : string; action : ProjectCodeAction|} * {|result : string option|} IVar
    | FileOpen of {|uri : string; spiText : string|}
    | FileChange of {|uri : string; spiEdit : SpiEdit|}
    | FileDelete of {|uris : string []|}
    | FileTokenRange of {|uri : string; range : VSCRange|} * int [] IVar
    | HoverAt of {|uri : string; pos : VSCPos|} * string option IVar
    | BuildFile of {|uri : string; backend : string|}

type SupervisorState = {
    packages : SchemaEnv
    modules : ModuleEnv
    packages_infer : ResultMap<PackageId,WDiff.ProjStateTC>
    packages_prepass : ResultMap<PackageId,WDiffPrepass.ProjStatePrepass>
    graph : MirroredGraph
    package_ids : Map<string,int> * Map<int,string>
    }

let proj_validate default_env s dirty_packages =
    let order,socs = Graph.circular_nodes s.graph dirty_packages
    let packages = ss_validate s.packages s.modules (order,socs)
    let packages_infer = wdiff_projenvr_tc default_env (fst s.package_ids) packages s.modules s.packages_infer (dirty_packages, order)
    order, {s with packages_infer = packages_infer; packages=packages}

let proj_graph_update default_env s dirty_packages =
    let removed_pids,package_ids = package_ids_update s.packages s.package_ids dirty_packages
    let packages_infer, packages_prepass = package_ids_remove s.packages_infer removed_pids, package_ids_remove s.packages_prepass removed_pids
    let graph = graph_update s.packages s.graph dirty_packages
    proj_validate default_env {s with graph = graph; package_ids = package_ids; packages_infer = packages_infer; packages_prepass = packages_prepass} dirty_packages

let proj_open default_env s (dir, text) =
    let packages,dirty_packages,modules = loader_package default_env s.packages s.modules (dir,text)
    proj_graph_update default_env {s with packages = packages; modules = modules} dirty_packages

let proj_revalidate_owner default_env s file =
    let rec loop (x : DirectoryInfo) =
        if x = null then [||], s
        else
            let x' = x.FullName |> SpiralFileSystem.standardize_path
            // trace Verbose (fun () -> $"""Supervisor.proj_revalidate_owner / x.FullName: {x.FullName |> SpiralSm.replace "\\" "|"} / x': {x'}""") _locals
            let x_ = x
            let x = {| FullName = x' |}
            if Map.containsKey x.FullName s.packages then proj_validate default_env s (HashSet [x.FullName])
            elif File.Exists(spiproj_suffix x.FullName) then proj_open default_env s (x.FullName,None)
            else loop x_.Parent
    loop (Directory.GetParent(file))

let file_delete default_env s (files : string []) =
    let deleted_modules = HashSet()
    let deleted_packages = HashSet()
    files |> Array.iter (fun k ->
        s.packages |> Map.iter (fun k' _ -> if (spiproj_suffix k').StartsWith(k) then deleted_packages.Add(k') |> ignore)
        s.modules |> Map.iter (fun k' _ -> if k'.StartsWith(k) then deleted_modules.Add(k') |> ignore)
        )
    let modules = Seq.foldBack Map.remove deleted_modules s.modules
    let packages = Seq.foldBack Map.remove deleted_packages s.packages
    let dirty_packages = HashSet(deleted_packages)
    let revalidate_parent (x : string) =
        trace Verbose (fun () -> $"Supervisor.file_delete.revalidate_parent.loop / x.FullName: {x}") _locals
        let rec loop (x : DirectoryInfo) =
            if x <> null then
                let x' = x.FullName |> SpiralFileSystem.standardize_path
                let x = DirectoryInfo x'
                if Map.containsKey x.FullName s.packages then dirty_packages.Add(x.FullName) |> ignore
            else loop x.Parent
        loop(Directory.GetParent x)
    Seq.iter revalidate_parent deleted_modules; Seq.iter revalidate_parent deleted_packages
    Seq.toArray deleted_modules, proj_graph_update default_env {s with modules = modules; packages = packages} dirty_packages

type AttentionState = {
    modules : string Set * string list
    packages : string Set * string list
    old_packages : SchemaEnv
    supervisor : SupervisorState
    }

let attention_server (errors : SupervisorErrorSources) (req : _ Ch) =
    let push path (s,o) = Set.add path s, path :: o
    let add (s,o) l = Array.foldBack (fun x (s,o as z) -> if Set.contains x s then z else Set.add x s, x :: o) l (s,o)
    let update (s : AttentionState) (modules,packages,supervisor) = {modules = add s.modules modules; packages = add s.packages packages; supervisor = supervisor; old_packages = s.supervisor.packages}
    let rec loop (s : AttentionState) =
        let clear uri =
            Hopac.start (Ch.send errors.tokenizer {|uri=uri; errors=[]|})
            Hopac.start (Ch.send errors.parser {|uri=uri; errors=[]|})
            Hopac.start (Ch.send errors.typer {|uri=uri; errors=[]|})
        let send_tokenizer uri x = Hopac.start (Ch.send errors.tokenizer {|uri=uri; errors=x|})
        let clear_parse uri = Hopac.start (Ch.send errors.parser {|uri=uri; errors=[]|})
        let clear_typer uri = Hopac.start (Ch.send errors.typer {|uri=uri; errors=[]|})
        let clear_old_package x = Map.tryFind x s.old_packages |> Option.iter (fun x ->
            let rec loop = function
                | SpiProj.FileHierarchy.File(_,(_,pdir),_) -> clear (Utils.file_uri pdir)
                | SpiProj.FileHierarchy.Directory(_,_,_,l) -> list l
            and list l = List.iter loop l
            list x.schema.modules
            )

        let inline body uri interrupt ers ers' src next = 
            Ch.Try.take req >>= function
            | Some x -> interrupt x
            | None ->
                if List.isEmpty ers then next ers'
                else
                    let ers = List.append ers ers'
                    Hopac.start (Ch.send src {|uri=uri; errors=ers|})
                    next ers

        let loop_module (s : AttentionState) mpath (m : WDiff.ModuleState) =
            let uri = Utils.file_uri mpath
            let interrupt x = loop (update {s with modules=push mpath s.modules} x)
            let rec bundler (r : BlockBundling.BlockBundleState) ers' = r >>= function
                | Cons((_,x),rs) -> body uri interrupt x.errors ers' errors.parser (bundler rs)
                | Nil -> loop s
            send_tokenizer uri m.tokenizer.errors
            clear_parse uri
            clear_typer uri
            bundler m.bundler []

        let rec loop_package (s : AttentionState) pdir = function
            | (mpath,l) :: ls ->
                let mpath' = mpath |> SpiralFileSystem.standardize_path
                let uri = Utils.file_uri mpath
                let pdir' = pdir |> SpiralFileSystem.standardize_path
                trace Verbose (fun () -> $"Supervisor.attention_server.loop.loop_package / mpath: {mpath} / mpath': {mpath'} / uri: {uri} / pdir: {pdir} / pdir': {pdir'}") _locals
                let interrupt x = loop (update {s with packages=push pdir s.packages} x)
                let rec typer (r : WDiff.TypecheckerStateValue Stream) ers' = r >>= function
                    | Cons((_,x,_),rs) -> body uri interrupt x.errors ers' errors.typer (typer rs)
                    | Nil -> loop_package s pdir' ls
                let rec bundler (r : BlockBundling.BlockBundleState) ers' = r >>= function
                    | Cons((_,x),rs) -> body uri interrupt x.errors ers' errors.parser (bundler rs)
                    | Nil -> clear_typer uri; typer l []
                let m = s.supervisor.modules.[mpath']
                send_tokenizer uri m.tokenizer.errors
                clear_parse uri
                bundler m.bundler []
            | [] -> loop s

        let package s =
            match s.packages with
            | se,x :: xs -> 
                let x' = x |> SpiralFileSystem.standardize_path
                trace Verbose (fun () -> $"Supervisor.attention_server.loop.package / x: {x} / x': {x'}") _locals
                let x = x'
                let s = {s with packages=Set.remove x se,xs}
                let package_errors = 
                    match Map.tryFind x s.supervisor.packages with 
                    | Some v -> List.concat [v.errors_parse; v.errors_modules; v.errors_packages]
                    | None -> []
                Hopac.start (Ch.send errors.package ({|uri=Utils.file_uri(spiproj_suffix x); errors=package_errors|}))
                clear_old_package x
                match Map.tryFind x (fst s.supervisor.package_ids) with
                | Some uid ->
                    match Map.tryFind uid s.supervisor.packages_infer.ok with
                    | Some v -> 
                        let path_tcvals =
                            let uids_file = v.files.uids_file
                            let rec loop x s = 
                                match x with
                                | WDiff.File(mid,path,_) ->
                                    let path' = path |> SpiralFileSystem.standardize_path
                                    trace Verbose (fun () -> $"Supervisor.attention_server.loop | WDiff.File(mid,path,_) / path: {path} / path': {path'}") _locals
                                    (path', (fst uids_file.[mid]).result) :: s
                                | WDiff.Directory(_,_,l) -> list l s
                            and list l s = List.foldBack loop l s
                            list v.files.files.tree []
                        loop_package s x path_tcvals
                    | None -> loop s
                | None -> loop s
            | _, [] -> req >>= (update s >> loop)

        match s.modules with
        | se,x :: xs -> 
            let s = {s with modules=Set.remove x se,xs}
            let x' = x |> SpiralFileSystem.standardize_path
            trace Verbose (fun () -> $"Supervisor.attention_server.loop / x: {x} / x': {x'}") _locals
            match Map.tryFind x s.supervisor.modules with
            | Some v ->
                let codeDir = x |> System.IO.Path.GetDirectoryName
                let tokensPath = codeDir </> "tokens.json"
                if tokensPath |> System.IO.File.Exists |> not
                then loop_module s x v
                else
                    trace Verbose (fun () -> $"Supervisor.attention_server.loop / tokens found, skipping / x: {x}") _locals
                    package s
            | None -> clear (Utils.file_uri x); package s
        | _,[] -> package s

    (req >>= fun (modules,packages,supervisor) -> 
        loop {modules = Set.ofArray modules, Array.toList modules; packages = Set.ofArray packages, Array.toList packages; supervisor = supervisor; old_packages = Map.empty}
        )

let show_position (s : SupervisorState) (x : PartEval.Prepass.Range) =
    let line = (fst x.range).line
    let col = (fst x.range).character
    let er_code = s.modules.[x.path].tokenizer.lines_text.[line]
    Text.StringBuilder()
        .AppendLine(sprintf "Error trace on line: %i, column: %i in module: %s ." (line+1) (col+1) x.path)
        .AppendLine(er_code)
        .Append(' ',col)
        .AppendLine("^")
        .ToString()

let show_trace s (x : PartEval.Main.Trace) (msg : string) =
    let rec loop (l : PartEval.Main.Trace) = function
        | (x : PartEval.Prepass.Range) :: xs -> 
            match l with
            | x' :: _ when x.path = x'.path && fst x.range = fst x'.range -> loop l xs
            | _ -> loop (x :: l) xs
        | _ -> l
    List.map (show_position s) (loop [] x), msg

type BuildResult =
    | BuildOk of {|code: string; file_extension : string|} list
    | BuildErrorTrace of string list * string
    | BuildFatalError of string
    | BuildSkip

let workspaceRoot = SpiralFileSystem.get_workspace_root ()
let targetDir = workspaceRoot </> "target/spiral_Eval"
let traceDir = targetDir </> "supervisor_trace"
let dir uri =
    let result = FileInfo(Uri(uri).LocalPath).Directory.FullName
    let result' = result |> SpiralFileSystem.standardize_path
    trace Verbose (fun () -> $"Supervisor.dir / uri: {uri} / result: {result} / result': {result'}") _locals
    result'
let file uri =
    let result =
        try
            FileInfo(Uri(uri).LocalPath).FullName
        with ex ->
            trace Verbose (fun () -> $"Supervisor.file / uri: {uri} / ex: %A{ex}") _locals
            uri
    let result' = result |> SpiralFileSystem.standardize_path
    // let result = result |> SpiralSm.replace "\\" "|"
    // trace Verbose (fun () -> $"Supervisor.file / uri: {uri} / result: {result} / result': {result'}") _locals
    result'
let supervisor_server (default_env : Startup.DefaultEnv) atten (errors : SupervisorErrorSources) req =
    let fatal x = Hopac.start (Ch.send errors.fatal x)
    let handle_packages (dirty_packages,s) = Hopac.start (Ch.send atten ([||],dirty_packages,s)); s
    let handle_file_packages file (dirty_packages,s) = Hopac.start (Ch.send atten ([|file|],dirty_packages,s)); s
    let handle_files_packages (dirty_files,(dirty_packages,s)) = Hopac.start (Ch.send atten (dirty_files,dirty_packages,s)); s
    let loop (s : SupervisorState) = req >>- function
        | ProjectFileChange x | ProjectFileOpen x -> proj_open default_env s (dir x.uri,Some x.spiprojText) |> handle_packages
        | FileOpen x -> 
            let file = file x.uri
            // trace Verbose (fun () -> $"Supervisor.supervisor_server.loop.FileOpen / x: %A{x} / file: {file}") _locals
            match Map.tryFind file s.modules with
            | Some m -> WDiff.wdiff_module_all default_env m x.spiText
            | None -> WDiff.wdiff_module_init_all default_env (is_top_down file) x.spiText
            |> fun v -> proj_revalidate_owner default_env {s with modules = Map.add file v s.modules} file
            |> handle_file_packages file
        | FileChange x ->
            let file = file x.uri
            trace Verbose (fun () -> $"Supervisor.supervisor_server.loop.FileChange / x: %A{x} / file: {file}") _locals
            match Map.tryFind file s.modules with
            | None -> fatal "It is not possible to apply a change to a file that is not present in the environment. Try reopening it in the editor."; s
            | Some m -> 
                match WDiff.wdiff_module_edit default_env m x.spiEdit with
                | Ok v -> proj_revalidate_owner default_env {s with modules = Map.add file v s.modules} file |> handle_file_packages file
                | Error er -> fatal er; s
        | FileDelete x ->
            trace Verbose (fun () -> $"Supervisor.supervisor_server.loop.FileDelete / x: {x}") _locals
            file_delete default_env s (Array.map file x.uris) |> handle_files_packages
        | ProjectFileLinks(x,res) -> 
            let l =
                match Map.tryFind (dir x.uri) s.packages with
                | None -> []
                | Some x ->
                    let mutable l = []
                    x.schema.packages |> List.iter (fun x ->
                        let r,dir = x.dir
                        trace Verbose (fun () -> $"Supervisor.supervisor_server.ProjectFileLinks / x.schema.packages |> List.iter / dir: {dir}") _locals
                        if Map.containsKey dir s.packages then l <- (r,Utils.file_uri (spiproj_suffix dir)) :: l
                        )
                    let rec loop = function
                        | SpiProj.FileHierarchy.Directory(_,_,_,l) -> list l
                        | SpiProj.FileHierarchy.File(_,(r,path),_) ->
                            trace Verbose (fun () -> $"Supervisor.supervisor_server.ProjectFileLinks.loop | SpiProj.FileHierarchy.File(_,(r,path),_) / path: {path}") _locals
                            if Map.containsKey path s.modules then l <- (r,Utils.file_uri path) :: l
                    and list l = List.iter loop l
                    list x.schema.modules
                    l
            Hopac.start (IVar.fill res l)
            s
        | ProjectCodeActions(x,res) ->
            let z =
                match Map.tryFind (dir x.uri) s.packages with
                | None -> []
                | Some x ->
                    let mutable z = []
                    let actions_dir (r,path) =
                        match r with
                        | None -> ()
                        | Some r ->
                            if Directory.Exists(path) then
                                z <- (r,RenameDirectory {|dirPath=path; target=null; validate_as_file=false|}) :: (r,DeleteDirectory {|dirPath=path; range=r|}) :: z
                            else
                                z <- (r,CreateDirectory {|dirPath=path|}) :: z
                    actions_dir x.schema.moduleDir
                    actions_dir x.schema.packageDir

                    let rec actions_module = function
                        | SpiProj.FileHierarchy.Directory(r',(r,path),_,l) -> 
                            trace Verbose (fun () -> $"Supervisor.supervisor_server.ProjectCodeActions.actions_module | SpiProj.FileHierarchy.Directory(r',(r,path),_,l) / path: {path}") _locals
                            if Directory.Exists(path) then
                                z <- (r,RenameDirectory {|dirPath=path; target=null; validate_as_file=true|}) :: (r,DeleteDirectory {|dirPath=path; range=r'|}) :: z
                            else
                                z <- (r,CreateDirectory {|dirPath=path|}) :: z
                            actions_modules l
                        | SpiProj.FileHierarchy.File(r',(r,path),_) -> 
                            trace Verbose (fun () -> $"Supervisor.supervisor_server.ProjectCodeActions.actions_module | SpiProj.FileHierarchy.File(r',(r,path),_) / path: {path}") _locals
                            if Map.containsKey path s.modules then 
                                z <- (r,RenameFile {|filePath=path; target=null|}) :: (r,DeleteFile {|range=r'; filePath=path|}) :: z
                            else 
                                z <- (r,CreateFile {|filePath=path|}) :: z
                    and actions_modules l = List.iter actions_module l
                    actions_modules x.schema.modules
                    z
            Hopac.start (IVar.fill res z)
            s
        | ProjectCodeActionExecute(x,res) ->
            let error, s =
                match code_action_execute x.action with
                | Error x -> Some x, s
                | Ok null -> None, proj_open default_env s (dir x.uri,None) |> handle_packages
                | Ok path -> None, file_delete default_env s [|path|] |> handle_files_packages
            Hopac.start (IVar.fill res {|result=error|})
            s
        | FileTokenRange(x, res) -> 
            let v =
                match Map.tryFind (file x.uri) s.modules with
                | Some v -> Some v
                | None when x.uri |> SpiralSm.ends_with ".dib" ->
                    x.uri
                    |> SpiralSm.replace "file:///" ""
                    |> File.ReadAllText
                    |> WDiff.wdiff_module_init_all default_env (is_top_down x.uri)
                    |> Some
                | None -> None

            match v with
            | Some v ->
                Hopac.start (BlockBundling.semantic_tokens v.parser >>= (Tokenize.vscode_tokens x.range >> IVar.fill res))
            | None ->
                if x.uri |> SpiralSm.starts_with "vscode-notebook-cell" |> not then
                    trace Debug
                        (fun () -> $"Supervisor.supervisor_server.FileTokenRange")
                        (fun () -> $"module=None / x.uri: {x.uri} / {_locals ()}")

                Hopac.start (IVar.fill res [||])
            s
        | HoverAt(x,res) -> 
            let file = file x.uri
            let pos = x.pos
            let _locals () = $"x: %A{x} / file: {file} / res: %A{res}"
            trace Verbose (fun () -> $"Supervisor.supervisor_server.HoverAt") _locals
            let go_hover x =
                match x with
                | None -> None
                | Some (x : Infer.InferResult) ->
                    x.hovers |> Array.tryPick (fun ((a,b),r) ->
                        if pos.line = a.line && (a.character <= pos.character && pos.character < b.character) then Some r else None
                        )
                |> (fun x ->
                    let _locals () = $"x: %A{x}"
                    trace Verbose (fun () -> $"Supervisor.supervisor_server.HoverAt.go_hover") _locals
                    x
                )
                |> IVar.fill res
            let go_block (x : WDiff.TypecheckerState) =
                let rec loop s (x : WDiff.TypecheckerStateValue Stream) =
                    x >>= function
                    | Nil -> go_hover s
                    | Cons((_,x,_),b) ->
                        // let _locals () = $"b: {b}"
                        // trace Verbose (fun () -> $"Supervisor.supervisor_server.HoverAt.go_block.loop Cons") _locals
                        if x.offset <= pos.line then loop (Some x) b
                        // If the block is over the offset that means the previous one must be the right choice.
                        else go_hover s
                Hopac.start (loop None x.result)
            let rec go_file uids_file trees = 
                let rec loop = function
                    | WDiff.File(uid,file',_) -> if file = file' then go_block (Array.get uids_file uid |> fst); true else false
                    | WDiff.Directory(_,_,l) -> list l
                and list l = List.exists loop l
                list trees
            let rec go_parent (x : DirectoryInfo) =
                trace Verbose (fun () -> $"Supervisor.supervisor_server.HoverAt.go_parent / x: %A{x}") _locals
                if x = null then false
                else
                    let path = x.FullName |> SpiralFileSystem.standardize_path
                    if Map.containsKey path s.packages then
                        let pid = (fst s.package_ids).[path]
                        match Map.tryFind pid s.packages_infer.ok with
                        | None -> false
                        | Some x -> go_file x.files.uids_file x.files.files.tree
                    else 
                        go_parent x.Parent
            if go_parent (Directory.GetParent(file)) = false then Hopac.start (IVar.fill res None)
            s
        | BuildFile x ->
            let backend = x.backend
            let file = file x.uri
            let _locals () = $"x: %A{x} / file: {file}"
            trace Verbose (fun () -> $"Supervisor.supervisor_server.BuildFile") _locals
            let handle_build_result = function
                | BuildOk l -> 
                    Job.fromUnitTask (fun () -> task {
                        for x in l do 
                            let file = Path.ChangeExtension(file,null) // null removes the extension from path.
                            do! IO.File.WriteAllTextAsync(file + x.file_extension, x.code)
                    })
                | BuildFatalError x -> Ch.send errors.fatal x
                | BuildErrorTrace(a,b) -> Ch.send errors.traced {|trace=a; message=b|}
                | BuildSkip ->
                    trace Debug (fun () -> $"Supervisor.supervisor_server.BuildFile.handle_build_result.BuildSkip") _locals
                    Job.unit ()
            let file_build (s : SupervisorState) mid (tc : WDiff.ProjStateTC, prepass : WDiffPrepass.ProjStatePrepass) =
                trace Verbose (fun () -> $"""Supervisor.supervisor_server.BuildFile.file_build / modules: %A{s.modules.Keys |> SpiralSm.concat ", "} / packages: %A{s.packages.Keys |> SpiralSm.concat ", "} / package_ids: %A{s.package_ids |> fst |> fun x -> x.Keys |> SpiralSm.concat ", "}""") _locals
                let a,b = tc.files.uids_file.[mid]
                let x,_ = prepass.files.uids_file.[mid]
                Hopac.start (a.state >>= fun (has_error',_) ->
                    b >>= fun (has_error,_) ->
                    if has_error || has_error' then fatal $"File {Path.GetFileNameWithoutExtension file} has a type error somewhere in its path."; Job.unit() else 
                    Stream.foldFun (fun _ (_,_,env) -> env) PartEval.Prepass.top_env_empty x.result >>= fun env ->
                    let body() =
                        match Map.tryFind "main" env.term with
                        | Some main ->
                            let prototypes_instances = Dictionary(env.prototypes_instances)
                            let nominals = 
                                let t = HashConsing.HashConsTable()
                                let d = Dictionary()
                                env.nominals |> Map.iter (fun k v -> d.Add(k, t.Add {|v with id=k|}))
                                d
                            try 
                                let inline build_many codegen backend =
                                    let (a,_),b = PartEval.Main.peval {prototypes_instances=prototypes_instances; nominals=nominals; backend=backend} main
                                    BuildOk (codegen file b a)
                                let build codegen backend file_extension =
                                    build_many (fun file b a -> [{|code = codegen b a; file_extension = file_extension|}]) backend
                                match backend with
                                | "Fsharp" -> build Codegen.Fsharp.codegen "Fsharp" ".fsx"
                                | "C" -> build Codegen.C.codegen "C" ".c"
                                | "Python + Cuda" -> build_many (Codegen.Python.codegen default_env) "Python"
                                | "Cpp + Cuda" -> build_many (Codegen.Cpp.codegen default_env) "CudaHost"
                                | "Cuda C++" -> BuildFatalError "The host C++ backend originally made for FPGAs, and then ported to Cuda has been removed in v2.10.0 of Spiral. Please use an earlier version to access it." // Date: 5/8/2024
                                | "Python" -> BuildFatalError "The prototype Python backend has been replaced by the Python + Cuda one in v2.5.0 of Spiral. Please use an earlier version to access it." // Date: 11/3/2023
                                | "UPMEM: Python + C" -> BuildFatalError "The UPMEM Python + C backend has been replaced by the Python + Cuda one in v2.5.0 of Spiral. Please use an earlier version to access it." // Date: 11/3/2023
                                | "HLS C++" -> BuildFatalError "The HLS C++ backend has been replaced by the Cuda one in v2.5.0 of Spiral. Please use an earlier version to access it." // Date: 10/17/2023
                                | "Cython*" | "Cython" -> BuildFatalError "The Cython backend has been replaced by the Python one in v2.3.1 of Spiral. Please use an earlier version to access it." // Date: 12/27/2022
                                | _ -> BuildFatalError $"Cannot recognize the backend: {backend}"
                            with
                                | :? PartEval.Main.TypeError as e -> BuildErrorTrace(show_trace s e.Data0 e.Data1)
                                | :? CodegenUtils.CodegenError as e -> BuildFatalError(e.Data1)
                                | :? CodegenUtils.CodegenErrorWithPos as e -> BuildErrorTrace(show_trace s e.Data0 e.Data1)
                                | ex ->
                                    if Directory.Exists traceDir then
                                        let guid = DateTime.Now |> SpiralDateTime.new_guid_from_date_time
                                        let trace_file = traceDir </> $"{guid}_error.json"
                                        async {
                                            try
                                                do! $"{ex}" |> SpiralFileSystem.write_all_text_async trace_file
                                            with ex ->
                                                trace Critical (fun () -> $"Supervisor.supervisor_server.BuildFile.file_build / ex: {ex |> Sm.format_exception}") _locals
                                        }
                                        |> Async.Start
                                    trace Critical (fun () -> $"Supervisor.supervisor_server.BuildFile.file_build / ex: %A{ex}") _locals
                                    BuildFatalError(ex.Message)
                        | None -> BuildFatalError $"Cannot find `main` in file {Path.GetFileNameWithoutExtension file}."

                    // The partial evaluator is using too much stack space, so as a temporary fix, I am running it on a separate thread with much more of it.
                    let result = IVar()
                    let thread = new Threading.Thread(Threading.ThreadStart(body >> IVar.fill result >> Hopac.start), 1 <<< 28) // Stack space = 2 ** 28 = 256mb.
                    thread.Start()
                    result >>= handle_build_result
                    )
            let file_find (s : SupervisorState) pdir =
                trace Verbose (fun () -> $"Supervisor.supervisor_server.BuildFile.file_find / pdir: {pdir}") _locals
                let uid = (fst s.package_ids).[pdir]
                match Map.tryFind uid s.packages_infer.ok, Map.tryFind uid s.packages_prepass.ok with
                | Some a, Some b ->
                    let rec loop = function
                        | WDiff.Directory(_,_,l) -> list l
                        | WDiff.File(mid,path,_) ->
                            trace Verbose (fun () -> $"Supervisor.supervisor_server.BuildFile.file_find.loop | WDiff.File(mid,path,_) / path: {path}") _locals
                            if file = path then file_build s mid (a, b); true else false
                    and list l = List.exists loop l
                    if list b.files.files.tree = false then fatal $"File {Path.GetFileNameWithoutExtension file} cannot be found in the project {spiproj_suffix pdir}"
                    s
                | None, None -> fatal $"Owner of file {Path.GetFileNameWithoutExtension file} has an error. Location: {spiproj_suffix pdir}"; s
                | _ -> failwith "Compiler error: The project status should be the same in both infer and prepass."
            let update_owner pdir =
                let order,dirty_packages = Graph.topological_sort' (fst s.graph) [pdir]
                let packages_prepass = wdiff_projenvr_prepass default_env (fst s.package_ids) s.packages s.packages_infer.ok s.packages_prepass (dirty_packages, order.ToArray())
                file_find {s with packages_prepass = packages_prepass} pdir
            let rec find_owner (x : DirectoryInfo) =
                if x = null then fatal $"Cannot find the package file of {file}"; s
                else
                    let x' = x.FullName |> SpiralFileSystem.standardize_path
                    trace Verbose (fun () -> $"""Supervisor.supervisor_server.BuildFile.find_owner / x.FullName: {x.FullName |> SpiralSm.replace "\\" "|"} / x': {x'}""") _locals
                    let x_ = x
                    let x = {| FullName = x' |}
                    if Map.containsKey x.FullName s.packages then update_owner x.FullName
                    else find_owner x_.Parent
            find_owner (Directory.GetParent(file))

    Job.iterateServer {
        packages = Map.empty
        modules = Map.empty
        packages_infer = {ok=Map.empty; error=Map.empty}
        packages_prepass = {ok=Map.empty; error=Map.empty}
        graph = mirrored_graph_empty
        package_ids = Map.empty, Map.empty
        } loop

type ClientReq =
    | ProjectFileOpen of {|uri : string; spiprojText : string|}
    | ProjectFileChange of {|uri : string; spiprojText : string|}
    | ProjectFileLinks of {|uri : string|}
    | ProjectCodeActionExecute of {|uri : string; action : ProjectCodeAction|}
    | ProjectCodeActions of {|uri : string|}
    | FileOpen of {|uri : string; spiText : string|}
    | FileChange of {|uri : string; spiEdit : SpiEdit|}
    | FileDelete of {|uris : string []|} // Also works for project files and directories.
    | FileTokenRange of {|uri : string; range : VSCRange|}
    | HoverAt of {|uri : string; pos : VSCPos|}
    | BuildFile of {|uri : string; backend : string|}
    | Ping of bool
    | Exit of bool

type ClientErrorsRes =
    | FatalError of string
    | TracedError of TracedError
    | PackageErrors of {|uri : string; errors : RString list|}
    | TokenizerErrors of {|uri : string; errors : RString list|}
    | ParserErrors of {|uri : string; errors : RString list|}
    | TypeErrors of {|uri : string; errors : RString list|}

open Microsoft.AspNetCore.SignalR
open FSharp.Json


type Supervisor = {
    supervisor_ch : SupervisorReq Ch
    }


type SpiralHub(supervisor : Supervisor) =
    inherit Hub()

    member _.ClientToServerMsg (x : string) =
        let job_null job = Hopac.start job; task { return null }
        
        let serialize (x : obj) = 
            match x with
            | null -> null
            | :? Option<string> as x -> x.Value
            | _ -> Json.serialize x

        let job_val job = let res = IVar() in Hopac.queueAsTask (job res >>=. IVar.read res >>- serialize)
        let supervisor = supervisor.supervisor_ch

        let client_req = Json.deserialize x

        if Directory.Exists traceDir then
            match client_req with
            | Ping _ -> ()
            | _ ->
                let req_name = client_req.GetType().Name
                let guid = DateTime.Now |> SpiralDateTime.new_guid_from_date_time
                let trace_file = traceDir </> $"{guid}_{req_name}.json"
                
                async {
                    do! Async.Sleep 500
                    try
                        do! x |> SpiralFileSystem.write_all_text_async trace_file
                    with ex ->
                        trace Critical (fun () -> $"SpiralHub.ClientToServerMsg / ex: {ex |> Sm.format_exception}") _locals
                }
                |> Async.Start

        match client_req with
        | ProjectFileOpen x -> job_null (supervisor *<+ SupervisorReq.ProjectFileOpen x)
        | ProjectFileChange x -> job_null (supervisor *<+ SupervisorReq.ProjectFileChange x)
        | ProjectCodeActionExecute x -> job_val (fun res -> supervisor *<+ SupervisorReq.ProjectCodeActionExecute(x,res))
        | ProjectFileLinks x -> job_val (fun res -> supervisor *<+ SupervisorReq.ProjectFileLinks(x,res))
        | ProjectCodeActions x -> job_val (fun res -> supervisor *<+ SupervisorReq.ProjectCodeActions(x,res))
        | FileOpen x -> job_null (supervisor *<+ SupervisorReq.FileOpen x)
        | FileChange x -> job_null (supervisor *<+ SupervisorReq.FileChange x)
        | FileDelete x -> job_null (supervisor *<+ SupervisorReq.FileDelete x)
        | FileTokenRange x -> job_val (fun res -> supervisor *<+ SupervisorReq.FileTokenRange(x,res))
        | HoverAt x -> job_val (fun res -> supervisor *<+ SupervisorReq.HoverAt(x,res))
        | BuildFile x -> job_null (supervisor *<+ SupervisorReq.BuildFile x)
        | Ping _ -> task { return null }
        | Exit _ ->
            async {
                trace Debug (fun () -> "Supervisor.SpiralHub.ClientToServerMsg / exiting...") _locals
                async {
                    do! Async.Sleep 300
                    System.Diagnostics.Process.GetCurrentProcess().Kill ()
                }
                |> Async.Start
                return null
            }
            |> Async.StartAsTask

open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging

let [<EntryPoint>] main args =
    SpiralTrace.TraceLevel.US0_1 |> set_trace_level
    // Scheduler.Global.setCreate { Scheduler.Create.Def with MaxStackSize = 1024 * 8192 |> Some }

    let env = Startup.parse args

    let uri_server = $"http://localhost:{env.port}"

    printfn "Server bound to: %s" uri_server
    trace Debug (fun () -> $"pwd: {Environment.CurrentDirectory}") _locals
    let dllPath = Reflection.Assembly.GetExecutingAssembly().Location |> System.IO.Path.GetDirectoryName
    trace Debug (fun () -> $"dllPath: {dllPath}") _locals
    trace Debug (fun () -> $"targetDir: {targetDir}") _locals
    let builder = WebApplication.CreateBuilder()
    builder.Logging.SetMinimumLevel LogLevel.Warning |> ignore
    builder.Services
        .AddCors()
        .AddSignalR(fun x -> 
            x.MaximumReceiveMessageSize <- 1 <<< 20 // 1mb
            x.EnableDetailedErrors <- true
            ) |> ignore
        
    builder.Services
        .AddSingleton<Supervisor>(fun s ->
            let hub = s.GetService<IHubContext<SpiralHub>>()
            let broadcast x = hub.Clients.All.SendCoreAsync("ServerToClientMsg",[|Json.serialize x|])

            let error_ch_create msg =
                let x = Ch()
                Hopac.server (Job.forever (Ch.take x >>= (msg >> fun (x : ClientErrorsRes) -> Hopac.Job.awaitUnitTask (broadcast x))))
                x

            let errors : SupervisorErrorSources = {
                fatal = error_ch_create FatalError
                package = error_ch_create PackageErrors
                tokenizer = error_ch_create TokenizerErrors
                parser = error_ch_create ParserErrors
                typer = error_ch_create TypeErrors
                traced = error_ch_create TracedError
                }
            let supervisor = Ch()
            let atten = Ch()

            do Hopac.server (attention_server errors atten)
            do Hopac.start (supervisor_server env atten errors supervisor)
            {supervisor_ch=supervisor}
            ) |> ignore
    builder.WebHost.UseUrls [|uri_server|] |> ignore
    builder.Logging.SetMinimumLevel(LogLevel.Warning) |> ignore

    let app = builder.Build()
    app.UseCors(fun x ->
        x.SetIsOriginAllowed(fun _ -> true)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials() |> ignore
        ) |> ignore
    app.MapHub<SpiralHub> "" |> ignore

    use _ = Eval.startTokenRangeWatcher ()
    spiral_compiler.startParentWatcher ()
    use _ = Eval.startCommandsWatcher uri_server

    printfn $"Starting the Spiral Server. It is bound to: {uri_server}"
    app.Run()
    0

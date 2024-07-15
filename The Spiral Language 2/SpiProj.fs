﻿// Everything that deals with Spiral project files themselves goes here
module Spiral.SpiProj
open FParsec
open VSCTypes

open Polyglot.Common


type RawFileHierarchy =
    | Directory of VSCRange * RString * RawFileHierarchy list
    | File of VSCRange * RString * is_top_down : bool * is_include : bool
type ConfigResumableError =
    | DuplicateFiles of VSCRange [] []
    | DuplicateRecordFields of VSCRange [] []
    | MissingNecessaryRecordFields of string [] * VSCRange
type ConfigFatalError =
    | Tabs of VSCRange []
    | ParserError of string * VSCRange
exception ConfigException of ConfigFatalError

let rec spaces_template s = (spaces >>. optional (followedByString "//" >>. skipRestOfLine true >>. spaces_template)) s
let spaces s = spaces_template s

let raise' x = raise (ConfigException x)
let raise_if_not_empty exn l = if Array.isEmpty l = false then raise' (exn l)
let add_to_exception_list' (p: CharStream<ResizeArray<ConfigResumableError>>) = p.State.UserState.Add
let add_to_exception_list (p: CharStream<ResizeArray<ConfigResumableError>>) exn l = if Array.isEmpty l = false then p.State.UserState.Add (exn l)
let column (p : CharStream<_>) = p.Column
let pos (p : CharStream<_>) : VSCPos = {|line=int p.Line - 1; character=int p.Column - 1|}
let pos' p = Reply(pos p)
let range f p = pipe3 pos' f pos' (fun a b c -> ((a, c) : VSCRange), b) p

let is_small_var_char_starting c = isAsciiLower c
let is_var_char c = isAsciiLetter c || c = '_' || c = ''' || isDigit c
let file' p = many1Satisfy2L is_small_var_char_starting is_var_char "lowercase variable name" p
let file p = (range file' .>> spaces) p
let file_verify p = (skipMany1Satisfy2L is_small_var_char_starting is_var_char "lowercase variable name" .>> spaces .>> eof) p

let rec file_hierarchy p =
    let i = column p
    let expr p = if i = column p then file_or_directory p else Reply(ReplyStatus.Error,expected "file or directory on the same or greater indentation as the first one")
    (many expr |>> fun l ->
        let _ = 
            l |> List.toArray
            |> Array.choose (function | File(_,(a,b),_,_) -> Some (b,a) | _ -> None)
            |> Array.groupBy fst
            |> Array.choose (fun (a,b) -> if b.Length > 1 then Some (Array.map snd b) else None)
            |> add_to_exception_list p DuplicateFiles
        l
        ) p

and file_or_directory p =
    let i = column p
    let file_hierarchy p = if i < column p then file_hierarchy p else Reply([])
    (range (range file' >>= fun (r,name) p ->
        let adjust_range ((a,b) : VSCRange) : VSCRange = if b.character < a.character then a,{|line=b.line-1; character=System.Int32.MaxValue|} else a,b
        let x = p.Peek2()
        match x.Char0, x.Char1 with
        | '/',_ -> p.Skip(); (spaces >>. file_hierarchy |>> fun files r' -> Directory(adjust_range r',(r,name),files)) p
        | '-',_ -> p.Skip(); (spaces >>% fun r' -> File(adjust_range r',(r,name),true,true)) p
        | '*','-' -> p.Skip(2); (spaces >>% fun r' -> File(adjust_range r',(r,name),false,true)) p
        | '*',_ -> p.Skip(); (spaces >>% fun r' -> File(adjust_range r',(r,name),false,false)) p
        | _ -> (spaces >>% fun r' -> File(adjust_range r',(r,name),true,false)) p
        )
    |>> fun (r',f) -> f r') p

type RawSchemaPackages = {range : VSCRange; name : string; is_in_compiler_dir : bool; is_include : bool}
let packages p =
    let i = column p
    let file = range (((skipChar '|' >>% true) <|>% false) .>>.  file') >>= fun (r,(is_in_compiler_dir,name)) p ->
        match p.Peek() with
        | '-' -> p.Skip(); (spaces >>% {range=r; name=name; is_in_compiler_dir=is_in_compiler_dir; is_include=true}) p
        | _ -> (spaces >>% {range=r; name=name; is_in_compiler_dir=is_in_compiler_dir; is_include=false}) p
    let file p = if i <= column p then file p else Reply(ReplyStatus.Error,expected "directory on the same or greater indentation as the first one")
    many file p

let tab_positions (str : string): VSCRange [] =
    let mutable line = -1
    Utils.lines str |> Array.choose (fun x -> 
        line <- line + 1
        let x = {|line=line; character=x.IndexOf("\t")|}
        if x.character <> -1 then Some(x,{|x with character=x.character+1|}) else None
        )

let record_reduce (field: Parser<'schema -> 'schema, _>) s p =
    let record_body p =
        let i = column p
        let indent expr p = if i = column p then expr p else Reply(ReplyStatus.Error,expected "record field on the same indentation as the first one")
        many (indent field) p
    (range record_body |>> fun (r,l) -> r, List.fold (|>) s l) p

let record_field (name, p) = 
    (skipString name >>. skipChar ':' >>. spaces >>. range p)
    |>> (fun (r,f) (s,l) -> f s, (r, name) :: l)

let record fields fields_necessary schema =
    let fields = choice (List.map record_field fields)
    record_reduce fields (schema, []) >>= fun (range,(schema,l)) p ->
        let l = List.toArray l
        let _ =
            let names = Array.map snd l
            Set fields_necessary - Set names
            |> Set.toArray
            |> add_to_exception_list p (fun fields -> MissingNecessaryRecordFields(fields,range))
        let _ =
            Array.groupBy snd l
            |> Array.choose (fun (k, v) -> if v.Length > 1 then Some (Array.map fst v) else None)
            |> add_to_exception_list p DuplicateRecordFields

        Reply(schema)

type RawSchema = {
    name : RString option
    version : RString option
    moduleDir : RString option
    modules : RawFileHierarchy list
    packageDir : RString option
    packages : RawSchemaPackages list
    }

let schema_def: RawSchema = {
    name=None
    version=None
    moduleDir=None
    modules=[]
    packageDir=None
    packages=[]
    }

type ConfigError = ResumableError of ConfigResumableError [] | FatalError of ConfigFatalError

open System.IO
let config text =
    try 
        let _ = tab_positions text |> raise_if_not_empty Tabs
        
        let directory p = (range (restOfLine false) .>> spaces |>> fun (r,x) -> Some(r,x.Trim())) p

        let fields = [
            "version", range (restOfLine true .>> spaces) |>> fun (r,x) s -> {s with version=Some (r,x.TrimEnd())}
            "name", file |>> fun x s -> {s with name=Some x}
            "moduleDir", directory |>> fun x s -> {s with moduleDir=x}
            "modules", file_hierarchy |>> fun x s -> {s with modules=x}
            "packageDir", directory |>> fun x s -> {s with packageDir=x}
            "packages", packages |>> fun x s -> {s with packages=x}
            ]
        let necessary = []

        match runParserOnString (spaces >>. record fields necessary schema_def .>> eof) (ResizeArray()) "spiral.config" text with
        | Success(a,userstate,_) -> 
            if userstate.Count > 0 then userstate.ToArray() |> ResumableError |> Result.Error else Result.Ok a
        | Failure(messages,error,_) ->
            let x = {|line=int error.Position.Line - 1; character=int error.Position.Column - 1|}
            ParserError(messages, (x,{|x with character=x.character+1|})) |> FatalError |> Result.Error
    with 
        | :? ConfigException as e -> e.Data0 |> FatalError |> Result.Error

    |> Result.mapError (fun x ->
        let fatal_error = function
            | Tabs l -> l |> Array.map (fun r -> r, "Tab not allowed.")
            | ParserError(x,r) -> [|r, (Utils.lines x).[3..] |> String.concat "\n"|]
        let inline duplicate er = Array.collect (fun l -> let er = er (Array.length l) in Array.map (fun r -> r, er) l)
        let resumable_error = function
            | DuplicateFiles l -> duplicate (sprintf "Duplicate name. Count: %i") l
            | DuplicateRecordFields l -> duplicate (sprintf "Duplicate record field. Count: %i") l
            | MissingNecessaryRecordFields (l,r) -> [|r, sprintf "Record is missing the fields: %s" (String.concat ", " l)|]
        match x with
        | ResumableError x -> Array.collect resumable_error x
        | FatalError x -> fatal_error x
        |> Array.toList
        )

type FileHierarchy =
    | Directory of VSCRange * path: RString * name : string * FileHierarchy list
    | File of VSCRange * path: RString * string option
type SchemaPackages = {dir : RString; name : string option}
type Schema = {
    moduleDir : VSCRange option * string
    modules : FileHierarchy list
    packageDir : VSCRange option * string 
    packages : SchemaPackages list
    }

exception SchemaException of RString
type SchemaResult = Result<Schema,RString list>
let schema (pdir,text) : SchemaResult = config text |> Result.bind (fun x ->
    try
        let combine a (r,b) =
            try
                Path.Combine(a,b)
                |> Path.GetFullPath
                |> fun result ->
                    let result' = result |> Lib.SpiralFileSystem.normalize_path
                    trace Verbose (fun () -> $"""SpiProj.schema.combine / a: {a} / b: {b} / result: {result |> Lib.SpiralSm.replace "\\" "|"} / result': {result'}""") _locals
                    result'
            with e ->
                raise (SchemaException(r,e.Message))
        let module_dir =
            match x.moduleDir with
            | Some(r,_ as x) -> Some r, combine pdir x
            | None -> None, pdir
        let package_dir = 
            match x.packageDir with
            | Some(r,_ as x) -> Some r, combine pdir x
            | None -> None, Path.Combine(pdir,"..") |> Path.GetFullPath
        trace Verbose (fun () -> $"""SpiProj.schema / pdir: {pdir} / module_dir: {module_dir |> snd} / package_dir: {package_dir |> snd |> Lib.SpiralSm.replace "\\" "|"}""") _locals
        let modules =
            let rec loop prefix = function
                | RawFileHierarchy.Directory(r,(r',a),l) -> 
                    let prefix = Path.Combine(prefix,a)
                    let prefix' = prefix |> Lib.SpiralFileSystem.normalize_path
                    trace Verbose (fun () -> $"SpiProj.schema.modules.loop | RawFileHierarchy.Directory(r,(r',a),l) / prefix: {prefix} / prefix': {prefix'}") _locals
                    let prefix = prefix'
                    Directory(r,(r',prefix),a,List.map (loop prefix) l)
                | RawFileHierarchy.File(r,(r',a),is_top_down,is_include) ->
                    let path = Path.Combine(prefix,a + if is_top_down then ".spi" else ".spir")
                    let path' = path |> Lib.SpiralFileSystem.normalize_path
                    trace Verbose (fun () -> $"SpiProj.schema.modules.loop | RawFileHierarchy.File(r,(r',a),is_top_down,is_include) / path: {path} / path': {path'}") _locals
                    let path = path'
                    File(r,(r',path),if is_include then None else Some a)
            List.map (loop (snd module_dir)) x.modules
        let packages =
            let cdir = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory,"..") |> Path.GetFullPath
            x.packages |> List.map (fun x ->
                let name = if x.is_include then None else Some x.name
                let dir = Path.Combine((if x.is_in_compiler_dir then cdir else snd package_dir),x.name)
                let dir' = dir |> Lib.SpiralFileSystem.normalize_path
                trace Verbose (fun () -> $"""SpiProj.schema.packages / dir: {dir |> Lib.SpiralSm.replace "\\" "|"} / dir': {dir'}""") _locals
                let dir = dir'
                {name = name; dir = x.range, dir}
                )
        Result.Ok {moduleDir = module_dir; modules = modules; packageDir = package_dir; packages = packages}
    with :? SchemaException as e -> Result.Error [e.Data0]
    )

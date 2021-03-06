﻿open System
open System.Xml
open System.Xml.Schema
open FParsec

let outputXsd (schema:XmlSchema) =
  let nsmgr = XmlNamespaceManager(NameTable())
  if schema.TargetNamespace <> null then
    nsmgr.AddNamespace("", schema.TargetNamespace)
  nsmgr.AddNamespace("xs", "http://www.w3.org/2001/XMLSchema")
  schema.Write(Console.Out, nsmgr)

[<EntryPoint>]
let main argv = 
  match Console.In.ReadToEnd() |> Xdef.parse with
  | Success (r, s, p) -> 
      Xsd.fromSchema r |> outputXsd
      0
  | Failure (msg, err, s) -> 
      eprintfn "%s" msg
      -1
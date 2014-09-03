﻿module Ast

open FParsec
open FParsec.Applicative

type Type = 
  | FixedString of string | FixedInt of int | FixedFloat of float
  | Bool | String | Int  | Float | Decimal | Guid 
  | DateTime of format : string option | TimeSpan of format : string option
  | RestrictedString of string list
  | IntRange of int * int
  | Pattern of string

// [b, e)
let intRange b e = IntRange(b, e - 1)

// [b, e]
let intRange2 b e = IntRange(b, e)

type XdefOccurrence =
  | Required
  | Many
  | RequiredMany
  | Optional
  | Specified of min : int * max : int

let xdefSpecified min max = Specified (min, max)

type XdefAttribute = {
  Name : string
  Type : Type
  Comment : string option
}

let xdefAttribute nm typ comm = { Name = nm; Type = typ; Comment = comm }

type XdefSimpleElement = {
  Name : string
  Occurrence : XdefOccurrence
  Type : Type
  Comment : string option
}

let xdefSimpleElement nm occurs typ comm = { Name = nm; Occurrence = occurs; Type = typ; Comment = comm }

type XdefOrder =
  | Sequence
  | Choice
  | All

type XdefComplexElement = {
  Name : string
  Occurrence : XdefOccurrence
  Order : XdefOrder
  Nodes : XdefNode list
  Comment : string option
}

and XdefNode = 
  | ComplexElement of XdefComplexElement
  | SimpleElement of XdefSimpleElement
  | Attribute of XdefAttribute
// TODO:  | Module of string

let xdefComplexElement nm occurs order comm nodes = { Name = nm; Occurrence = occurs; Order = order; Nodes = nodes; Comment = comm }


type IndentLevel = int
type UserState = IndentLevel
type Parser<'t> = Parser<'t, UserState>
let indent = updateUserState ((+) 1)
let unindent = updateUserState (fun x -> System.Math.Max(x - 1, 0))

let pXdefName : Parser<_> = regex "\w+"
let pFixedStringChar : Parser<_> = attempt ('"' <! pstring "\\\"") <|> noneOf "\""
let pFixedString : Parser<_> = FixedString <!> pchar '"' *> manyChars pFixedStringChar <* pchar '"' 
let pFixedInt : Parser<_> = FixedInt <!> pint32
let pFixedFloat : Parser<_> = FixedFloat <!> pfloat
let pPrimitiveType f typeName = f <! pstring typeName
let pFormatChar : Parser<_> =  attempt ('>' <! pstring "\\>") <|> noneOf ">"
let pFormatText = manyChars pFormatChar
let pFormat = between (pstring "<") (pstring ">") pFormatText
let pPrimitiveTypeWithFormat f typeName = f <!> pstring typeName *> (opt pFormat)
let pRestrictedString = 
  between (pstring "(") (pstring ")") <|
  ((List.map (function FixedString v -> v | _ -> failwith "internal error") >> RestrictedString) <!> (sepBy1 (spaces *> pFixedString <* spaces) (pchar '|')))

let pIntRange : Parser<_> = 
  between (pstring "[") (pstring ")") <|
  (intRange <!> spaces *> pint32 <* spaces <* pchar ',' <* spaces <*> pint32 <* spaces)
  
let pIntRange2 : Parser<_> = 
  between (pstring "[") (spaces *> pstring "]") <|
  (intRange2 <!> spaces *> pint32 <* spaces <* pchar ',' <* spaces <*> pint32 <* spaces)

let pPatternChar : Parser<_> = attempt ('/' <! pstring "\\/") <|> noneOf "/"
let pPattern : Parser<_> = Pattern <!> pchar '/' *> manyChars pPatternChar <* pchar '/' 

let pType =
  pRestrictedString
  <|> pIntRange |> attempt
  <|> pIntRange2
  <|> pPattern
  <|> pFixedString
  <|> pFixedInt
  <|> pFixedFloat
  <|> pPrimitiveType Bool "Bool"
  <|> pPrimitiveType String "String"
  <|> pPrimitiveType Int "Int" 
  <|> pPrimitiveType Float "Float" 
  <|> pPrimitiveType Decimal "Decimal" 
  <|> pPrimitiveType Guid "Guid" 
  <|> pPrimitiveTypeWithFormat DateTime "DateTime"
  <|> pPrimitiveTypeWithFormat TimeSpan "TimeSpan"

let pTyped = spaces *> pchar ':' *> spaces *> pType

let pOrder =
  (Sequence <! pstring "Sequence") |> attempt
  <|> (Choice <! pstring "Choice") |> attempt
  <|> (All <! pstring "All")

let pOrdered = 
  (spaces *> pstring "::" *> spaces *> pOrder) |> attempt
  <|> (preturn Sequence)

let pOccurrence : Parser<_> =
  (between (pstring "{") (pstring "}") (xdefSpecified <!> spaces *> pint32 <* spaces <* pstring ".." <* spaces <*> pint32 <* spaces)) |> attempt
  <|> (Many <! pstring "*")
  <|> (RequiredMany <! pstring "+")
  <|> (Optional <! pstring "?")
  <|> (preturn Required)

let pIndent = attempt <| parse { 
  let! indentLevel = getUserState
  let indentLevel = (indentLevel) * 2
  do! skipManyMinMaxSatisfy indentLevel indentLevel ((=) ' ')
}

let pCommentChar : Parser<_> = noneOf ['\n'; '\r']
let pComment : Parser<_> = 
  (spaces *> pstring "--" *> spaces *> manyChars pCommentChar <* (skipNewline <|> eof) |> opt) |> attempt
  <|> (None <! (skipNewline <|> eof))

let pXdefAttribute = 
  xdefAttribute <!> pIndent *> pchar '@' *> pXdefName <*> pTyped <*> pComment

let (pNodes, pNodesImpl) = createParserForwardedToRef ()
let (pNode, pNodeImpl) = createParserForwardedToRef ()

let pXdefSimpleElement = 
  xdefSimpleElement <!> pIndent *> pXdefName <*> pOccurrence <*> pTyped <*> pComment

let pXdefComplexElement =
  xdefComplexElement <!> pIndent *> pXdefName <*> pOccurrence <*> pOrdered <*> pComment <*> indent *> pNodes

do pNodesImpl := (many pNode) <* unindent

do pNodeImpl :=
    (Attribute <!> pXdefAttribute) |> attempt
    <|> (SimpleElement <!> pXdefSimpleElement) |> attempt
    <|> (ComplexElement <!> pXdefComplexElement)
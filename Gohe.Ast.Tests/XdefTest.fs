﻿module Test.``Xdef Test``

open NUnit.Framework
open FsUnit
open FParsec

let parse p input = 
  match runParserOnString p 0 "" input with
  | Success (r, s, p) -> Some  r
  | Failure (msg, err, s) -> None

[<Test>]
let ``XdefAttributeをパースできる`` () =  
    parse Ast.pXdefAttribute "@Name : String"
    |> should equal (Some <| Ast.xdefAttribute "Name" Ast.Type.String None)

[<Test>]
let ``XdefSimpleElementをパースできる`` () =  
    parse Ast.pXdefSimpleElement "Name : String" 
    |> should equal (Some <| Ast.xdefSimpleElement "Name" None Ast.Type.String None)

[<Test>]
let ``出現回数付XdefSimpleElementをパースできる`` () =  
    parse Ast.pXdefSimpleElement "Name? : String" 
    |> should equal (Some <| Ast.xdefSimpleElement "Name" (Some Ast.XdefOccurs.Option) Ast.Type.String None)

    parse Ast.pXdefSimpleElement "Name* : String" 
    |> should equal (Some <| Ast.xdefSimpleElement "Name" (Some Ast.XdefOccurs.Many) Ast.Type.String None)

[<Test>]
let ``XdefSequenceElementをパースできる`` () =  
    parse Ast.pXdefSequenceElement "Root"
    |> should equal (Some <| Ast.xdefSequenceElement "Root" None None [])

[<Test>]
let ``子要素持ちのXdefSequenceElementをパースできる`` () =  
    let xdef = "Root\n  @Name : String\n  Description : String"

    let expected = 
      Ast.xdefSequenceElement "Root" None None [
        Ast.Attribute <| Ast.xdefAttribute "Name" Ast.String None 
        Ast.SimpleElement <| Ast.xdefSimpleElement "Description" None Ast.String None
        ]

    parse Ast.pXdefSequenceElement xdef
    |> should equal (Some <| expected)

[<Test>]
let ``複雑なXdefNodeをパースできる`` () =  
    let xdef = """
Root
  @Id : Guid -- ID属性
  Description : String -- 詳細
  Children
    Child* : [0,10)
  Behavior
    OptionA? : "Enabled" """.Trim()

    let expected = 
      Ast.SequenceElement <| Ast.xdefSequenceElement "Root" None None [
        Ast.Attribute <| Ast.xdefAttribute "Id" Ast.Guid (Some "ID属性") 
        Ast.SimpleElement <| Ast.xdefSimpleElement "Description" None Ast.String (Some "詳細")
        Ast.SequenceElement <| Ast.xdefSequenceElement "Children" None None [
            Ast.SimpleElement <| Ast.xdefSimpleElement "Child" (Some Ast.XdefOccurs.Many) (Ast.intRange 0 10) None
          ]
        Ast.SequenceElement <| Ast.xdefSequenceElement "Behavior" None None [
            Ast.SimpleElement <| Ast.xdefSimpleElement "OptionA" (Some Ast.XdefOccurs.Option) (Ast.StringValue "Enabled") None
          ]
        ]

    parse Ast.pNode xdef
    |> should equal (Some <| expected)
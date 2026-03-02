module StressTest

open HtmlTypeProvider
open FSharp.Data

// JSON provider instantiations to simulate storm's FSharp.Data usage
type JsonUser = JsonProvider<"""{"id":1,"name":"John","email":"john@test.com","age":30}""">
type JsonCompany = JsonProvider<"""{"id":"c1","name":"Acme","size":100,"industry":"tech"}""">
type JsonPosition = JsonProvider<"""{"id":"p1","title":"Dev","level":"senior","status":"open"}""">
type JsonCandidate = JsonProvider<"""{"id":"x1","name":"Jane","score":95.5,"tags":["a","b"]}""">
type JsonEval = JsonProvider<"""{"id":"e1","rating":4,"comments":"good","date":"2025-01-01"}""">
type JsonReport = JsonProvider<"""[{"metric":"cpu","value":0.5},{"metric":"mem","value":0.8}]""">
type JsonConfig = JsonProvider<"""{"theme":"dark","lang":"en","features":{"a":true,"b":false}}""">
type JsonEvent = JsonProvider<"""{"type":"click","target":"btn","timestamp":1234567890}""">
type JsonResponse = JsonProvider<"""{"status":200,"data":{"items":[1,2,3],"total":3}}""">
type JsonAuth = JsonProvider<"""{"token":"abc","expires":3600,"scopes":["read","write"]}""">

// 30 template instantiations - each generates a type with nested types (Card, ListItem)
type Page1 = Template<"templates/page1.html">
type Page2 = Template<"templates/page2.html">
type Page3 = Template<"templates/page3.html">
type Page4 = Template<"templates/page4.html">
type Page5 = Template<"templates/page5.html">
type Page6 = Template<"templates/page6.html">
type Page7 = Template<"templates/page7.html">
type Page8 = Template<"templates/page8.html">
type Page9 = Template<"templates/page9.html">
type Page10 = Template<"templates/page10.html">
type Page11 = Template<"templates/page11.html">
type Page12 = Template<"templates/page12.html">
type Page13 = Template<"templates/page13.html">
type Page14 = Template<"templates/page14.html">
type Page15 = Template<"templates/page15.html">
type Page16 = Template<"templates/page16.html">
type Page17 = Template<"templates/page17.html">
type Page18 = Template<"templates/page18.html">
type Page19 = Template<"templates/page19.html">
type Page20 = Template<"templates/page20.html">
type Page21 = Template<"templates/page21.html">
type Page22 = Template<"templates/page22.html">
type Page23 = Template<"templates/page23.html">
type Page24 = Template<"templates/page24.html">
type Page25 = Template<"templates/page25.html">
type Page26 = Template<"templates/page26.html">
type Page27 = Template<"templates/page27.html">
type Page28 = Template<"templates/page28.html">
type Page29 = Template<"templates/page29.html">
type Page30 = Template<"templates/page30.html">

// Dummy types to increase type count in compilation (simulating a real project)
type Model1 = { Id: int; Name: string; Value: float }
type Model2 = { Id: int; Name: string; Value: float }
type Model3 = { Id: int; Name: string; Value: float }
type Model4 = { Id: int; Name: string; Value: float }
type Model5 = { Id: int; Name: string; Value: float }
type Model6 = { Id: int; Name: string; Value: float }
type Model7 = { Id: int; Name: string; Value: float }
type Model8 = { Id: int; Name: string; Value: float }
type Model9 = { Id: int; Name: string; Value: float }
type Model10 = { Id: int; Name: string; Value: float }

type Result1 = Ok1 of Model1 | Err1 of string
type Result2 = Ok2 of Model2 | Err2 of string
type Result3 = Ok3 of Model3 | Err3 of string
type Result4 = Ok4 of Model4 | Err4 of string
type Result5 = Ok5 of Model5 | Err5 of string

// Use templates to force type checking
let render1 () = Page1().Title("Test").Heading("H").Content("C").Elt()
let render2 () = Page2().Title("Test").Heading("H").Content("C").Elt()
let render3 () = Page3().Title("Test").Heading("H").Content("C").Elt()
let render4 () = Page4().Title("Test").Heading("H").Content("C").Elt()
let render5 () = Page5().Title("Test").Heading("H").Content("C").Elt()
let render6 () = Page6().Title("Test").Heading("H").Content("C").Elt()
let render7 () = Page7().Title("Test").Heading("H").Content("C").Elt()
let render8 () = Page8().Title("Test").Heading("H").Content("C").Elt()
let render9 () = Page9().Title("Test").Heading("H").Content("C").Elt()
let render10 () = Page10().Title("Test").Heading("H").Content("C").Elt()
let render11 () = Page11().Title("Test").Heading("H").Content("C").Elt()
let render12 () = Page12().Title("Test").Heading("H").Content("C").Elt()
let render13 () = Page13().Title("Test").Heading("H").Content("C").Elt()
let render14 () = Page14().Title("Test").Heading("H").Content("C").Elt()
let render15 () = Page15().Title("Test").Heading("H").Content("C").Elt()

// Use nested templates too
let card1 () = Page1.Card().CardTitle("T").CardBody("B").Elt()
let card2 () = Page2.Card().CardTitle("T").CardBody("B").Elt()
let card3 () = Page3.Card().CardTitle("T").CardBody("B").Elt()
let item1 () = Page1.ListItem().ItemText("T").Elt()
let item2 () = Page2.ListItem().ItemText("T").Elt()

[<EntryPoint>]
let main _ =
    printfn "Stress test loaded"
    0

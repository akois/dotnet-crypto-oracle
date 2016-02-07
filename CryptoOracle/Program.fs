// Learn more about F# at http://fsharp.net
open System
open System.Threading.Tasks
open System.Net
open Microsoft.FSharp.Control.WebExtensions
open System.Reactive
open System.Reactive.Linq
open System.Reactive.Subjects

//module CryptoOracle
//open FSharp.Control

//let a= Observable.Fr

//myAsyncSubject.

let cypher="f20bdba6ff29eed7b046d1df9fb7000058b1ffb4210a580f748b4ac714c001bd4a61044426fb515dad3f21f18aa577c0bdf302936266926ff37dbf7035d5eeb4"


let bytes2hexstring bytes = 
    bytes 
    |> Array.map (fun (x : byte) -> System.String.Format("{0:X2}", x))
    |> String.concat String.Empty


let hexstring2bytes (s:string) = 
    s
    |> Seq.windowed 2
    |> Seq.mapi (fun i j -> (i,j))
    |> Seq.filter (fun (i,j) -> i % 2=0)
    |> Seq.map (fun (_,j) -> Byte.Parse(new System.String(j),System.Globalization.NumberStyles.AllowHexSpecifier))
    |> Array.ofSeq

  
let bytes2string (bytes: byte[]):String = System.Text.Encoding.ASCII.GetString bytes
    


let fetchUrlAsync url =        
    async {                             
        let req = WebRequest.Create(Uri(url)) 
        use! resp = req.AsyncGetResponse()  // new keyword "use!"  
        use stream = resp.GetResponseStream() 
        use reader = new IO.StreamReader(stream) 
        let html = reader.ReadToEnd() 
        printfn "finished downloading %s" url 
        //html
        }

let fetchUrlAsync2(name, url:string) =
    async { 
        try 
            let uri = new System.Uri(url)
            let webClient = new WebClient()
            let! html = webClient.AsyncDownloadString(uri)
            printfn "Read %d characters for %s" html.Length name
        with
            | ex -> printfn "%s" (ex.Message);
    }

exception NoSuchElement of byte

let fetchUrlOKAsync2 url =        
    async {                             
        printfn "process url %s" url
        let req = WebRequest.Create(Uri(url)) :?> HttpWebRequest

        try
            use! resp = req.AsyncGetResponse()  
            let webResponse=resp :?> HttpWebResponse

            printfn "process url OK"
            //raise (NoSuchElement(symbol))
            //if webResponse.StatusCode=HttpStatusCode.NotFound then return symbol//404 
            return 0uy
        with 
        | :? WebException as webEx when (webEx.Response :? HttpWebResponse) -> 
            let httpWebResponse = webEx.Response :?> HttpWebResponse

            // Return an error message based on the HTTP status code.
            match httpWebResponse.StatusCode with
            | HttpStatusCode.NotFound -> 
                printfn "process url 404"
                return 0uy
            | otherCode -> 
                printfn "process url err"
                //raise (NoSuchElement(symbol))
                return 0uy
        | :? WebException as webEx ->
            printfn "%s" (webEx.Status.ToString())
            //raise (NoSuchElement(0uy))
            return 0uy

        //finally return symbol
        }


let fetchUrlOKAsync symbol url =        
    async {                             
        //printfn "process url %s" url
        let req = WebRequest.Create(Uri(url)) :?> HttpWebRequest

        try
            use! resp = req.AsyncGetResponse()  
            use webResponse=resp :?> HttpWebResponse

            printfn "process url OK"
            raise (NoSuchElement(symbol))
            //if webResponse.StatusCode=HttpStatusCode.NotFound then return symbol//404 
            return 0uy
        with 
        | :? WebException as webEx when (webEx.Response :? HttpWebResponse) -> 
            let httpWebResponse = webEx.Response :?> HttpWebResponse

            // Return an error message based on the HTTP status code.
            match httpWebResponse.StatusCode with
            | HttpStatusCode.NotFound -> 
                //printfn "process url 404"
                return symbol
            | otherCode -> 
                raise (NoSuchElement(symbol))
                return 0uy
        | :? WebException as webEx ->
            printfn "%s" (webEx.Status.ToString())
            raise (NoSuchElement(symbol))
            return 0uy

        //finally return symbol
        }
        

let tryUrl symbol cipher = fetchUrlOKAsync symbol ("http://crypto-class.appspot.com/po?er=" + cipher)

//let test a:int  =
//    let queue = BlockingQueueAgent(a)
//    queue.Get ()


let anyOld (list: Async<'T>[]) :Async<'T>=
    let tcs = new TaskCompletionSource<'T>()

    list |> Array.map (fun wf->Async.Start (async{
                let! res=wf
                tcs.TrySetResult (res) |> ignore
            }))
         |> ignore

    Async.AwaitTask tcs.Task


let any (list: Async<'T>[]) :Async<'T>=
    let tcs = new TaskCompletionSource<'T>()

    list |> Array.map (fun wf->(Async.StartWithContinuations (wf,
                                                                (fun res->tcs.TrySetResult (res) |> ignore),
                                                                (fun _ ->()),
                                                                (fun _ -> ()))))
         |> ignore

    Async.AwaitTask tcs.Task



let processSymbol (list: byte[], IV: byte[], acc: byte[], cipher: byte[], i: int) = 
    list 
    |> Array.map (fun a-> 
        let newIV=Array.init 16 (fun element->IV.[element] ^^^ acc.[element] ^^^ if element=15-i then a^^^(byte)(i+1) else if element > 15-i then (byte)(i+1) else 0uy)
        Array.concat [newIV;cipher] |> bytes2hexstring |> tryUrl a
        )
    |> any 
    

type OracleHit=int*char

let processBlock1 (blockIndex: int, IV: byte[], cipher: byte[])= 
    let myAsyncSubject = new ReplaySubject<OracleHit>();
    //printfn "process block %d" blockIndex
    Async.Start (async {
        //printfn "async process block %d" blockIndex
        let acc=ref (Array.create 16 0uy)
        let i= ref 0
        while !i < 16 do
            let! foundSymbol=processSymbol ([|2uy..128uy|], IV, !acc, cipher, !i)
            //printfn "found symbol %s %d"  (foundSymbol.ToString()) !i
            myAsyncSubject.OnNext ((blockIndex*16+15- !i), (char)foundSymbol)
            acc := Array.init 16 (fun element-> (!acc).[element] ^^^ (if element=15 - !i then foundSymbol else 0uy)) 
            i:=!i+1
    
        //printfn "%s" (bytes2hexstring(!acc))
        myAsyncSubject.OnCompleted()
      //return !acc
    })// |> ignore
    myAsyncSubject.AsObservable()

let processBlock (blockIndex: int, block: byte [][])=processBlock1(blockIndex, block.[0], block.[1])

System.Console.WriteLine("Start")
System.Net.ServicePointManager.DefaultConnectionLimit<-32

let bytearray = 
    hexstring2bytes cypher
    |> (Seq.windowed 16) |> Seq.mapi (fun i j -> (i,j)) |> Seq.filter (fun (i,j) -> i % 16=0) |> Seq.map (fun (_,j)->j)
    |>(Seq.windowed 2)
    |> Seq.mapi (fun i block-> processBlock (i, block))
    |> Observable.Merge

    //|> Async.Parallel
    //|> Async.RunSynchronously
    //|> Array.concat
    //|> bytes2string
    //|> printfn "%s"


//let tcs = new TaskCompletionSource<OracleHit>()
let arr=ref (Array.create 48 32uy)
let obs=bytearray

obs.Subscribe (fun (index, symbol)->
    (!arr).[index]<-(byte)symbol
    (printfn "%s" (bytes2string !arr))) |> ignore





obs
|> Observable.Wait
|> ignore


System.Console.WriteLine("OK")


    //.sliding (16,16).toArray.sliding(2,1).toArray
//

//let async1 = async {
//        do! Async.Sleep (System.Random().Next(1000, 2000))
//        return 1 }
//let async2 = async {
//        do! Async.Sleep (System.Random().Next(1000, 2000))
//        return 2 }


//printfn "%d" <| ([|async1;async2|] |> any |> Async.RunSynchronously)


   
//System.Console.WriteLine(bytes2hexstring (hexstring2bytes cypher))
//System.Console.WriteLine(bytes2string ([|66uy;67uy;63uy|]))



module CryptoOracle

open System
open System.Threading
open System.Threading.Tasks
open System.Net
open Microsoft.FSharp.Control.WebExtensions
open System.Reactive
open System.Reactive.Linq
open System.Reactive.Subjects

#light

type OracleHit=int*char
exception NoSuchElement of byte

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
    

let fetchUrlOKAsync symbol url =        
    async {                             
        let req = WebRequest.Create(Uri(url)) :?> HttpWebRequest

        try
            use! resp = req.AsyncGetResponse()  
            use webResponse=resp :?> HttpWebResponse
            raise (NoSuchElement(symbol))
            return 0uy
        with 
        | :? WebException as webEx when (webEx.Response :? HttpWebResponse) -> 
            let httpWebResponse = webEx.Response :?> HttpWebResponse
            match httpWebResponse.StatusCode with
            | HttpStatusCode.NotFound -> 
                return symbol
            | otherCode -> 
                raise (NoSuchElement(symbol))
                return 0uy
        | :? WebException as webEx ->
            raise (NoSuchElement(symbol))
            return 0uy
        }
        

let tryUrl symbol cipher = fetchUrlOKAsync symbol ("http://crypto-class.appspot.com/po?er=" + cipher)



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
    


let processBlock1 (blockIndex: int, IV: byte[], cipher: byte[])= 
    Observable.Create (fun (observer:IObserver<OracleHit>)->
                            let cts = new CancellationTokenSource()
                            Async.Start (async {
                                                let acc=ref (Array.create 16 0uy)
                                                let i= ref 0
                                                while !i < 16 do 
                                                    let! foundSymbol=processSymbol ([|2uy..128uy|], IV, !acc, cipher, !i)
                                                    observer.OnNext ((blockIndex*16+15- !i), (char)foundSymbol)
                                                    acc := Array.init 16 (fun element-> (!acc).[element] ^^^ (if element=15 - !i then foundSymbol else 0uy)) 
                                                    i:=!i+1

                                                observer.OnCompleted()
                                            },
                                            cts.Token)
                            
                            {new IDisposable with member x.Dispose() = cts.Cancel()})
    

let processBlock (blockIndex: int, block: byte [][])=processBlock1(blockIndex, block.[0], block.[1])


let DecryptMessage cypher = 
    hexstring2bytes cypher
    |> (Seq.windowed 16) |> Seq.mapi (fun i j -> (i,j)) |> Seq.filter (fun (i,j) -> i % 16=0) |> Seq.map (fun (_,j)->j)
    |> Seq.windowed 2
    |> Seq.mapi (fun i block-> processBlock (i, block))
    |> Observable.Merge


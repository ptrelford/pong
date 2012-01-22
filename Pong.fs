﻿#if INTERACTIVE
#else
module Play
#endif

open System
open System.Windows
open System.Windows.Controls
open System.Windows.Input
open System.Windows.Media
open System.Windows.Shapes
#if XNA
open Microsoft.Xna.Framework.Audio
#endif
// [snippet:Pong game]
let width, height = 512,384
let move(shape,x,y) = Canvas.SetLeft(shape,float x); Canvas.SetTop(shape,float y)
let read(shape) = Canvas.GetLeft(shape) |> int, Canvas.GetTop(shape) |> int
let rectangle(x,y,w,h) =
    let shape= Rectangle(Width=float w,Height=float h,Fill=SolidColorBrush Colors.White)
    move(shape,x,y)
    shape
let digits = [
    [0b111; 0b001; 0b111; 0b111; 0b101; 0b111; 0b111; 0b111; 0b111; 0b111]
    [0b101; 0b001; 0b001; 0b001; 0b101; 0b100; 0b100; 0b001; 0b101; 0b101]
    [0b101; 0b001; 0b111; 0b111; 0b111; 0b111; 0b111; 0b001; 0b111; 0b111]
    [0b101; 0b001; 0b100; 0b001; 0b001; 0b001; 0b101; 0b001; 0b101; 0b001]
    [0b111; 0b001; 0b111; 0b111; 0b001; 0b111; 0b111; 0b001; 0b111; 0b001]]
let toDigit n =
    let canvas = Canvas()
    digits |> List.iteri (fun y xs ->
        for x = 0 to 2 do    
            if (xs.[n] &&& (1 <<< (2 - x))) <> 0 then
                rectangle(x*10,y*10,10,10) |> canvas.Children.Add
    )
    canvas
let run rate update =
    let rate = TimeSpan.FromSeconds(rate)
    let lastUpdate = ref DateTime.Now
    let residual = ref (TimeSpan())
    CompositionTarget.Rendering.Subscribe (fun _ -> 
        let now = DateTime.Now
        residual := !residual + (now - !lastUpdate)
        while !residual > rate do
            update(); residual := !residual - rate
        lastUpdate := now
    )

type Keys (control:Control) =
    let mutable keysDown = Set.empty  
    do  control.KeyDown.Add (fun e -> keysDown <- keysDown.Add e.Key)
    do  control.KeyUp.Add (fun e -> keysDown <- keysDown.Remove e.Key)        
    member keys.IsKeyDown key = keysDown.Contains key

type Pad(keys:Keys,up,down,x,y) =
    let shape = rectangle(x,y,10,60)
    let y = ref y
    member pad.Shape = shape
    member pad.Update () =
        if keys.IsKeyDown up then y := !y - 4
        if keys.IsKeyDown down then y := !y + 4
        move(shape,x,!y)

type Ball(blocks:Rectangle list, beep1, beep2, win:Event<_>) =
    let bx, by, bdx, bdy = ref (width/2), ref (height/4), ref 1, ref 1
    let shape = rectangle(!bx,!by,10,10)
    let checkBlocks () =
        for block in blocks do
            let x,y = read block
            let w,h = int block.Width, int block.Height
            if !bx >= x && !bx < x + w && !by >= y && !by < y + h then
                if w > h then bdy := - !bdy else bdx := - !bdx 
                by := !by + !bdy*2; bx := !bx + !bdx*2
                if !bdx < 0 then beep1() else beep2()
    member ball.Shape = shape
    member ball.Reset() = bx := width/2; by := height/2; move(shape,!bx,!by)
    member ball.Update() =
        bx := !bx + !bdx*2; by := !by + !bdy*2                               
        checkBlocks()
        move(shape,!bx,!by)
        if !bx < -10 then win.Trigger(0,1)
        if !bx > width then win.Trigger(1,0)

type GameControl() as control=
    inherit UserControl(Width=float width, Height=float height, IsTabStop=true)
    let canvas = new Canvas(Background = SolidColorBrush Colors.Black)  
    #if XNA
    let loadSound name = 
        let x = Application.GetResourceStream(Uri(name, UriKind.RelativeOrAbsolute))
        x.Stream|> SoundEffect.FromStream
    let playSound (stream:SoundEffect) () = 
        stream.CreateInstance().Play() |> ignore
    let beep1, beep2 = loadSound "pongblipa5.wav", loadSound "pongblipf-5.wav"
    #else
    let uri = Uri("/Pong;component/GameControl.xaml", UriKind.Relative)
    do  Application.LoadComponent(control, uri)
    let loadSound path =
        let sound = MediaElement(AutoPlay=false, Source=Uri(path,UriKind.Relative))    
        canvas.Children.Add sound
        sound
    let playSound (sound:MediaElement) () = sound.Stop(); sound.Play()
    let beep1, beep2 = loadSound "/pongblipa5.mp3", loadSound "/pongblipf-5.mp3"
    #endif 
    let win = Event<_>()
    let keys = Keys(control)     
    let top, bottom = rectangle(0,10,width,10), rectangle(0,height-20,width,10)
    let pad1, pad2 = Pad(keys,Key.Q,Key.A,10,60), Pad(keys,Key.P,Key.L,width-20,120) 
    let ball = Ball([top;bottom;pad1.Shape;pad2.Shape], playSound beep1, playSound beep2, win)
    let (+.) (container:Panel) item = container.Children.Add item; container
    do  base.Content <- canvas+.top+.bottom+.pad1.Shape+.pad2.Shape+.ball.Shape
    let update () = pad1.Update(); pad2.Update(); ball.Update()
    let rec loop (a,b) = async {
        let subscription = run (1.0/50.0) update 
        let! a',b' = win.Publish |> Async.AwaitEvent        
        subscription.Dispose()
        let a, b = a + a', b + b'
        let a', b' = toDigit a, toDigit b
        move(a',width/2-60,height/3); move(b',width/2+20,height/3)
        a' |> canvas.Children.Add; b' |> canvas.Children.Add
        if a < 9 && b < 9 then
            do! Async.Sleep 2500
            a' |> canvas.Children.Remove |> ignore; b'|> canvas.Children.Remove |> ignore
            ball.Reset()
            do! Async.Sleep 2500
            return! loop(a,b) 
        } 
    do  async { 
        do! control.MouseLeftButtonDown |> Async.AwaitEvent |> Async.Ignore
        do! loop (0,0) }|> Async.StartImmediate
// [/snippet]
#if INTERACTIVE
open Microsoft.TryFSharp
App.Dispatch (fun() -> 
    App.Console.ClearCanvas()
    let canvas = App.Console.Canvas
    let control = GameControl()    
    control |> canvas.Children.Add
    App.Console.CanvasPosition <- CanvasPosition.Right
    control.Focus() |> ignore
)
#endif
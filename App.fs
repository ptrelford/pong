namespace Pong

open System.Windows

type App() as app =
    inherit Application()
    do  app.Startup.Add(fun _ -> app.RootVisual <- Play.GameControl())
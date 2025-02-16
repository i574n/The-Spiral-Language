module Hopac.Infixes

let (>>**) x f =
    if x |> Hopac.Promise.Now.isFulfilled
    then x |> Hopac.Promise.Now.get |> f
    else Hopac.Infixes.(>>=*) x f

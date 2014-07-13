﻿namespace DeepBelief

module Utils =

    open Microsoft.FSharp.Quotations
    open MathNet.Numerics.Random
    open MathNet.Numerics.Distributions
    open System
    open System.Threading.Tasks
    open Alea.CUDA.Utilities
    open Common.Analytics

    let LCG_A = 1664525u
    let LCG_C = 1013904223u

    let generateStartState (seed:uint32) =
        let state = Array.zeroCreate 8
        state.[0] <- seed
        for i = 1 to 7 do state.[i] <- LCG_A * state.[i - 1] + LCG_C
        state

    /// Transforms an uint32 random number to a float value 
    /// on the interval [0,1] by dividing by 2^32-1
    let [<ReflectedDefinition>] toFloat32 (x:uint32) = float32(x) * 2.3283064E-10f

    /// Transforms an uint32 random number to a float value 
    /// on the interval [0,1] by dividing by 2^32-1
    let [<ReflectedDefinition>] toFloat64 (x:uint32) = float(x) * 2.328306437080797e-10   

    /// compute the derivative of a function, midpoint rule
    let [<ReflectedDefinition>] derivative eps f = 
        fun x -> ((f (x + eps/2.0f) - f (x - eps/2.0f)) / eps)

    /// Density of normal with mean mu and standard deviation sigma. 
    let inline normpdf (mu:'T) (sigma:'T) : Expr<'T -> 'T> =
        <@ fun x -> exp(-(x - mu)*(x - mu)/(2G*sigma*sigma)) / (sigma*__sqrt2pi()) @>

    let flattenMatrix M = 
        let h = Array2D.length1 M
        let w = Array2D.length2 M
        Array.init (h*w) (fun i -> M.[i / w,i % w])

    let flattenSamples samples =
        samples |> Array.map flattenMatrix
        |> Array.fold (fun acc element -> Array.concat [acc; element]) [| |]

    let rebuildMatrix wFull h w (Vector X) =
        Array2D.init h w (fun i j -> X.[i * wFull + j]) |> Matrix

    let nextMultipleOf n i =
        let r = i % n
        if r = 0 then i else i + n - r

    let padToMultiplesOf n M =
        let h = Array2D.length1 M
        let w = Array2D.length2 M
        let paddedHeight = nextMultipleOf n h
        let paddedWidth = nextMultipleOf n w
        Array2D.init paddedHeight paddedWidth 
            (fun i j -> if i < h && j < w then M.[i, j] else 0.0f)

    let padToMultipleOf n x =
        let size = Array.length x
        let paddedSize = nextMultipleOf n size
        Array.init paddedSize 
            (fun i -> if i < size then x.[i] else 0.0f)

    let topLeftSubmatrix h w (M : float32[,]) =
        Array2D.init h w (fun i j -> M.[i, j])

    let subvector size (x : float32[]) =
        Array.init size (fun i -> x.[i])

    let column j M =
        Array.init (height M) (fun i -> M.[i, j])

    let sigmoidFunction = FloatingPointFunction (fun (Domain x)  -> 1.0f / (1.0f + exp(-x)) |> Range)
    let sigmoidDerivative = FunctionValueForm (fun (Range s) -> s * (1.0f - s) |> Gradient)
    let sigmoidActivation = DifferentiableFunction (sigmoidFunction, sigmoidDerivative)
    let logitFunction x = log x - log (1.0f - x)

    let toList M =
        let h = height M
        let w = width M
        [0..h - 1] |> List.map (fun i -> List.init w (fun j -> M.[i, j]))

    let transpose M =
        let h = width M
        let w = height M
        Array2D.init h w (fun i j -> M.[j, i])

    let toColumns (Matrix M) = 
        let h = height M
        let w = width M
        [|0..w - 1|] |> Array.map (fun j -> Array.init h (fun i -> M.[i, j])) 

    let multiplyVectorByScalar (lambda : float32) v =
        let n = Array.length v
        Array.init n (fun i -> lambda * v.[i])

    let multiplyMatrixByScalar (lambda : float32) M =
        let h = height M
        let w = width M
        Array2D.init h w (fun i j -> lambda * M.[i, j])

    let identityMatrix n =
        Array2D.init n n (fun i j -> if i = j then 1.0f else 0.0f)

    let batchesOf n =
        Seq.ofList >> Seq.mapi (fun i v -> i / n, v) >>
        Seq.groupBy fst >> Seq.map snd >>
        Seq.map (Seq.map snd >> Seq.toList) >> Seq.toList
    
    let nextSingle (rnd : Random) = rnd.NextDouble() |> float32

    let permutation rnd arr =
        arr |> Array.sortBy (fun element -> nextSingle rnd)
    let permute rnd n = permutation rnd [|0..(n-1)|]
    let permuteRows rnd M = 
        let row i = Array.init (width M) (fun j -> M.[i, j])
        permute rnd (height M) |> Array.map (fun i -> row i)

    let proportionOfVisibleUnits v =
        v |> Array.filter (fun u -> u > 0.5f) |> fun arr -> float32 arr.Length / float32 v.Length

    let disposeAll ([<ParamArray>] arr : 'a list array when 'a :> IDisposable) =
        for items in arr do
            for item in items do item.Dispose()

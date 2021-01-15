(*** hide ***)
// This block of code is omitted in the generated HTML documentation. Use 
// it to define helpers that you do not want to show in the documentation.
#I "../../bin/FSharp.Stats/netstandard2.0"
#r "../../packages/formatting/FSharp.Plotly/lib/netstandard2.0/Fsharp.Plotly.dll"
#r "../../packages/FSharpAux/lib/netstandard2.0/FSharpAux.dll"
#r "netstandard"
open FSharp.Plotly

#r "FSharp.Stats.dll"
open FSharp.Stats
open FSharpAux

let axis title= Axis.LinearAxis.init(Title=title,Mirror=StyleParam.Mirror.All,Ticks=StyleParam.TickOptions.Inside,Showline=true,Showgrid=true)
let axisRange title range= Axis.LinearAxis.init(Title=title,Range=StyleParam.Range.MinMax(range),Mirror=StyleParam.Mirror.All,Showgrid=false,Ticks=StyleParam.TickOptions.Inside,Showline=true)

(**
#Clustering


For demonstration of several clustering methods, the classic iris data set is used, which consists of 150 records, each of which contains four measurements and a species identifier.
Since the species identifier occur several times (_Iris-irginica_, _Iris-versicolor_, and _Iris-setosa_), the first step is to generate unique labels:

  - The data is shuffled and an index is appended to the data label, such that each label is unique.
*)

open FSharp.Stats
open FSharp.Plotly

let fromFileWithSep (separator:char) (filePath) =     
    // The function is implemented using a sequence expression
    seq {   let sr = System.IO.File.OpenText(filePath)
            while not sr.EndOfStream do 
                let line = sr.ReadLine() 
                let words = line.Split separator//[|',';' ';'\t'|] 
                yield words }

                
let lables,data =
    fromFileWithSep ',' (__SOURCE_DIRECTORY__ + "/data/irisData.csv")
    |> Seq.skip 1
    |> Seq.map (fun arr -> arr.[4], [| float arr.[0]; float arr.[1]; float arr.[2]; float arr.[3]; |])
    |> Seq.toArray
    |> fun x -> Array.init 3 (fun _ -> x) |> Array.concat
    |> Array.shuffleFisherYates
    |> Array.mapi (fun i (lable,data) -> sprintf "%s_%i" lable i, data)
    |> Array.unzip
   
(*** hide ***)
let colnames = ["Sepal length";"Sepal width";"Petal length";"Petal width"]

let colorscaleValue = 
    StyleParam.Colorscale.Electric //Custom [(0.0,"#3D9970");(1.0,"#001f3f")]
    
let dataChart = 
    Chart.Heatmap(data,ColNames=colnames,RowNames=lables,Colorscale=colorscaleValue,Showscale=true)
    |> Chart.withMarginSize(Left=250.)
    |> Chart.withTitle "raw iris data"
(*** include-value:dataChart ***)


(**
##Iterative Clustering

###k-means clustering

In k-means clustering a cluster number has to be specified prior to clustering the data. K centroids are randomly chosen.
After all data points are assigned to their nearest centroid, the algorithm iteratively approaches a centroid position configuration,
that minimizes the dispersion of every of the k clusters.
For cluster number determination see below (Determining the optimal number of clusters).
*)

open FSharp.Stats.ML
open FSharp.Stats.ML.Unsupervised


// Kmeans clustering

// For random cluster inititalization use randomInitFactory:
let rnd = new System.Random()
let randomInitFactory : IterativeClustering.CentroidsFactory<float []> = 
    IterativeClustering.randomCentroids<float []> rnd

//let cvmaxFactory : IterativeClustering.CentroidsFactory<float []> = 
//    IterativeClustering.intitCVMAX
  
let kmeansResult = 
    IterativeClustering.kmeans <| DistanceMetrics.euclidean <| randomInitFactory 
    <| data <| 4

let clusteredIrisData =
    Array.zip lables data
    |> Array.sortBy (fun (l,dataPoint) -> fst (kmeansResult.Classifier dataPoint)) 
    |> Array.unzip
    |> fun (labels,d) -> 
        Chart.Heatmap(d,ColNames=colnames,RowNames=labels,Colorscale=colorscaleValue,Showscale=true)
        |> Chart.withMarginSize(Left=250.)
        |> Chart.withTitle "clustered iris data (k-means clustering)"

(*** include-value:clusteredIrisData ***)

// To get the best kMeans clustering result in terms of the average squared distance of each point
// to its centroid, perform the clustering b times and minimize the dispersion.
let getBestkMeansClustering data k bootstraps =
    [1..bootstraps]
    |> List.mapi (fun i x -> 
        IterativeClustering.kmeans <| DistanceMetrics.euclidean <| randomInitFactory <| data <| k
        )
    |> List.minBy (fun clusteringResult -> IterativeClustering.DispersionOfClusterResult clusteringResult)

(**
##Density based clustering

###DBSCAN

*)
//four dimensional clustering with sepal length, petal length, sepal width and petal width
let t = DbScan.compute DistanceMetrics.Array.euclideanNaN 5 1.0 data

//extract petal length and petal width
let petLpetW      = data |> Array.map (fun x -> [|x.[2];x.[3]|])

//extract petal width, petal length and sepal length  
let petWpetLsepL = data |> Array.map (fun x -> [|x.[3];x.[2];x.[0]|])

//to create a chart with two dimensional data use the following function
let create2dChart (dfu:array<'a> -> array<'a> -> float) (minPts:int) (eps:float) (input:seq<#seq<'a>>) =  
    if (input |> Seq.head |> Seq.length) <> 2 then failwithf "create2dChart only can handle 2 coordinates"
    
    let result = DbScan.compute dfu minPts eps input
    
    let chartCluster = 
        if result.Clusterlist |> Seq.length > 0 then      
            result.Clusterlist
            |> Seq.mapi (fun i l ->
                l
                |> Seq.map (fun x -> x.[0],x.[1])
                |> Seq.distinct //more efficient visualization; no difference in plot but in point numbers
                |> Chart.Point
                |> Chart.withTraceName (sprintf "Cluster %i" i))
            |> Chart.Combine
        else Chart.Point []

    let chartNoise = 
        if result.Noisepoints |> Seq.length > 0 then 
            result.Noisepoints
            |> Seq.map (fun x -> x.[0],x.[1])  
            |> Seq.distinct //more efficient visualization; no difference in plot but in point numbers
            |> Chart.Point
            |> Chart.withTraceName "Noise"
        else Chart.Point []

    let chartname = 
        let noiseCount    = result.Noisepoints |> Seq.length
        let clusterCount  = result.Clusterlist |> Seq.length
        let clPtsCount    = result.Clusterlist |> Seq.sumBy Seq.length
        sprintf "eps:%.1f minPts:%i pts:%i cluster:%i noisePts:%i" 
            eps minPts (noiseCount + clPtsCount) clusterCount noiseCount 

    [chartNoise;chartCluster]
    |> Chart.Combine
    |> Chart.withTitle chartname
    |> Chart.withX_Axis (axis "Petal width") 
    |> Chart.withY_Axis (axis "Petal length")
    
let clusteredChart = create2dChart DistanceMetrics.Array.euclidean 20 0.5 petLpetW

(*** include-value:clusteredChart ***)

//to create a chart with three dimensional data use the following function
let create3dChart (dfu:array<'a> -> array<'a> -> float) (minPts:int) (eps:float) (input:seq<#seq<'a>>) =   
    if (input |> Seq.head |> Seq.length) <> 3 then failwithf "create3dChart only can handle 3 coordinates"
    
    let result = DbScan.compute dfu minPts eps input
    
    let chartCluster = 
        if result.Clusterlist |> Seq.length > 0 then 
            result.Clusterlist
            |> Seq.mapi (fun i l ->
                l
                |> Seq.map (fun x -> x.[0],x.[1],x.[2])
                |> Seq.distinct //faster visualization; no difference in plot but in point number
                |> fun x -> Chart.Scatter3d (x,StyleParam.Mode.Markers)
                |> Chart.withTraceName (sprintf "Cluster_%i" i))
            |> Chart.Combine
        else  Chart.Scatter3d ([],StyleParam.Mode.Markers)

    let chartNoise =
        if result.Noisepoints |> Seq.length > 0 then 
            result.Noisepoints
            |> Seq.map (fun x -> x.[0],x.[1],x.[2])  
            |> Seq.distinct //faster visualization; no difference in plot but in point number
            |> fun x -> Chart.Scatter3d (x,StyleParam.Mode.Markers)
            |> Chart.withTraceName "Noise"
        else Chart.Scatter3d ([],StyleParam.Mode.Markers)

    let chartname = 
        let noiseCount    = result.Noisepoints |> Seq.length
        let clusterCount  = result.Clusterlist |> Seq.length
        let clPtsCount    = result.Clusterlist |> Seq.sumBy Seq.length
        sprintf "eps:%.1f minPts:%i n:%i Cluster:%i NoisePts:%i" 
            eps minPts (noiseCount + clPtsCount) clusterCount noiseCount 

    [chartNoise;chartCluster]
    |> Chart.Combine
    |> Chart.withTitle chartname
    |> Chart.withX_Axis (axis "Petal width")
    |> Chart.withY_Axis (axis "Petal length")
    |> Chart.withZ_Axis (axis "Sepal length")
        


//for faster computation you can use the squaredEuclidean distance and set your eps to its square
let clusteredChart3D = create3dChart DistanceMetrics.Array.euclideanNaNSquared 20 (0.7**2.) petWpetLsepL 

(*** include-value:clusteredChart3D ***)

(**
##Hierarchical clustering

Hierarchical clustering results in a tree structure, that has a single cluster (node) on its
root and recursevely splits up into clusters of elements that are more similar to each other than to elements
of other clusters. For generating multiple cluster results with different number of clusters, the clustering 
has to performed only once. Subsequently a threshold can be determined which will result in the desired number of clusters.

*)

open FSharp.Stats.ML.Unsupervised.HierarchicalClustering
#time

// calculates the clustering and reports a single root cluster (node), 
// that may recursively contains further nodes
let clusterResultH = 
    HierarchicalClustering.generate DistanceMetrics.euclideanNaNSquared Linker.wardLwLinker data true
    |> fun w -> 1.

let clusterResultH = 
    HierarchicalClustering.generateFloat DistanceMetrics.euclideanNaNSquared Linker.wardLwLinker data
    |> fun w -> 1.


euclideanNaNSquared 11:00 14:00 
// If a desired cluster number is specified, the following function cuts the cluster according
// to the depth, that results in the respective number of clusters (here 3). Only leaves are reported.
let threeClustersH = HierarchicalClustering.cutHClust 3 clusterResultH

(**

Every cluster leaf contains its raw values and an index that 
indicates the position of the respective data point in the raw data.
The index can be retrieved from leaves by HierarchicalClustering.getClusterId.
*)

let inspectThreeClusters =
    threeClustersH
    |> List.map (fun cluster -> 
        cluster
        |> List.map (fun leaf -> 
            lables.[HierarchicalClustering.getClusterId leaf]
            )
        )
    |> fun clusteredLabels -> 
        sprintf "Detailed information for %i clusters is given:" clusteredLabels.Length,clusteredLabels

(*** include-value:inspectThreeClusters ***)


// To recursevely flatten the cluster tree into leaves only, use flattenHClust.
// A leaf list is reported, that does not contain any cluster membership, 
// but is sorted by the clustering result.
let hLeaves = 
    clusterResultH
    |> HierarchicalClustering.flattenHClust

// takes the sorted cluster result and reports a tuple of lable and data value.
let dataSortedByClustering =    
    hLeaves
    |> Seq.choose (fun c -> 
        let lable  = lables.[HierarchicalClustering.getClusterId c]
        let values = HierarchicalClustering.tryGetLeafValue c
        match values with
        | None -> None
        | Some x -> Some (lable,x)
        )


let hierClusteredDataHeatmap = 
    let (hlable,hdata) =
        dataSortedByClustering
        |> Seq.unzip
    Chart.Heatmap(hdata,ColNames=colnames,RowNames=hlable,Colorscale=colorscaleValue,Showscale=true)
    |> Chart.withMarginSize(Left=250.)
    |> Chart.withTitle "Clustered iris data (hierarchical clustering)"

(*** include-value:hierClusteredDataHeatmap ***)

(**

#Determining the optimal number of clusters

##Rule of thumb

The rule of thumb is a very crude cluster number estimation only based on the number of data points.

Reference: 'Review on Determining of Cluster in K-means Clustering'; Kodinariya et al; January 2013

*)

//optimal k for iris data set by using rule-of-thumb
let ruleOfThumb = ClusterNumber.kRuleOfThumb data

(*** include-value:ruleOfThumb ***)

(**


##Elbow criterion

The elbow criterion is a visual method to determine the optimal cluster number. The cluster dispersion is measured as the sum of all average (squared) euclidean distance of each point to its associated centroid.
The point at wich the dispersion drops drastically and further increase in k does not lead to a strong decrease in dispersion is the optimal k.

Reference: 'Review on Determining of Cluster in K-means Clustering'; Kodinariya et al; January 2013

*)
open IterativeClustering
open DistanceMetrics

let kElbow = 10

let iterations = 10 

let dispersionOfK = 
    [|1..kElbow|]
    |> Array.map (fun k -> 
        let (dispersion,std) = 
            [|1..iterations|]
            |> Array.map (fun i -> 
                kmeans euclideanNaNSquared (randomCentroids rnd) data k
                |> DispersionOfClusterResult)
            |> fun dispersions -> 
                Seq.mean dispersions, Seq.stDev dispersions
        k,dispersion,std
        )

let elbowChart = 
    Chart.Line (dispersionOfK |> Array.map (fun (k,dispersion,std) -> k,dispersion))
    |> Chart.withYErrorStyle (dispersionOfK |> Array.map (fun (k,dispersion,std) -> std))
    |> Chart.withX_Axis (axis "k")
    |> Chart.withY_Axis (axis "dispersion")
    |> Chart.withTitle "Iris data set dispersion"


(*** include-value:elbowChart ***)

(**
#AIC

[Reference](https://nlp.stanford.edu/IR-book/html/htmledition/cluster-cardinality-in-k-means-1.html)

The Akaike information criterion (AIC) balances the information gain (with raising k) against parameter necessarity (number of k).
The k that minimizes the AIC is assumed to be the optimal one. 

*)


let aicBootstraps = 10

//optimal k for iris data set by using aic
let (aicK,aicMeans,aicStd) =
    //perform 10 iterations and take the mean and standard deviation of the aic
    let aic = 
        [|1..aicBootstraps|]
        |> Array.map (fun b -> ClusterNumber.calcAIC 10 (kmeans euclideanNaNSquared (randomCentroids rnd) data) 15)
    aic
    |> Array.map (fun iteration -> Array.map snd iteration)
    |> JaggedArray.transpose
    |> Array.mapi (fun i aics -> 
        i+1,Seq.mean aics,Seq.stDev aics)
    |> Array.unzip3

let aicChart = 
    Chart.Line (aicK,aicMeans)
    |> Chart.withX_Axis (axis "k")
    |> Chart.withY_Axis (axis "AIC")
    |> Chart.withYErrorStyle aicStd

(*** include-value:aicChart ***)

(**
##Silhouette coefficient

The silhouette index ranges from -1 to 1, where -1 indicates a missclassified point, and 1 indicates a perfect fit.
It can be calculated for every point by comparing the mean intra cluster distance with the nearest mean inter cluster distance.
The mean of all indices can be visualized, where a maximal value indicates the optimal k.

Reference: 'Review on Determining of Cluster in K-means Clustering'; Kodinariya et al; January 2013

*)


// The following example expects the raw data to be clustered by k means clustering.
// If you already have clustered data use the 'silhouetteIndex' function instead.

let silhouetteData = 
    System.IO.File.ReadAllLines(__SOURCE_DIRECTORY__ + "/data/silhouetteIndexData.txt")
    |> Array.map (fun x -> 
        let tmp = x.Split '\t'
        [|float tmp.[0]; float tmp.[1]|])

let sI = 
    ML.Unsupervised.ClusterNumber.silhouetteIndexKMeans 
        50              // number of bootstraps 
        (kmeans euclideanNaNSquared (randomCentroids rnd) silhouetteData) 
        silhouetteData  // input data
        15              // maximal number of allowed k

let rawDataChart =
    silhouetteData 
    |> Array.map (fun x -> x.[0],x.[1])
    |> Chart.Point

let silhouetteIndicesChart =
    Chart.Line (sI |> Array.map (fun x -> x.ClusterNumber,x.SilhouetteIndex))
    |> Chart.withYErrorStyle (sI |> Array.map (fun x -> x.SilhouetteIndexStDev))

(*** hide ***)

let combinedSilhouette =
    [
    rawDataChart |> Chart.withX_Axis (axis "") |> Chart.withY_Axis (axis "") |> Chart.withTraceName "raw data"
    silhouetteIndicesChart |> Chart.withX_Axis (axis "k") |> Chart.withY_Axis (axis "silhouette index") |> Chart.withTraceName "silhouette"
    ]
    |> Chart.Stack (2,0.1)

(*** include-value:combinedSilhouette ***)

(**

##GapStatistics

Reference: 'Estimating the number of clusters in a data set via the gap statistic'; J. R. Statist. Soc. B (2001); Tibshirani, Walther, and Hastie

Gap statistics allows to determine the optimal cluster number by comparing the cluster dispersion (intra-cluster variation) of a reference dataset to the original data cluster dispersion.
For each k both dispersions are calculated, while for the reference dataset multiple iterations are performed for each k. The difference of the log(dispersionOriginal) and the log(dispersionReference) is called 'gap'.
The maximal gap points to the optimal cluster number. 

Two ways to generate a reference data set are implemented.

 - a uniform coverage within the range of the original data set
  
 - a PCA based point coverage, that considers the density/shape of the original data

*)

(*** hide ***)
let gapStatisticsData = 
    System.IO.File.ReadAllLines(__SOURCE_DIRECTORY__ + "/data/gapStatisticsData.txt")
    |> Array.map (fun x ->
        let tmp = x.Split '\t'
        tmp |> Array.map float)

let gapDataChart = 
    [
    gapStatisticsData|> Array.map (fun x -> x.[0],x.[1]) |> Chart.Point |> Chart.withTraceName "original" |> Chart.withX_Axis (axisRange "" (-4.,10.)) |> Chart.withY_Axis (axisRange "" (-2.5,9.))
    (GapStatistics.PointGenerators.generateUniformPoints rnd gapStatisticsData) |> Array.map (fun x -> x.[0],x.[1]) |> Chart.Point |> Chart.withTraceName "uniform" |> Chart.withX_Axis (axisRange "" (-4.,10.)) |> Chart.withY_Axis (axisRange "" (-2.5,9.))
    (GapStatistics.PointGenerators.generateUniformPointsPCA rnd gapStatisticsData) |> Array.map (fun x -> x.[0],x.[1]) |> Chart.Point |> Chart.withTraceName "uniform PCA" |> Chart.withX_Axis (axisRange "" (-4.,10.)) |> Chart.withY_Axis (axisRange "" (-2.5,9.))
    ]
    |> Chart.Stack 3
    |> Chart.withSize(800.,400.)
(*** include-value:gapDataChart ***)
(**
The log(dispersionReference) should decrease with rising k, but - if clusters are presend in the data - should be greater than the log(dispersionOriginal). 

*)
open GapStatistics

//create gap statistics
let gaps =
    GapStatistics.calculate
        (PointGenerators.generateUniformPointsPCA rnd)      //uniform point distribution
        100// no gain above 500                                //number of bootstraps samples 
        ClusterDispersionMetric.logDispersionKMeansInitRandom //dispersion metric of clustering algorithm
        10                                                     //maximal number of allowed clusters
        gapStatisticsData                                      //float [] [] data of coordinates

//number of clusters        
let k        = gaps |> Array.map (fun x -> x.ClusterIndex)
//log(dispersion) of the original data (with rising k)
let disp     = gaps |> Array.map (fun x -> x.Dispersion)
//log(dispersion) of the reference data (with rising k)
let dispRef = gaps |> Array.map (fun x -> x.ReferenceDispersion)
//log(dispersionRef) - log(dispersionOriginal)
let gap      = gaps |> Array.map (fun x -> x.Gaps)
//standard deviation of reference data set dispersion
let std      = gaps |> Array.map (fun x -> x.RefDispersionStDev)

let gapStatisticsChart =
    let dispersions =
        [
        Chart.Line (k,disp)    |> Chart.withTraceName "disp"
        Chart.Line (k,dispRef)|> Chart.withTraceName "dispRef" |> Chart.withYErrorStyle(std)
        ]
        |> Chart.Combine 
        |> Chart.withX_Axis (axisRange "" (0.,11.)) 
        |> Chart.withY_Axis (axis "log(disp)")
    let gaps = 
        Chart.Line (k,gap)|> Chart.withTraceName "gaps"
        |> Chart.withX_Axis (axisRange "k" (0.,11.)) 
        |> Chart.withY_Axis (axis "gaps")
    [dispersions; gaps]
    |> Chart.Stack 1

(*** include-value:gapStatisticsChart ***)

(**
The maximal gap points to the optimal cluster number with the following condition:

 - kopt = smallest k such that Gap(k)>= Gap(k+1)-sk+1

 - where sk = std * sqrt(1+1/bootstraps)

*)

//calculate s(k) out of std(k) and the number of performed iterations for the refernce data set
let sK   = std |> Array.map (fun sd -> sd * sqrt(1. + 1./500.)) //bootstraps = 500 

let gapChart =
    Chart.Line (k,gap)
    |> Chart.withYErrorStyle(sK)
    |> Chart.withX_Axis (axisRange "k" (0.,11.)) 
    |> Chart.withY_Axis (axisRange "gap" (-0.5,2.)) 
    
(*** include-value:gapChart ***)

//choose kOpt = smallest k such that Gap(k)>= Gap(k+1)-sk+1, where sk = sdk * sqrt(1+1/bootstraps)
let kOpt = 
    Array.init (gap.Length - 2) (fun i -> gap.[i] >= gap.[i+1] - sK.[i+1])
    |> Array.findIndex id
    |> fun x -> sprintf "The optimal cluster number is: %i" (x + 1)
    
(*** include-value:kOpt ***)

namespace KMeansAzure
{
    public class Result
    {
        public int[] Clustering { get; set; }   // Int array member to hold the final clustering results of the algorithm. From this member, statistics (as well as per-cluster means) can be derived. (Index - instance, Value - cluster of that instance)
    }
}

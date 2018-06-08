namespace KMeansAzure
{
    public class Data
    {
        public double[][] Instances { get; set; } // Double Array of arrays member to hold data in it's numeric form. Every 1-dimensional array here represents a vector of data. Better use Array of arrays instead of 2d array ([,]) in this case
        public int Clusters { get; set; }         // Amount of clusters to divide the instances to - K Hyper parameter
    }
}

using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;
using System.IO;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text;
using System;

namespace KMeansAlgorithm
{
    public static class KMeansAlgorithm
    {
        [FunctionName("KMeansAlgorithm")]
        public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)]HttpRequest req, TraceWriter log)
        {
            log.Info("K Means function triggered. ");

            string requestBody = new StreamReader(req.Body).ReadToEnd();    // Read body of the request
            dynamic data = JsonConvert.DeserializeObject(requestBody);      // Parse the data
            string instances = data?.instances;                         // Read data into strings as Nullables
            string clusters = data?.clusters;
            if (instances == null || clusters == null)                  // Could not read the data which means the algorithm cannot operate
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError);
            }
            double[][] dataInstances = JsonUtils.ParseDataFromJson<double[][]>(instances); // Parsing data input to int array of arrays to work with the data vectors
            int amountOfClusters = JsonUtils.ParseDataFromJson<int>(clusters); // Parsing amount of clusters to use
            log.Info("Amount of clusters is: " + amountOfClusters);

            int[] resultsArray = KMeansClustering.ExecuteAlgorithm(dataInstances, amountOfClusters);
            string resultClustering = JsonUtils.ParseDataToJson(resultsArray); // Parse results array back to string
            log.Info("Result is: " + resultClustering);
            HttpResponseMessage algorithmOutput = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
            algorithmOutput.Content = new StringContent(resultClustering, Encoding.UTF8, "application/json"); // Prepare sending result array as HTTP Response

            return algorithmOutput;
        }
    }

    public static class KMeansClustering
    {
        /// <summary>
        /// Main method to execute the K-Means algorithm
        /// </summary>
        /// <param name="i_Data">Data instances</param>
        /// <param name="i_AmountOfClusters">Amount of clusters to divide the data into</param>
        /// <returns></returns>
        public static int[] ExecuteAlgorithm(double[][] i_Data, int i_AmountOfClusters)
        {
            double[][] data = Normalize(i_Data);                                    // Normalize data to bypass different scaling on different features of the instances (data vectors)
            double[][] means = CreateMatrix(i_AmountOfClusters, data[0].Length);    // Initialize means array (used to hold updated cluster means)
            int[] clustersArray = Initialize(data.Length, i_AmountOfClusters);      // Initialize clusters array to have some random values
            bool changedSinceLast = true;                                           // Booleans to know when the algorithm converged or that further calculation will yield an empty (zero) cluster which we want to avoid
            bool calculationSucceeded = true;
            int maxNumberOfIterations = data.Length * 10;                           // Safety net/Sanity check for the algorithm to stop
            int currentIteration = 0;

            while (changedSinceLast && calculationSucceeded && currentIteration < maxNumberOfIterations)    // Basically, if there was still a change in the clusters
            {                                                                                               // and the calculation did not create a zero cluster situation and we still did not reach our limit, continue.
                currentIteration++;
                calculationSucceeded = UpdateMeans(data, ref means, clustersArray);
                changedSinceLast = UpdateClusters(data, means, clustersArray);
            }

            return clustersArray;
        }

        /// <summary>
        /// Checks and updates (if necessary) the clustering of the instances according to their distances from the newly calculated means
        /// </summary>
        /// <param name="i_Data">Data instances</param>
        /// <param name="i_Means">Means of every cluster</param>
        /// <param name="i_Clustering">Current clustering of the instances</param>
        /// <returns>Returns true if there was an update in the clustering of the instances, else false</returns>
        private static bool UpdateClusters(double[][] i_Data, double[][] i_Means, int[] i_Clustering)
        {
            int amountOfClusters = i_Means.Length;
            bool clusterChanged = false;
            int[] updatedClusters = new int[i_Clustering.Length];       // Since the method does not work on the original array, no use of ref keyword here
            Array.Copy(i_Clustering, updatedClusters, i_Clustering.Length);     // Copy original clustering as start point for the new clustering
            double[] distances = new double[amountOfClusters];      // Array to hold the distance between an instance and every cluster to check later if there was a change in the cluster for that instance

            for (int i = 0; i < i_Data.Length; i++)
            {
                for (int j = 0; j < amountOfClusters; j++)
                {
                    distances[j] = CalculateDistance(i_Data[i], i_Means[j]);    // Calculate distances of the i'th instance from every cluster mean point
                }

                int possinleNewCluster = MinDistanceIndex(distances);        // Take the cluster ID of the lowest distance achieved
                if (possinleNewCluster != updatedClusters[i])           // If it's different then current cluster for the i'th instance, update the array and mark that there is a change in clustering (algorithm continues)
                {
                    clusterChanged = true;
                    updatedClusters[i] = possinleNewCluster;
                }
            }

            if (!clusterChanged)
                return false;       // No need to continue with the function's logic if there was no change

            int[] clustersCount = new int[amountOfClusters];
            for (int i = 0; i < updatedClusters.Length; i++)
            {
                int clusterID = updatedClusters[i];    // Count amount of instances in each cluster
                clustersCount[clusterID]++;
            }

            for (int i = 0; i < clustersCount.Length; i++)
            {
                if (clustersCount[i] == 0)      // Check if there is an empty cluster (zero cluster). If there is, terminate algorithm as going further can result in zero division and/or in less than K intended clusters
                {
                    return false;
                }
            }

            Array.Copy(updatedClusters, i_Clustering, updatedClusters.Length);      // Finally, copy the updated clusters array back to the input parameter array (Since it's passed by reference anyways)
            return true;    // Indicates that there was at least one change in the clustering AND that there are no empty clusters
        }

        /// <summary>
        /// Helper method to find the index of the shortest distance inside the distance array
        /// </summary>
        /// <param name="i_Distances">Double array that holds distances of a certain instance from all the clusters</param>
        /// <returns>The index of the nearest cluster according to the calculation of distances</returns>
        private static int MinDistanceIndex(double[] i_Distances)
        {
            int indexOfMin = 0;
            double shortestDistance = i_Distances[0];

            for (int i = 1; i < i_Distances.Length; i++)    // Index 0 already initialized, start checking from index 1
            {
                if (i_Distances[i] < shortestDistance)
                {
                    shortestDistance = i_Distances[i];
                    indexOfMin = i;
                }
            }

            return indexOfMin;
        }

        /// <summary>
        /// Helper method to calculate Euclidean distance between an instance and a cluster
        /// </summary>
        /// <param name="i_FeaturesValues">Array with values for every feature of a certain instance</param>
        /// <param name="i_ClusterMeans">Means of a certain cluster</param>
        /// <returns>Euclidean distance between the given instance and the given cluster</returns>
        private static double CalculateDistance(double[] i_FeaturesValues, double[] i_ClusterMeans)
        {
            double sumSquaredDiffs = 0.0;
            
            for (int i = 0; i < i_FeaturesValues.Length; i++)       // For every feature, calculate the squared difference between it's value and the corresponding mean
            {
                sumSquaredDiffs += Math.Pow((i_FeaturesValues[i] - i_ClusterMeans[i]), 2);
            }

            return Math.Sqrt(sumSquaredDiffs);
        }

        /// <summary>
        /// Calculates the means of every cluster according to its associated instances
        /// </summary>
        /// <param name="i_Data">Data instances</param>
        /// <param name="i_Means">Means array to reset and re-populate with new means (Note that it is passed by ref to explicitly mark that I use the reference to update the original array with new values</param>
        /// <param name="i_Clustering">Current clustering of the instances</param>
        /// <returns>Method calculated means successfuly or not. (Will return false if there are empty clusters)</returns>
        private static bool UpdateMeans(double[][] i_Data, ref double[][] i_Means, int[] i_Clustering)
        {
            int amountOfClusters = i_Means.Length;
            int[] clustersCount = new int[amountOfClusters];

            for (int i = 0; i < clustersCount.Length; i++)
            {
                clustersCount[i] = 0;   // Initialize array for safety
            }

            for (int i = 0; i < i_Clustering.Length; i++)
            {
                int clusterID = i_Clustering[i];    // Count amount of instances in each cluster
                clustersCount[clusterID]++;
            }

            for (int i = 0; i < clustersCount.Length; i++)
            {
                if (clustersCount[i] == 0)      // Check if there is an empty cluster (zero cluster). If there is, terminate algorithm as going further can result in zero division and/or in less than K intended clusters
                {
                    return false;
                }
            }

            for (int i = 0; i < i_Means.Length; i++)
            {
                for (int j = 0; j < i_Means[i].Length; j++)
                {
                    i_Means[i][j] = 0.0;    // Reset the i_Means matrix in preperation for new mean calculation
                }
            }

            for (int i = 0; i < i_Data.Length; i++)
            {
                int clusterID = i_Clustering[i];
                for (int j = 0; j < i_Data[i].Length; j++)
                {
                    i_Means[clusterID][j] += i_Data[i][j];      // Sum values of every feature of the instances to it's corresponding cluster's array (Literally means: Count this instance toward the mean of its cluster)
                }
            }

            for (int i = 0; i < i_Means.Length; i++)
            {
                for (int j = 0; j < i_Means[i].Length; j++)
                {
                    i_Means[i][j] /= clustersCount[i];      // To calculate average from the sums. * No risk of division by zero since we stop the algorithm if there is an empty cluster earlier
                }
            }

            return true;
        }

        /// <summary>
        /// Helper method to allocate a new double matrix with given dimensions
        /// </summary>
        /// <param name="i_Rows">Amount of rows in the matrix</param>
        /// <param name="i_Columns">Amount of columns in the matrix</param>
        /// <returns>Matrix with i_Rows row and i_Column columns</returns>
        private static double[][] CreateMatrix(int i_Rows, int i_Columns)
        {
            double[][] matrix = new double[i_Rows][];       // Allocate matrix according to given dimension

            for (int i = 0; i < matrix.Length; i++)
            {
                matrix[i] = new double[i_Columns];      // Allocate array according to given dimension
                for (int j = 0; j < matrix[i].Length; j++)
                {
                    matrix[i][j] = 0.0;     // Initialize array for safety
                }
            }

            return matrix;
        }

        /// <summary>
        /// Initialize clusters, randomly assigning instances to clusters. (After making sure every cluster has at least 1 instance in it at initialization time)
        /// </summary>
        /// <param name="i_AmountOfInstances">Amount of instances in the data</param>
        /// <param name="i_AmountOfClusters">Amount of clusters to use in the algorithm</param>
        /// <returns>Int array that represents the initial clustering of the instances</returns>
        private static int[] Initialize(int i_AmountOfInstances, int i_AmountOfClusters)
        {
            Random random = new Random();
            int[] clusters = new int[i_AmountOfInstances];
            
            for (int i = 0; i < i_AmountOfClusters; i++)
            {
                clusters[i] = i;    // Initialize first i_AmountOfClusters (K) clusters so every cluster has at least 1 instance inside it
            }

            for (int i = i_AmountOfClusters; i < clusters.Length; i++)
            {
                clusters[i] = random.Next(0, i_AmountOfClusters);   // Randomly distribute the rest of the instances across the clusters
            }

            return clusters;
        }

        /// <summary>
        /// Normalizes the data according to Gaussian normalization method.
        /// </summary>
        /// <param name="i_Data">Data to be normalized</param>
        /// <returns>Normalized data</returns>
        private static double[][] Normalize(double[][] i_Data)
        {
            double[][] normalizedData = new double[i_Data.Length][];
            
            for (int i = 0; i < normalizedData.Length; i++)         // Initialize normalizedData array with the copied arrays (and contained values) from i_Data parameter
            {
                normalizedData[i] = new double[i_Data[i].Length];   // Using i_Data[i] even though the array is squared (All the instances have the same amount of features). Does not really matter
                Array.Copy(i_Data[i], normalizedData[i], i_Data[i].Length);     // Copy the corresponding array - which represents a single instance
            }

            for (int i = 0; i < normalizedData[i].Length; i++)      // Iterates over features, not instances this time (To get every value of this feature from every instance and normalize it accordingly)
            {
                double sumValues = 0.0;
                
                for (int j = 0; j < normalizedData.Length; j++)
                {
                    sumValues += normalizedData[j][i];              // Sum values of feature 'i'
                }
                double featureMean = sumValues / (double) normalizedData.Length;        // Calculate mean

                double sumFeatureVariance = 0.0;
                for (int j = 0; j < normalizedData.Length; j++)
                {
                    sumFeatureVariance += Math.Pow((normalizedData[j][i] - featureMean), 2);       // Calculate for every value it's squared distance from the mean
                }
                double standardDeviation = sumFeatureVariance / (double) normalizedData.Length;     // Standard deviation of i'th feature

                for (int j = 0; j < normalizedData.Length; j++)                                     
                {
                    normalizedData[j][i] = ((normalizedData[j][i] - featureMean) / standardDeviation);      // Lastly, normalize the data
                }
            }

            return normalizedData;
        }
    }

    public static class JsonUtils
    {
        /// <summary>
        /// Helper method to parse JSON text to C# Generic type T (Made it generic so every object can be used, to avoid code duplication and imporve reuseability
        /// </summary>
        /// <typeparam name="T">Type to convert to</typeparam>
        /// <param name="i_JsonString">JSON string to convert from</param>
        /// <returns></returns>
        public static T ParseDataFromJson<T>(string i_JsonString)
        {
            return (JsonConvert.DeserializeObject<T>(i_JsonString));
        }

        /// <summary>
        /// Helper method to parse obejct to JSON text
        /// </summary>
        /// <param name="i_DataToParse">Object to parse</param>
        /// <returns>JSON string</returns>
        public static string ParseDataToJson(object i_DataToParse)
        {
            return (JsonConvert.SerializeObject(i_DataToParse));
        }
    }
}

using System;
using System.Text;
using System.Threading.Tasks;

namespace KMeansAzure
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("------ *** K-Means Algorithm implementation as Azure function by Niv Rosnovsky - 7.6.18 *** ------");

            string clustersAmountString;
            if (args.Length < 2)
            {
                clustersAmountString = WriteAndWait("Please enter the amount of clusters you wish to use in the K-Means algorithm.");
            }
            else
            {
                clustersAmountString = args[1]; // Can be read from command-line arguments too.
            }
            int clustersAmount;
            bool parseSucceded = int.TryParse(clustersAmountString, out clustersAmount);    // Try parsing the amount of clusters.
            while (!parseSucceded || clustersAmount < 2)
            {
                clustersAmountString = WriteAndWait("Invalid amount of clusters entered. Please try again.");
                parseSucceded = int.TryParse(clustersAmountString, out clustersAmount);
            }

            Console.WriteLine("Loading data...");
            KMeans kMeansOperator = new KMeans(clustersAmount);
            bool dataLoaded = kMeansOperator.LoadData((args.Length > 0) ? args[0] : "data.json"); // Get path name from command line arguments. If no arguments specified, use by default current directory with file name "data.json"
            if (!dataLoaded)
            {
                WriteAndWait(string.Format("Data could not be loaded. Please check file integrity and validity and try again.{0}" +
                                            "Press any key to continue...", Environment.NewLine));
                Environment.Exit(1);
            }
            Console.WriteLine("Data loaded successfuly.");

            Console.WriteLine(string.Format("Initiating & Running K-Means algorithm on Azure on given dataset with K: {0}.", clustersAmount));
            bool algorithmSucceed = await kMeansOperator.RunAlgorithm();                           // Run the actual K-Means algorithm located inside an Azure function on the cloud
            if (!algorithmSucceed)
            {
                WriteAndWait("Algorithm Failed - Clustering array returned as null.");
                Environment.Exit(1);
            }

            Console.WriteLine("Finished clustering.");
            Console.WriteLine("Result as an array - Index means instance number, Value means cluster that an instance got.");
            ShowArray(kMeansOperator.Clustering);
            Console.WriteLine(string.Format("{0}Instances divided by clusters:{0}", Environment.NewLine));
            ShowClusteredData(kMeansOperator.Instances, kMeansOperator.Clustering, kMeansOperator.K);
            WriteAndWait("Please press any key to terminate the program...");     // Just to stop the console from closing.
        }

        /// <summary>
        /// Helper method to write text to the console and then wait for input to pause console/get input from user
        /// </summary>
        /// <param name="i_TextToWrite">Text to write to console</param>
        /// <returns></returns>
        private static string WriteAndWait(string i_TextToWrite)
        {
            Console.WriteLine(i_TextToWrite); // Write text to console followed by a new line, then wait for user response.
            return Console.ReadLine();
        }

        /// <summary>
        /// Helper method to write int array to the console
        /// </summary>
        /// <param name="i_ArrayToString">Array to parse</param>
        private static void ShowArray(int[] i_ArrayToString)
        {
            StringBuilder builder = new StringBuilder();

            builder.Append("[ ");
            for (int i = 0; i < i_ArrayToString.Length; i++)
            {
                builder.Append(string.Format("{0}, ", i_ArrayToString[i]));
            }
            builder.Append("]");

            Console.WriteLine(builder.ToString());
        }

        /// <summary>
        /// Helper method to write instances divided to clusters to the console
        /// </summary>
        /// <param name="i_Data">Data instances</param>
        /// <param name="i_Clustering">Final clustering</param>
        /// <param name="i_AmountOfClusters">Amount of clusters</param>
        private static void ShowClusteredData(double[][] i_Data, int[] i_Clustering, int i_AmountOfClusters)
        {
            for (int i = 0; i < i_AmountOfClusters; i++)
            {
                Console.WriteLine("==========================");
                Console.WriteLine(string.Format("--------Cluster #{0}--------", (i + 1)));
                Console.WriteLine("==========================");
                for (int j = 0; j < i_Data.Length; j++)
                {
                    int clusterID = i_Clustering[j];
                    if (clusterID != i)     // Only print instances that match current cluster
                    {
                        continue;
                    }

                    Console.Write(j.ToString().PadLeft(3) + " ");
                    for (int k = 0; k < i_Data[j].Length; k++)      // Iterate over all the features and print their values
                    {
                        if (i_Data[j][k] >= 0.0)
                        {
                            Console.Write(" ");
                        }
                        Console.Write(i_Data[j][k].ToString("F1"));
                    }
                    Console.WriteLine();
                }
                Console.WriteLine("==========================");
            }
        }
    }
}

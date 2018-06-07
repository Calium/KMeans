using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace KMeansAzure
{
    public class KMeans
    {
        private string m_DataInJson;        // String member to hold data read from Json formatted text file.
        private double[][] m_Data;          // Double Array of arrays member to hold data in it's numeric form. Every 1-dimensional array here represents a vector of data. Better use Array of arrays instead of 2d array ([,]) in this case
        private int[] m_ClusteringResult;   // Int array member to hold the final clustering results of the algorithm. From this member, statistics (as well as per-cluster means) can be derived.
        private int m_AmountOfClusters;     // Amount of clusters to divide the instances to - K Hyper parameter
        private string m_AzureFunctionURL = "https://rosnovsky.azurewebsites.net/api/KMeansAlgorithm";   // URL to the Azure function that executes the K-Mean algorithm


        public KMeans(int i_AmountOfClusters)
        {
            m_AmountOfClusters = i_AmountOfClusters;
        }

        // Properties
        public double[][] Data
        {
            get { return m_Data; }
        }

        public int[] Clustering
        {
            get { return m_ClusteringResult; }
        }

        public int K
        {
            get { return m_AmountOfClusters; }
        }

        /// <summary>
        /// This function is responsible for loading the data and reporting back if the operation was successful or an exception was caught.
        /// Using Newtonsoft.Json library
        /// </summary>
        /// <param name="i_PathToRead">Path to a text file containing Json formatted data</param>
        /// <returns>Operation successful - True, Operation Failed - False</returns>
        public bool LoadData(string i_PathToRead)
        {
            bool operationSucceeded = false;
            JsonTextReader jsonReader = null;
            StreamReader streamReader = null;
            bool instancesFound = false;

            try
            {
                streamReader = new StreamReader(i_PathToRead);                      // Try to open the file in specified destination for reading
                string lineFromFile;
                StringBuilder dataBuilder = new StringBuilder();
                while ((lineFromFile = streamReader.ReadLine()) != null)            // Load member with data while inserting amount of clusters from the user's input
                {
                    if (lineFromFile == "}")                                        // Last line of the file, then append the cluster amount before that
                    {
                        dataBuilder.AppendLine(string.Format("\"clusters\": \"{0}\"", m_AmountOfClusters));
                    }

                    dataBuilder.Append(lineFromFile);
                    if (lineFromFile != "{" && lineFromFile != "}" && lineFromFile[lineFromFile.Length - 1] != ',')     // Means we are not at the begining of the file nor at the end, then check if a comma is needed at the end.
                    {
                        dataBuilder.Append(",");
                    }
                    dataBuilder.AppendLine();
                }
                m_DataInJson = dataBuilder.ToString();

                jsonReader = new JsonTextReader(new StringReader(m_DataInJson));  // Try to find instances for parsing
                while (jsonReader.Read())                                         // Read through the json text
                {
                    if (jsonReader.Value != null)
                    {
                        if (jsonReader.Value.ToString() == "instances")           // Found instances inside the file and mark to read the data from next iteration
                        {
                            instancesFound = true;
                        }
                        else if (instancesFound)                                  // If instances found, read them.
                        {
                            m_Data = parseDataFromJson<double[][]>(jsonReader.Value.ToString());   // Parse data from Json format to numeric double C# Array of arrays
                            break;
                        }
                    }
                }
                
                operationSucceeded = true;                                        // If we reached here it means no exception thrown, so operation was successful
            }
            catch (Exception e)
            {
#if DEBUG
                Console.WriteLine("Exception error is: " + e.ToString());
#endif
                operationSucceeded = false;                                       // Operation failed, report back
            }
            finally
            {                                                     
                if (jsonReader != null)                                           // Close any resources that were opened during the process
                {
                    jsonReader.CloseInput = true;
                    jsonReader.Close();
                }

                if (streamReader != null)
                {
                    streamReader.Close();
                }
            }

            if (!instancesFound)                                                  // If for some reason could not find instances inside the file, report back as read operation failed. (Return false)
            {
                Console.WriteLine("Instances could not be found in data file.");
                operationSucceeded = false;
            }
            if (m_AmountOfClusters > m_Data.Length)                               // Having more clusters than instances is not logical and will force empty clusters, thus forbidden
            {
                Console.WriteLine("Amount of clusters (K) exceeds amount of instances available in the data file.");
                operationSucceeded = false;
            }
            return operationSucceeded;
        }

        /// <summary>
        /// Runner method to call the RunAlgorithm method and wait for response
        /// </summary>
        public void Run()
        {
            Task<int[]> clusterArray = RunAlgorithm();  // Initiate Asynchronous process of invoking the Azure function that runs the algorithm
            clusterArray.Wait();                        // Wait for the Task to be completed and have a result value
            m_ClusteringResult = clusterArray.Result;   // Save result value in the designated member
        }

        /// <summary>
        /// Asynchronous Run function to execute the actual algorithm in the cloud.
        /// Invoking the Azure function with the read Json data and await for response from the cloud.
        /// </summary>
        private async Task<int[]> RunAlgorithm()
        {
            HttpClient client = new HttpClient();
            HttpContent bodyContent = new StringContent(m_DataInJson, Encoding.UTF8, "application/json"); // Create body content, JSON formatted
            HttpResponseMessage response = await client.PostAsync(m_AzureFunctionURL, bodyContent);       // Send request to Azure function
            if (!response.IsSuccessStatusCode)  // Check if there was an error parsing the data in the Azure function
            {
                Console.WriteLine("Azure function returned error. Please check data file and try again.");
                Console.WriteLine("Press any key to exit...");
                Console.ReadLine();
                Environment.Exit(1);
            }

            string responseString = await response.Content.ReadAsStringAsync();                           // Wait for Asynchronous response
            return (parseDataFromJson<int[]>(responseString));                                            // Parse and Return result
        }

        /// <summary>
        /// Helper function to parse Json string to Generic data (I made it generic so it will be more reuseable)
        /// Using Newtonsoft.Json library
        /// </summary>
        /// <typeparam name="T">Data Type to parse to.</typeparam>
        /// <param name="i_DataToParse">Json formatted string</param>
        /// <returns>The requested parsed data type</returns>
        private T parseDataFromJson<T>(string i_DataToParse)
        {
            return (JsonConvert.DeserializeObject<T>(i_DataToParse));
        }
    }
}

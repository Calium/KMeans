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
        private Data m_Data;                // Data object member to hold the data and the amount of clusters
        private Result m_Result;            // Result object member to hold the result the algorithm returned
        private string m_AzureFunctionURL = "https://rosnovsky.azurewebsites.net/api/KMeansAlgorithm";   // URL to the Azure function that executes the K-Mean algorithm


        public KMeans(int i_AmountOfClusters)
        {
            m_Data = new Data();
            m_Data.Clusters = i_AmountOfClusters;
        }

        // Properties
        public double[][] Instances
        {
           get { return m_Data.Instances; }
        }

        public int[] Clustering
        {
            get { return m_Result.Clustering; }
        }

        public int K
        {
            get { return m_Data.Clusters; }
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
            StreamReader streamReader = null;

            try
            {
                
                streamReader = new StreamReader(i_PathToRead);                      // Try to open the file in specified destination for reading
                m_DataInJson = streamReader.ReadToEnd();                            // Read JSON file
                m_Data.Instances = JsonUtils.ParseDataFromJson<Data>(m_DataInJson).Instances;   // Deserialize the JSON file and save the data
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
                if (streamReader != null)
                {
                    streamReader.Close();
                }
            }

            if (m_Data.Clusters > m_Data.Instances.Length)                       // Having more clusters than instances is not logical and will force empty clusters, thus forbidden
            {
                Console.WriteLine("Amount of clusters (K) exceeds amount of instances available in the data file.");
                operationSucceeded = false;
            }
            return operationSucceeded;
        }

        /// <summary>
        /// Asynchronous Run function to execute the actual algorithm in the cloud.
        /// Invoking the Azure function with the read Json data and await for response from the cloud.
        /// </summary>
        public async Task<bool> RunAlgorithm()
        {
            HttpClient client = new HttpClient();
            string serializedData = JsonUtils.ParseDataToJson(m_Data);
            HttpContent bodyContent = new StringContent(serializedData, Encoding.UTF8, "application/json"); // Create body content, JSON formatted

            HttpResponseMessage response = await client.PostAsync(m_AzureFunctionURL, bodyContent);       // Send request to Azure function
            if (!response.IsSuccessStatusCode)  // Check if there was an error parsing the data in the Azure function
            {
                Console.WriteLine("Azure function returned error. Please check data file and try again.");
                Console.WriteLine("Press any key to exit...");
                Console.ReadLine();
                Environment.Exit(1);
            }

            string responseString = await response.Content.ReadAsStringAsync();     // Wait for Asynchronous response
            m_Result = JsonUtils.ParseDataFromJson<Result>(responseString);         // Saves result for future implementations if needed
            return m_Result.Clustering != null;                                     // Return whether result has value
        }
    }
}

using System.Net.Http;
using System.Windows;
using System.Net;

namespace DisplayAMap
{
    class NSAPICalls
    {
        private static readonly HttpClient httpClient = new HttpClient();

        public static string GetTrainData()
        {
            // Attempt the API call up to 3 times
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                HttpRequestMessage httpRequest = new HttpRequestMessage(HttpMethod.Get, "https://gateway.apiportal.ns.nl/virtual-train-api/api/vehicle");

                if (!httpRequest.Headers.Any())
                {
                    httpRequest.Headers.Add("Ocp-Apim-Subscription-Key", "c109e8eea0a242e0be8a92566437793c");
                }

                // Send GET request
                HttpResponseMessage response = httpClient.SendAsync(httpRequest).Result;

                // Check if the request was successful (status code 200)
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    // Read and return the response content
                    return response.Content.ReadAsStringAsync().Result;
                }
                else
                {
                    MessageBox.Show($"Error: Failed to get data, retrying in 5 seconds.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    Thread.Sleep(5000); // Wait for 2 seconds (adjust the time as needed)
                }
            }

            // If all attempts fail, display error message and quit the application
            MessageBox.Show($"Error: No data received after 3 attempts. The application will now close.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Application.Current.Shutdown();
            return null;
        }

        public static async Task<string> GetTrackData()
        {
            // Attempt the API call up to 3 times
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                HttpRequestMessage httpRequest = new HttpRequestMessage(HttpMethod.Get, "https://gateway.apiportal.ns.nl//Spoorkaart-API/api/v1/spoorkaart");

                if (!httpRequest.Headers.Any())
                {
                    httpRequest.Headers.Add("Ocp-Apim-Subscription-Key", "c109e8eea0a242e0be8a92566437793c");
                }

                // Send GET request
                HttpResponseMessage response = httpClient.SendAsync(httpRequest).Result;

                // Check if the request was successful (status code 200)
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    // Read and return the response content
                    return await response.Content.ReadAsStringAsync();
                }
                else
                {
                    MessageBox.Show($"Error: Failed to get data, retrying in 5 seconds.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    Thread.Sleep(5000); // Wait for 2 seconds (adjust the time as needed)
                }
            }

            // If all attempts fail, display error message and quit the application
            MessageBox.Show($"Error: No data received after 3 attempts. The application will now close.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Application.Current.Shutdown();
            return null;
        }
    }
}



using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Threading.Tasks;


namespace MealGeniusBackend.Services
{

    public class ImageService
    {
        private readonly HttpClient _httpClient;
        private const string UnsplashAccessKey = "REDACTED";
        private const string PixabayApiKey = "REDACTED";

        public ImageService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<string> GetUnsplashImageAsync(string searchKeyword)
        {
            string url = $"https://api.unsplash.com/search/photos?query={searchKeyword}&client_id={UnsplashAccessKey}";

            var response = await _httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var unsplashResponse = JsonConvert.DeserializeObject<UnsplashResponse>(content);

                return unsplashResponse?.results?.Length > 0 ? unsplashResponse.results[0].urls.regular : null;
            }

            return null;
        }

        public async Task<string> GetPixabayImageAsync(string searchKeyword)
        {
            string url = $"https://pixabay.com/api/?key={PixabayApiKey}&q={searchKeyword}&image_type=photo";

            var response = await _httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var pixabayResponse = JsonConvert.DeserializeObject<PixabayResponse>(content);

                return pixabayResponse?.hits?.Length > 0 ? pixabayResponse.hits[0].webformatURL : null;
            }

            return null;
        }

        private class UnsplashResponse
        {
            public Result[] results { get; set; }
        }

        private class Result
        {
            public Urls urls { get; set; }
        }

        private class Urls
        {
            public string regular { get; set; }
        }

        private class PixabayResponse
        {
            public Hit[] hits { get; set; }
        }

        private class Hit
        {
            public string webformatURL { get; set; }
        }
    }

}

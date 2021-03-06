﻿using MultiSourceTorrentDownloader.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace MultiSourceTorrentDownloader.Services
{
    public class SourceBase
    {
        protected HttpClient _httpClient;
        protected string _baseUrl;
        protected string _searchEndpoint;

        protected async Task<string> UrlGetResponseString(string url)
        {
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync();
        }

        protected string TorrentUrl(string torrentUri)
        {
            return Path.Combine(_baseUrl, torrentUri);
        }

        protected async Task<string> BaseGetTorrentDescriptionAsync(string detailsUri, ITorrentParser parser)
        {
            var fullUrl = Path.Combine(_baseUrl, detailsUri);
            var contents = await UrlGetResponseString(fullUrl);
            return await parser.ParsePageForDescriptionHtmlAsync(contents);
        }

        protected async Task<string> BaseGetTorrentMagnetAsync(string detailsUri, ITorrentParser parser)
        {
            var fullUrl = Path.Combine(_baseUrl, detailsUri);
            var contents = await UrlGetResponseString(fullUrl);
            return await parser.ParsePageForMagnetAsync(contents);
        }
    }
}

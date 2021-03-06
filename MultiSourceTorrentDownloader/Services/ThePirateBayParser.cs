﻿using HtmlAgilityPack;
using MultiSourceTorrentDownloader.Constants;
using MultiSourceTorrentDownloader.Data;
using MultiSourceTorrentDownloader.Enums;
using MultiSourceTorrentDownloader.Interfaces;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace MultiSourceTorrentDownloader.Services
{
    public class ThePirateBayParser : ParserBase, IThePirateBayParser
    {
        private readonly ILogService _logger;

        private readonly string _dateStringToReplace;
        private readonly string _sizeStringToReplace;
        private readonly string _uploaderStringToReplace;

        private readonly string[] _formats = new string[]
        {
            "'Today' HH:mm",
            "MM-dd HH:mm"
        };

        public ThePirateBayParser(ILogService logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _dateStringToReplace = "Uploaded";
            _sizeStringToReplace = "Size";
            _uploaderStringToReplace = "ULed by";
        }

        public async Task<TorrentQueryResult> ParsePageForTorrentEntriesAsync(string pageContents)
        {
            return await Task.Run(() =>
            {
                try
                {
                    _logger.Information("ThePirateBay parsing");
                    var htmlDocument = LoadedHtmlDocument(pageContents);

                    var tableRows = htmlDocument.DocumentNode.SelectNodes("//table[@id='searchResult']/tr");//gets table rows that contain torrent data
                    if (NoTableEntries(tableRows))//probably end of results
                        return new TorrentQueryResult { IsLastPage = true };

                    var result = new List<TorrentEntry>();
                    foreach (var dataRow in tableRows)
                    {
                        var dataColumns = dataRow.SelectNodes("td[position()>1]");//skips first column because it does not contain useful info
                        if (dataColumns == null || dataColumns.Count != 3)
                        {
                            _logger.Warning($"Could not find all columns for torrent {Environment.NewLine} {dataColumns[ThePirateBayTorrentIndexer.TitleNode].OuterHtml}");
                            continue;
                        }

                        var titleNode = dataColumns[ThePirateBayTorrentIndexer.TitleNode].SelectSingleNode("div[@class='detName']/a[@class='detLink']");
                        if (titleNode == null)
                        {
                            _logger.Warning($"Could not find title node for torrent {Environment.NewLine} {dataColumns[ThePirateBayTorrentIndexer.TitleNode].OuterHtml}");
                            continue;
                        }

                        var title = titleNode.InnerText;
                        if (string.IsNullOrEmpty(title))//empty title entry makes no sense. log and skip
                        {
                            _logger.Warning($"Empty title from {Environment.NewLine}{titleNode.OuterHtml}");
                            continue;
                        }
                        var torrentUri = titleNode.Attributes?.FirstOrDefault(a => a.Name == "href")?.Value;//this field is not vital, null is acceptable

                        var magnetLink = string.Empty;
                        try
                        {
                            magnetLink = dataColumns[ThePirateBayTorrentIndexer.TitleNode].SelectNodes("a")
                                                       .First(a => a.Attributes.Any(atr => atr.Name == "href" && atr.Value.Contains("magnet")))
                                                       .Attributes.First(atr => atr.Name == "href")
                                                       .Value;
                        }
                        catch (Exception ex)
                        {
                            _logger.Warning($"Could not find magnet link for {Environment.NewLine}{dataColumns[ThePirateBayTorrentIndexer.TitleNode].OuterHtml}", ex);
                            continue;//no point in showing non-downloadable entry
                        }

                        var detailsNode = dataColumns[ThePirateBayTorrentIndexer.TitleNode].SelectSingleNode("font[@class='detDesc']");
                        if (detailsNode == null)
                        {
                            _logger.Warning($"Could not find details node for {Environment.NewLine}{dataColumns[ThePirateBayTorrentIndexer.TitleNode].OuterHtml}");
                            continue;
                        }

                        var details = (detailsNode.InnerText + detailsNode.SelectSingleNode("a")?.InnerText).Replace("&nbsp;", " ").Split(',');//date, size, uploader
                        var date = string.Empty;
                        var size = string.Empty;
                        var uploader = string.Empty;

                        if (details.Length == 3)
                        {
                            date = PrunedDetail(details[ThePirateBayTorrentIndexer.DateIndex], _dateStringToReplace);
                            size = PrunedDetail(details[ThePirateBayTorrentIndexer.SizeIndex], _sizeStringToReplace);
                            uploader = PrunedDetail(details[ThePirateBayTorrentIndexer.UploaderIndex], _uploaderStringToReplace);
                        }

                        if (!int.TryParse(dataColumns[ThePirateBayTorrentIndexer.Seeders].InnerText, out var seeders))
                            _logger.Warning($"Could not parse seeders {Environment.NewLine}{dataColumns[ThePirateBayTorrentIndexer.Seeders].OuterHtml}");

                        if (!int.TryParse(dataColumns[ThePirateBayTorrentIndexer.Leechers].InnerText, out var leechers))
                            _logger.Warning($"Could not parse leechers {Environment.NewLine}{dataColumns[ThePirateBayTorrentIndexer.Leechers].OuterHtml}");

                        var splitSize = size.Split(' ');
                        result.Add(new TorrentEntry
                        {
                            Title = title,
                            TorrentUri = TrimUriStart(torrentUri),
                            TorrentMagnet = magnetLink,
                            Date = ParseDate(date, _formats),
                            Size = new SizeEntity
                            {
                                Value = splitSize[1] != "B" ? double.Parse(splitSize[0], CultureInfo.InvariantCulture) : double.Parse(splitSize[0], CultureInfo.InvariantCulture) / 1024,
                                Postfix = ParseSizePostfix(splitSize[1])
                            },
                            Uploader = uploader,
                            Seeders = seeders,
                            Leechers = leechers,
                            Source = TorrentSource.ThePirateBay
                        });
                    }

                    var pagination = htmlDocument.DocumentNode.SelectNodes("//img[@alt='Next']");
                    return new TorrentQueryResult
                    {
                        TorrentEntries = result,
                        IsLastPage = pagination == null
                    };
                }
                catch(Exception ex)
                {
                    _logger.Warning("Pirate bay parse exception", ex);
                    throw;
                }
                
            });
        }

        private string PrunedDetail(string source, string toRemove) => source.Replace(toRemove, "").Trim();

        private bool NoTableEntries(HtmlNodeCollection tableRows) => tableRows == null;

        protected override DateTime ParseDate(string date, string[] formats)
        {
            var yesterdayFormat = "'Y-day' HH:mm";
            if (DateTime.TryParseExact(date, yesterdayFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
                return parsedDate.AddDays(-1);

            if (!DateTime.TryParse(date, out parsedDate))
                DateTime.TryParseExact(date, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsedDate);

            return parsedDate;
        }

        public async Task<string> ParsePageForMagnetAsync(string pageContents)//magnet is parsed from first page
        {
            throw new NotImplementedException();
        }

        public async Task<string> ParsePageForDescriptionHtmlAsync(string pageContents)
        {
            return await Task.Run(() =>
            {
                var htmlDocument = LoadedHtmlDocument(pageContents);

                var detailsNode = htmlDocument.DocumentNode.SelectSingleNode("//pre");
                if (detailsNode == null)
                {
                    _logger.Warning($"Could not find details node for thepiratebay");
                    return string.Empty;
                }

                return detailsNode.OuterHtml;
            });
           
        }

        protected override string ParseSizePostfix(string postfix)
        {
            if (postfix == "B") return SizePostfix.KiloBytes;
            if (postfix == "KiB") return SizePostfix.KiloBytes;
            if (postfix == "MiB") return SizePostfix.MegaBytes;
            if (postfix == "GiB") return SizePostfix.GigaBytes;

            return SizePostfix.Undefined;
        }
    }
}

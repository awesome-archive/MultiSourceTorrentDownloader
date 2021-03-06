﻿using MaterialDesignThemes.Wpf;
using MultiSourceTorrentDownloader.Common;
using MultiSourceTorrentDownloader.Data;
using MultiSourceTorrentDownloader.Enums;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MultiSourceTorrentDownloader.Models
{
    public partial class MainModel : ModelBase
    {
        public MainModel()
        {
            TorrentEntries = new ObservableCollection<TorrentEntry>();
            AvailableSortOrders = new List<KeyValuePair<Sorting, string>>();
            AvailableSources = new ObservableCollection<DisplaySource>();
            SelectablePages = new ObservableCollection<int>();
        }

        private bool _isLoading;
        private string _searchValue;
        private string _torrentFilter;

        public ObservableCollection<DisplaySource> AvailableSources { get; set; }
        private int _pagesToLoadBySearch;
        private ObservableCollection<int> _selectablePages;

        public ObservableCollection<TorrentEntry> TorrentEntries { get; set; }

        private IEnumerable<KeyValuePair<Sorting, string>> _availableSortOrders;
        private KeyValuePair<Sorting, string> _selectedSearchSortOrder;
        private bool _saveSearchSortOrder;

        private TorrentEntry _selectedTorrent;
        private TorrentCategory _selectedTorrentCategory;

        public Command LoadMoreCommand { get; set; }
        public Command SearchCommand { get; set; }
        public Command OpenTorrentInfoCommand { get; set; }
        public Command DownloadMagnetCommand { get; set; }
        public Command CopyTorrentLinkCommand { get; set; }
    }
}

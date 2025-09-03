using Avalonia.Controls;
using Avalonia.Interactivity;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Linq;
using YoutubeExplode;

namespace SharpMusicPlayer
{
    public partial class MainWindow : Window
    {
        private Dictionary<string, string> playlists = new();
        private List<(string Title, string Url)> tracks = new();
        private string selected_playlist = "";
        private Process? player;

        private YoutubeClient youtube = new YoutubeClient();

        public MainWindow()
        {
            InitializeComponent();
            load_playlists();
            combo_playlists.ItemsSource = playlists.Keys.ToList();
            combo_playlists.SelectionChanged += combo_playlists_SelectionChanged;
            entry_filter.KeyUp += entry_filter_key_up;
            list_tracks.DoubleTapped += list_tracks_double_tapped;
        }

        private void load_playlists()
        {
            string exe_dir = AppContext.BaseDirectory;

            void load_json(string file, string prefix)
            {
                string path = Path.Combine(exe_dir, file);
                if (File.Exists(path))
                {
                    var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path));
                    if (dict != null)
                    {
                        foreach (var kv in dict)
                            playlists[$"{prefix}{kv.Key}"] = kv.Value;
                    }
                }
            }

            load_json("playlists_yt.json", "YT - ");
            load_json("playlists_local.json", "LOCAL - ");

            playlists["SEARCH YT"] = "SEARCH";
        }

        private async void combo_playlists_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (combo_playlists.SelectedItem is string name)
            {
                selected_playlist = name;
                await load_playlist_async(playlists[name]);
            }
        }

        private async System.Threading.Tasks.Task load_playlist_async(string url)
        {
            tracks.Clear();
            list_tracks.ItemsSource = null;

            if (url == "SEARCH")
                return;

            // Local directory
            if (Directory.Exists(url))
            {
                foreach (var file in Directory.GetFiles(url))
                {
                    string ext = Path.GetExtension(file).ToLower();
                    if (ext is ".mp3" or ".flac" or ".wav" or ".ogg" or ".m4a")
                        tracks.Add((Path.GetFileName(file), file));
                }
                tracks.Sort();
            }
            else if (url.Contains("youtube.com") || url.Contains("youtu.be"))
            {
                try
                {
                    // YouTube playlist
                    var playlist = await youtube.Playlists.GetAsync(url);
                    await foreach (var video in youtube.Playlists.GetVideosAsync(playlist.Id))
                    {
                        tracks.Add((video.Title, $"https://www.youtube.com/watch?v={video.Id}"));
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to load YouTube playlist: {ex.Message}");
                }
            }

            update_track_list();
        }

        private void update_track_list()
        {
            var items = new List<string>();
            for (int i = 0; i < tracks.Count; i++)
                items.Add($"{i + 1}. {tracks[i].Title}");
            list_tracks.ItemsSource = items;
        }

        private async void search_yt(string term)
        {
            tracks.Clear();

            var results = youtube.Search.GetVideosAsync(term);

            await foreach (var video in results)
            {
                tracks.Add((video.Title, $"https://www.youtube.com/watch?v={video.Id}"));
            }

            update_track_list();
        }

        private void entry_filter_key_up(object? sender, Avalonia.Input.KeyEventArgs e)
        {
            if (e.Key == Avalonia.Input.Key.Enter && selected_playlist == "SEARCH YT")
            {
                search_yt(entry_filter.Text ?? "");
            }
            else
            {
                if (selected_playlist != "SEARCH YT")
                {
                    string keyword = (entry_filter.Text ?? "").ToLower();
                    var items = new List<string>();
                    for (int i = 0; i < tracks.Count; i++)
                        if (tracks[i].Title.ToLower().Contains(keyword))
                            items.Add($"{i + 1}. {tracks[i].Title}");
                    list_tracks.ItemsSource = items;
                }
            }
        }

        private void list_tracks_double_tapped(object? sender, RoutedEventArgs e)
        {
            if (list_tracks.SelectedItem is string selectedText)
            {
                // Extract the number at the start of the string (before the first dot)
                int dotIndex = selectedText.IndexOf('.');
                if (dotIndex > 0 && int.TryParse(selectedText.Substring(0, dotIndex), out int trackNumber))
                {
                    int trackIndex = trackNumber - 1; // convert to 0-based index
                    if (trackIndex >= 0 && trackIndex < tracks.Count)
                    {
                        var track = tracks[trackIndex];
                        play_track(track.Url, track.Title);
                    }
                }
            }
        }

        private void play_track(string url, string title)
        {
            label_track.Text = title;

            if (player != null && !player.HasExited)
                player.Kill();

            var psi = new ProcessStartInfo
            {
                FileName = "mpv",
                Arguments = $"--ytdl=yes --osc=yes --force-window=yes --loop=inf \"{url}\"",
                UseShellExecute = false
            };

            player = Process.Start(psi);
        }

        protected override void OnClosed(EventArgs e)
        {
            if (player != null && !player.HasExited)
                player.Kill();
            base.OnClosed(e);
        }
    }
}

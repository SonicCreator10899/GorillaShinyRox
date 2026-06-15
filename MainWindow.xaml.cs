
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace GorillaShinyRox;

public partial class MainWindow : Window
{
    private readonly TrackerSettings settings;
    private readonly DispatcherTimer timer;
    private readonly Forms.NotifyIcon trayIcon;
    private readonly List<CosmeticSearchCandidate> cosmeticCandidates = new();
    private CosmeticSearchCandidate? selectedCosmetic;
    private readonly List<CatalogCosmetic> catalogCosmetics = new();
    private CatalogCosmetic? selectedCatalogCosmetic;
    private const string CreatorApplyUrl = "https://www.anotheraxiom.com/aa-creator";

    public MainWindow()
    {
        InitializeComponent();

        settings = TrackerSettings.Load();

        int addedOnOpen = ApplyRewardsIfExpired();
        settings.Save();

        LoadSettingsIntoInputs();
        UpdateStaticScreen();
        UpdateLiveScreen();

        trayIcon = new Forms.NotifyIcon
        {
            Text = "Gorilla Shiny Rox",
            Icon = SystemIcons.Information,
            Visible = true
        };

        Forms.ContextMenuStrip menu = new();

        Forms.ToolStripMenuItem openItem = new("Open Gorilla Shiny Rox");
        openItem.Click += (_, _) => ShowApp();

        Forms.ToolStripMenuItem tipsItem = new("Tips");
        tipsItem.Click += (_, _) => ShowTips();

        Forms.ToolStripMenuItem exitItem = new("Exit");
        exitItem.Click += (_, _) => ExitApp();

        menu.Items.Add(openItem);
        menu.Items.Add(tipsItem);
        menu.Items.Add(exitItem);

        trayIcon.ContextMenuStrip = menu;
        trayIcon.DoubleClick += (_, _) => ShowApp();

        if (addedOnOpen > 0)
        {
            ShowSmallNotification("Shiny Rox added", $"+{addedOnOpen:N0} Shiny Rox added.");
        }

        if (!settings.HasSeenTips)
        {
            Loaded += (_, _) => ShowTips();
        }

        timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };

        timer.Tick += (_, _) =>
        {
            int added = ApplyRewardsIfExpired();

            if (added > 0)
            {
                settings.Save();
                LoadSettingsIntoInputs();
                ShowSmallNotification("Shiny Rox added", $"+{added:N0} Shiny Rox added.");
                UpdateStaticScreen();
            }

            UpdateLiveScreen();
        };

        timer.Start();
    }

    protected override void OnClosed(EventArgs e)
    {
        trayIcon.Visible = false;
        trayIcon.Dispose();
        base.OnClosed(e);
    }

    private void ShowApp()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void ExitApp()
    {
        trayIcon.Visible = false;
        trayIcon.Dispose();
        System.Windows.Application.Current.Shutdown();
    }

    private void TipsButton_Click(object sender, RoutedEventArgs e)
    {
        ShowTips();
    }

    private void ShowTips()
    {
        TipsOverlay.Visibility = Visibility.Visible;
    }

    private void CloseTipsButton_Click(object sender, RoutedEventArgs e)
    {
        TipsOverlay.Visibility = Visibility.Collapsed;
        settings.HasSeenTips = true;
        settings.Save();
    }

    private int ApplyRewardsIfExpired()
    {
        if (!settings.IsTimerSet)
        {
            return 0;
        }

        int added = 0;
        int rewardAmount = Math.Max(1, settings.RoxPerReward);
        int cycleHours = Math.Max(1, settings.RewardCycleHours);

        while (DateTime.UtcNow >= settings.NextRewardUtc && settings.CurrentRox < settings.GoalRox)
        {
            settings.CurrentRox = Math.Min(settings.GoalRox, settings.CurrentRox + rewardAmount);
            settings.NextRewardUtc = settings.NextRewardUtc.AddHours(cycleHours);
            added += rewardAmount;
        }

        return added;
    }

    private void LoadSettingsIntoInputs()
    {
        CurrentRoxBox.Text = settings.CurrentRox.ToString();
        GoalRoxBox.Text = settings.GoalRox.ToString();
        RoxPerRewardBox.Text = settings.RoxPerReward.ToString();
        RewardCycleHoursBox.Text = settings.RewardCycleHours.ToString();

        if (!settings.IsTimerSet)
        {
            NextRewardHoursBox.Text = "0";
            NextRewardMinutesBox.Text = "0";
        }
        else
        {
            TimeSpan next = GetTimeUntilNextReward();
            NextRewardHoursBox.Text = ((int)Math.Floor(next.TotalHours)).ToString();
            NextRewardMinutesBox.Text = next.Minutes.ToString();
        }
    }

    private void SaveSettingsFromInputs(bool updateTimerFromInputs)
    {
        settings.CurrentRox = ReadInt(CurrentRoxBox.Text, settings.CurrentRox, min: 0);
        settings.GoalRox = ReadInt(GoalRoxBox.Text, settings.GoalRox, min: 1);
        settings.RoxPerReward = ReadInt(RoxPerRewardBox.Text, settings.RoxPerReward, min: 1);
        settings.RewardCycleHours = ReadInt(RewardCycleHoursBox.Text, settings.RewardCycleHours, min: 1);

        if (updateTimerFromInputs)
        {
            int hours = ReadInt(NextRewardHoursBox.Text, 0, min: 0);
            int minutes = ReadInt(NextRewardMinutesBox.Text, 0, min: 0);
            if (minutes > 59) minutes = 59;
            settings.NextRewardUtc = DateTime.UtcNow.AddHours(hours).AddMinutes(minutes);
            settings.IsTimerSet = hours > 0 || minutes > 0;
        }

        settings.Save();
    }

    private static int ReadInt(string text, int fallback, int min)
    {
        if (!int.TryParse(text.Trim().Replace(",", ""), out int value))
        {
            return fallback;
        }

        return Math.Max(min, value);
    }

    private void PresetDailyButton_Click(object sender, RoutedEventArgs e)
    {
        RewardCycleHoursBox.Text = "24";
        NextRewardHoursBox.Text = "24";
        NextRewardMinutesBox.Text = "0";
    }

    private void Preset12Button_Click(object sender, RoutedEventArgs e)
    {
        RewardCycleHoursBox.Text = "12";
        NextRewardHoursBox.Text = "12";
        NextRewardMinutesBox.Text = "0";
    }

    private void Preset100Button_Click(object sender, RoutedEventArgs e)
    {
        RewardCycleHoursBox.Text = "100";
        NextRewardHoursBox.Text = "100";
        NextRewardMinutesBox.Text = "0";
    }

    private void UseRepeatButton_Click(object sender, RoutedEventArgs e)
    {
        int repeatHours = ReadInt(RewardCycleHoursBox.Text, 24, min: 1);
        NextRewardHoursBox.Text = repeatHours.ToString();
        NextRewardMinutesBox.Text = "0";
    }

    private void SetTimerButton_Click(object sender, RoutedEventArgs e)
    {
        SaveSettingsFromInputs(updateTimerFromInputs: true);
        LoadSettingsIntoInputs();
        UpdateStaticScreen();
        UpdateLiveScreen();
        ShowSmallNotification("Timer set", $"Next Shiny Rox in {FormatLiveTimer()}.");
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        SaveSettingsFromInputs(updateTimerFromInputs: false);
        UpdateStaticScreen();
        UpdateLiveScreen();
        ShowSmallNotification("Saved", GetNotificationText());
    }

    private void AddRewardButton_Click(object sender, RoutedEventArgs e)
    {
        SaveSettingsFromInputs(updateTimerFromInputs: false);

        settings.CurrentRox = Math.Min(settings.GoalRox, settings.CurrentRox + settings.RoxPerReward);
        settings.NextRewardUtc = DateTime.UtcNow.AddHours(settings.RewardCycleHours);
        settings.IsTimerSet = true;
        settings.Save();

        LoadSettingsIntoInputs();
        UpdateStaticScreen();
        UpdateLiveScreen();

        ShowSmallNotification("Shiny Rox added", $"+{settings.RoxPerReward:N0} Shiny Rox added.");
    }

    private void ResetRewardButton_Click(object sender, RoutedEventArgs e)
    {
        SaveSettingsFromInputs(updateTimerFromInputs: false);

        settings.NextRewardUtc = DateTime.UtcNow.AddHours(settings.RewardCycleHours);
        settings.IsTimerSet = true;
        settings.Save();

        LoadSettingsIntoInputs();
        UpdateStaticScreen();
        UpdateLiveScreen();

        ShowSmallNotification("Timer reset", $"Next Shiny Rox set to {settings.RewardCycleHours}:00:00.");
    }

    private void ExitButton_Click(object sender, RoutedEventArgs e)
    {
        ExitApp();
    }

    private void FindCosmeticButton_Click(object sender, RoutedEventArgs e)
    {
        string cosmeticName = CosmeticNameBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(cosmeticName))
        {
            CosmeticResultText.Text = "Type a cosmetic name first. Example: Sweatband.";
            return;
        }

        CosmeticResultsList.Items.Clear();
        cosmeticCandidates.Clear();
        selectedCosmetic = null;

        List<CosmeticSearchCandidate> matches = FindBuiltInCosmeticMatches(cosmeticName);

        if (matches.Count == 0)
        {
            CosmeticResultText.Text =
                $"No built-in match found for “{cosmeticName}”. Click Open Wiki or type the price manually into Cosmetic price / goal.";
            return;
        }

        foreach (CosmeticSearchCandidate candidate in matches)
        {
            cosmeticCandidates.Add(candidate);
            CosmeticResultsList.Items.Add(candidate);
        }

        CosmeticResultsList.SelectedIndex = 0;

        CosmeticResultText.Text =
            matches[0].Confidence >= 95
                ? $"Best match: {matches[0].Title} — {matches[0].Price:N0} Shiny Rox. Click “Use Selected Price” if it looks right."
                : $"Found {matches.Count} possible match(es). Pick the correct cosmetic, then click “Use Selected Price”.";
    }

    private void CosmeticResultsList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        selectedCosmetic = CosmeticResultsList.SelectedItem as CosmeticSearchCandidate;

        if (selectedCosmetic is not null)
        {
            CosmeticResultText.Text = $"Selected: {selectedCosmetic}. Use this only if the name looks right."; 
        }
    }

    private void UseSelectedCosmeticPriceButton_Click(object sender, RoutedEventArgs e)
    {
        if (selectedCosmetic is null)
        {
            CosmeticResultText.Text = "Search first, then select the correct cosmetic from the list.";
            return;
        }

        if (selectedCosmetic.Price == -2)
        {
            CosmeticResultText.Text = $"{selectedCosmetic.Title} is a creator badge. You cannot buy it with Shiny Rox. Apply through Another Axiom if you want to try for creator access.";
            OpenCreatorApplyPage();
            return;
        }

        if (selectedCosmetic.Price < 0)
        {
            CosmeticResultText.Text = "This price is unknown. Type the price manually into Cosmetic price / goal.";
            return;
        }

        GoalRoxBox.Text = selectedCosmetic.Price.ToString();
        SaveSettingsFromInputs(updateTimerFromInputs: false);
        UpdateStaticScreen();
        UpdateLiveScreen();

        CosmeticResultText.Text =
            $"Goal set to {selectedCosmetic.Price:N0} Shiny Rox for {selectedCosmetic.Title}.";
        ShowSmallNotification("Cosmetic goal set", $"{selectedCosmetic.Title}: {selectedCosmetic.Price:N0} Shiny Rox");
    }

    private void OpenCosmeticWikiButton_Click(object sender, RoutedEventArgs e)
    {
        string url;

        if (selectedCosmetic is not null && !string.IsNullOrWhiteSpace(selectedCosmetic.PageUrl))
        {
            url = selectedCosmetic.PageUrl;
        }
        else
        {
            string query = CosmeticNameBox.Text.Trim();
            url = string.IsNullOrWhiteSpace(query)
                ? "https://gorillatag.fandom.com/wiki/Cosmetics"
                : "https://gorillatag.fandom.com/wiki/Special:Search?query=" + Uri.EscapeDataString(query);
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
    }

    private static List<CosmeticSearchCandidate> FindBuiltInCosmeticMatches(string searchText)
    {
        string normalizedQuery = NormalizeName(searchText);
        string compactQuery = CompactName(searchText);

        return GetBuiltInCosmetics()
            .Select(cosmetic =>
            {
                cosmetic.Confidence = MatchScore(normalizedQuery, compactQuery, cosmetic.Title);
                return cosmetic;
            })
            .Where(c => c.Confidence >= 70)
            .OrderByDescending(c => c.Confidence)
            .ThenBy(c => c.Title.Length)
            .ThenBy(c => c.Title)
            .Take(8)
            .ToList();
    }

    private static List<CosmeticSearchCandidate> GetBuiltInCosmetics()
    {
        // Built-in starter list.
        // This avoids unreliable live wiki scraping.
        // Users can still type any missing price manually.
        return new List<CosmeticSearchCandidate>
        {
            new("Aviators", 1000),
            new("Banana Hat", 2000),
            new("Cloche", 1000),
            new("Coconut", 2000),
            new("Cowboy Hat", 1000),
            new("Fez", 1000),
            new("Sunhat", 1000),
            new("Tortoiseshell Sunglasses", 1000),
            new("Top Hat", 1000),

            new("Baseball Cap", 1000),
            new("Basic Beanie", 1000),
            new("Cat Ears", 1000),
            new("Chefs Hat", 2500),
            new("Flower Crown", 2000),
            new("Forehead Mirror", 2000),
            new("Golden Head", 5000),
            new("Headphones1", 2000),
            new("Party Hat", 500),
            new("Pineapple Hat", 2000),
            new("Sweatband", 500),
            new("Monke Cap", 1000),
            new("Gorilla Face Cap", 1000),
            new("Banana Logo Cap", 1000),
            new("Ushanka", 1000),
            new("White Fedora", 1500),
            new("Witch Hat", 1500),

            new("Basic Earrings", 1000),
            new("Basic Scarf", 1000),
            new("Big Eyebrows", 1000),
            new("Boxy Sunglasses", 2500),
            new("Double Eyepatch", 1000),
            new("Eyebrow Stud", 1000),
            new("Goggles", 2000),
            new("Left Eyepatch", 500),
            new("Nose Ring", 1000),
            new("Right Eyepatch", 500),
            new("Round Sunglasses", 1500),
            new("Skull Mask", 2200),
            new("Surgical Mask", 500),
            new("Triangle Sunglasses", 2000),
            new("Bowtie", 500),
            new("Canyon Pin", 1000),
            new("City Pin", 1000),
            new("Crystals Pin", 1000),
            new("Gorilla Pin", 2000),
            new("Mountain Pin", 1000),
            new("Neck Scarf", 1000),
            new("Tree Pin", 1000),
            new("Banana Logo Tee", 1000),
            new("I Love GT Tee", 1000),
            new("Gorilla Tag Flag", 2000),

            new("Star Balloon", 2000),
            new("Diamond Balloon", 1500),
            new("Chocolate Donut Balloon", 2500),
            new("Heart Balloon", 1500),
            new("Pink Donut Balloon", 2500),

            new("Clown Set", 4500),
            new("Mummy Wrap", 1500),
            new("Paperbag Hat", 2000),
            new("Pirate Bandana", 1500),
            new("Pumpkin Hat", 2000),
            new("Star Princess Set", 4500),
            new("Vampire Set", 4500),
            new("Werewolf Set", 4500),
            new("Witch Nose", 1000),

            new("Face Scarf", 2000),
            new("Maple Leaf", 3000),
            new("Turkey Finger Puppet", 4500),

            new("Pink Hawaiian Shirt", 2000),

            new("AA Creator Badge", -2),
            new("Finger Painter Badge", -2),
            new("Illustrator Badge", -2)
        };
    }

    private static int MatchScore(string normalizedQuery, string compactQuery, string title)
    {
        string normalizedTitle = NormalizeName(title);
        string compactTitle = CompactName(title);

        if (normalizedQuery == normalizedTitle || compactQuery == compactTitle) return 100;
        if (normalizedTitle.StartsWith(normalizedQuery) || compactTitle.StartsWith(compactQuery)) return 92;
        if (normalizedTitle.Contains(normalizedQuery) || compactTitle.Contains(compactQuery)) return 86;
        if (normalizedQuery.Contains(normalizedTitle) || compactQuery.Contains(compactTitle)) return 80;

        string[] queryWords = normalizedQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string[] titleWords = normalizedTitle.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (queryWords.Length == 0 || titleWords.Length == 0)
        {
            return 0;
        }

        int sharedWords = queryWords.Count(word => titleWords.Contains(word));

        if (sharedWords == queryWords.Length)
        {
            return 78;
        }

        return sharedWords > 0 ? 60 + sharedWords * 5 : 0;
    }

    private static string NormalizeName(string value)
    {
        value = Regex.Replace(value, "([a-z])([A-Z])", "$1 $2");
        value = Regex.Replace(value.ToLowerInvariant(), @"[^a-z0-9 ]", " ");
        value = Regex.Replace(value, @"\s+", " ").Trim();
        return value;
    }

    private static string CompactName(string value)
    {
        return Regex.Replace(NormalizeName(value), @"\s+", "");
    }

    private async void LoadCatalogButton_Click(object sender, RoutedEventArgs e)
    {
        CatalogStatusText.Text = "Loading all current cosmetics from Gorilla Tag Wiki...";
        CatalogList.Items.Clear();
        catalogCosmetics.Clear();
        selectedCatalogCosmetic = null;
        SelectedCatalogNameText.Text = "Select a cosmetic";
        SelectedCatalogPriceText.Text = "Price: select item";

        try
        {
            List<CatalogCosmetic> items = await LoadSmartCatalogAsync();

            if (items.Count == 0)
            {
                CatalogStatusText.Text = "No catalog items loaded. Try Open Wiki or type the price manually.";
                return;
            }

            foreach (CatalogCosmetic item in items)
            {
                catalogCosmetics.Add(item);
                CatalogList.Items.Add(item);
            }

            int known = items.Count(i => i.Price > 0);
            int unknown = items.Count(i => i.Price == -1);
            int free = items.Count(i => i.Price == 0);
            int creatorOnly = items.Count(i => i.Price == -2);

            CatalogStatusText.Text =
                $"Loaded {items.Count} current cosmetics. Priced: {known}, Free: {free}, Creator-only: {creatorOnly}, Unknown: {unknown}.";
        }
        catch (Exception ex)
        {
            CatalogStatusText.Text = "Online catalog failed. Loading built-in starter list instead. " + ex.Message;

            List<CatalogCosmetic> fallbackItems = GetBuiltInCatalogByYear(2021);

            foreach (CatalogCosmetic item in fallbackItems)
            {
                catalogCosmetics.Add(item);
                CatalogList.Items.Add(item);
            }

            CatalogStatusText.Text = $"Loaded {fallbackItems.Count} built-in 2021 cosmetics.";
        }
    }

    private void CatalogList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        selectedCatalogCosmetic = CatalogList.SelectedItem as CatalogCosmetic;

        if (selectedCatalogCosmetic is null)
        {
            return;
        }

        SelectedCatalogNameText.Text = selectedCatalogCosmetic.Title;
        SelectedCatalogPriceText.Text = selectedCatalogCosmetic.PriceDisplay;

        if (selectedCatalogCosmetic.Price == -2)
        {
            CatalogStatusText.Text = "This is a creator badge. It cannot be bought with Shiny Rox.";
        }
        else if (selectedCatalogCosmetic.Price < 0)
        {
            CatalogStatusText.Text = "Price unknown. Type the price manually into Cosmetic price / goal.";
        }
        else
        {
            CatalogStatusText.Text = "Selected cosmetic preview loaded.";
        }


    }

    private void UseGalleryPriceButton_Click(object sender, RoutedEventArgs e)
    {
        if (selectedCatalogCosmetic is null)
        {
            CatalogStatusText.Text = "Select a cosmetic from the catalog first.";
            return;
        }

        if (selectedCatalogCosmetic.Price == -2)
        {
            CatalogStatusText.Text = $"{selectedCatalogCosmetic.Title} is a creator badge. You cannot buy it with Shiny Rox. Apply through Another Axiom if you want to try for creator access.";
            OpenCreatorApplyPage();
            return;
        }

        if (selectedCatalogCosmetic.Price < 0)
        {
            CatalogStatusText.Text = "This cosmetic price is unknown. Type the price manually into Cosmetic price / goal.";
            return;
        }

        GoalRoxBox.Text = selectedCatalogCosmetic.Price.ToString();
        SaveSettingsFromInputs(updateTimerFromInputs: false);
        UpdateStaticScreen();
        UpdateLiveScreen();

        CatalogStatusText.Text =
            selectedCatalogCosmetic.Price == 0
                ? $"{selectedCatalogCosmetic.Title} is Free. Goal set to 0."
                : $"Goal set to {selectedCatalogCosmetic.Price:N0} Shiny Rox for {selectedCatalogCosmetic.Title}.";

        ShowSmallNotification(
            "Cosmetic goal set",
            selectedCatalogCosmetic.Price == 0
                ? $"{selectedCatalogCosmetic.Title}: Free"
                : $"{selectedCatalogCosmetic.Title}: {selectedCatalogCosmetic.Price:N0} Shiny Rox"
        );
    }

    private void OpenGalleryItemButton_Click(object sender, RoutedEventArgs e)
    {
        if (selectedCatalogCosmetic is null)
        {
            CatalogStatusText.Text = "Select a cosmetic from the catalog first.";
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = selectedCatalogCosmetic.PageUrl,
            UseShellExecute = true
        });
    }

    private static async Task<List<CatalogCosmetic>> LoadSmartCatalogAsync()
    {
        using HttpClient client = new();
        client.Timeout = TimeSpan.FromSeconds(25);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("GorillaShinyRox/1.8");

        // 1) Parse the Cosmetics page for price data.
        string html = await client.GetStringAsync("https://gorillatag.fandom.com/wiki/Cosmetics?action=render");
        List<CatalogCosmetic> pricedRows = ParseCosmeticsPageRows(html);

        Dictionary<string, CatalogCosmetic> priceByName = pricedRows
            .GroupBy(i => NormalizeName(i.Title))
            .ToDictionary(g => g.Key, g => g.OrderByDescending(i => i.Price).First());

        // 2) Load all current cosmetic pages from the category and use the page-image API for thumbnails.
        List<CatalogCosmetic> items = await LoadCategoryCatalogWithImagesAsync(client);

        // 3) Fill prices from the parsed table rows.
        foreach (CatalogCosmetic item in items)
        {
            string key = NormalizeName(item.Title);

            if (priceByName.TryGetValue(key, out CatalogCosmetic priced))
            {
                item.Price = priced.Price;
                item.Year = priced.Year;
            }
        }

        // 4) Fill known built-in prices and creator-only badges.
        Dictionary<string, CatalogCosmetic> builtIns = GetBuiltInCatalogByYear(2021)
            .GroupBy(c => NormalizeName(c.Title))
            .ToDictionary(g => g.Key, g => g.First());

        foreach (CatalogCosmetic item in items)
        {
            string key = NormalizeName(item.Title);

            if (item.Price < 0 && builtIns.TryGetValue(key, out CatalogCosmetic builtIn))
            {
                item.Price = builtIn.Price;
                item.Year = builtIn.Year;
                item.PageUrl = builtIn.PageUrl;
            }
        }

        // 5) Add parsed table items that were not in the category list.
        HashSet<string> existing = items.Select(i => NormalizeName(i.Title)).ToHashSet();

        foreach (CatalogCosmetic priced in pricedRows)
        {
            string key = NormalizeName(priced.Title);

            if (!existing.Contains(key))
            {
                items.Add(priced);
                existing.Add(key);
            }
        }

        // 6) Add built-ins and creator badges if missing.
        foreach (CatalogCosmetic builtIn in builtIns.Values)
        {
            string key = NormalizeName(builtIn.Title);

            if (!existing.Contains(key))
            {
                items.Add(builtIn);
                existing.Add(key);
            }
        }

        // 7) Images are disabled in the stable public beta.
        await AddThumbnailsToCatalogAsync(items);

        return items
            .GroupBy(i => i.Title, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(i => !string.IsNullOrWhiteSpace(i.ThumbnailUrl))
                          .ThenByDescending(i => i.Price)
                          .First())
            .OrderBy(i => i.Title)
            .ToList();
    }

    private static async Task<List<CatalogCosmetic>> LoadCategoryCatalogWithImagesAsync(HttpClient client)
    {
        List<string> titles = new();
        string? cmContinue = null;
        int safetyPages = 0;

        do
        {
            string url =
                "https://gorillatag.fandom.com/api.php?action=query&format=json&list=categorymembers&cmtitle=Category:Cosmetics&cmlimit=50";

            if (!string.IsNullOrWhiteSpace(cmContinue))
            {
                url += "&cmcontinue=" + Uri.EscapeDataString(cmContinue);
            }

            string json = await client.GetStringAsync(url);

            using JsonDocument doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("query", out JsonElement query) &&
                query.TryGetProperty("categorymembers", out JsonElement members) &&
                members.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement member in members.EnumerateArray())
                {
                    if (member.TryGetProperty("title", out JsonElement titleElement))
                    {
                        string? title = titleElement.GetString();

                        if (!string.IsNullOrWhiteSpace(title) &&
                            !title.StartsWith("Category:", StringComparison.OrdinalIgnoreCase) &&
                            !title.StartsWith("File:", StringComparison.OrdinalIgnoreCase))
                        {
                            titles.Add(title);
                        }
                    }
                }
            }

            cmContinue = null;

            if (doc.RootElement.TryGetProperty("continue", out JsonElement cont) &&
                cont.TryGetProperty("cmcontinue", out JsonElement contValue))
            {
                cmContinue = contValue.GetString();
            }

            safetyPages++;

        } while (!string.IsNullOrWhiteSpace(cmContinue) && safetyPages < 30);

        titles = titles
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(t => t)
            .ToList();

        Dictionary<string, string> thumbnails = await LoadThumbnailsForTitlesAsync(client, titles);

        return titles.Select(title => new CatalogCosmetic
        {
            Title = title,
            Price = -1,
            Year = 0,
            ThumbnailUrl = thumbnails.TryGetValue(title, out string? thumb) ? thumb : "",
            PageUrl = "https://gorillatag.fandom.com/wiki/" + Uri.EscapeDataString(title.Replace(' ', '_'))
        }).ToList();
    }

    private static List<CatalogCosmetic> ParseCosmeticsPageRows(string html)
    {
        List<CatalogCosmetic> results = new();

        if (string.IsNullOrWhiteSpace(html))
        {
            return results;
        }

        MatchCollection rows = Regex.Matches(html, @"<tr[^>]*>(.*?)</tr>", RegexOptions.Singleline | RegexOptions.IgnoreCase);

        foreach (Match rowMatch in rows)
        {
            string rowHtml = rowMatch.Groups[1].Value;

            string title = ExtractFirstCosmeticTitleFromRow(rowHtml);
            if (string.IsNullOrWhiteSpace(title))
            {
                continue;
            }

            string rowText = CleanHtmlText(rowHtml);
            int price = ExtractSmartPrice(rowText);
            string thumbnailUrl = ExtractThumbnailUrlFromRow(rowHtml);

            // Year is not always available in the wiki table. Start unknown as 0.
            int year = ExtractYear(rowText);

            results.Add(new CatalogCosmetic
            {
                Title = title,
                Price = price,
                Year = year,
                ThumbnailUrl = thumbnailUrl,
                PageUrl = "https://gorillatag.fandom.com/wiki/" + Uri.EscapeDataString(title.Replace(' ', '_'))
            });
        }

        return results;
    }

    private static string ExtractThumbnailUrlFromRow(string rowHtml)
    {
        if (string.IsNullOrWhiteSpace(rowHtml))
        {
            return "";
        }

        // Fandom tables often use lazy image attributes like data-src.
        MatchCollection imgMatches = Regex.Matches(
            rowHtml,
            @"<img\b[^>]*(?:data-src|src)=[""']([^""']+)[""'][^>]*>",
            RegexOptions.Singleline | RegexOptions.IgnoreCase
        );

        foreach (Match imgMatch in imgMatches)
        {
            string url = System.Net.WebUtility.HtmlDecode(imgMatch.Groups[1].Value);

            if (string.IsNullOrWhiteSpace(url))
            {
                continue;
            }

            if (url.StartsWith("//"))
            {
                url = "https:" + url;
            }

            if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Skip tiny placeholders/spacers.
            if (url.Contains("data:image", StringComparison.OrdinalIgnoreCase) ||
                url.Contains("blank", StringComparison.OrdinalIgnoreCase) ||
                url.Contains("transparent", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return url;
        }

        return "";
    }

    private static string ExtractFirstCosmeticTitleFromRow(string rowHtml)
    {
        MatchCollection cells = Regex.Matches(
            rowHtml,
            @"<t[dh]\b[^>]*>(.*?)</t[dh]>",
            RegexOptions.Singleline | RegexOptions.IgnoreCase
        );

        // Most wiki tables put the image in the first cell and the cosmetic name in the next useful cell.
        foreach (Match cell in cells.Cast<Match>().Skip(1))
        {
            string cellHtml = cell.Groups[1].Value;
            string cellText = CleanHtmlText(cellHtml);

            if (LooksLikeCosmeticName(cellText))
            {
                return cellText;
            }
        }

        MatchCollection linkMatches = Regex.Matches(
            rowHtml,
            @"<a\b[^>]*href=""/wiki/([^""]+)""[^>]*>(.*?)</a>",
            RegexOptions.Singleline | RegexOptions.IgnoreCase
        );

        foreach (Match linkMatch in linkMatches)
        {
            string href = Uri.UnescapeDataString(linkMatch.Groups[1].Value.Replace("_", " "));
            string anchor = CleanHtmlText(linkMatch.Groups[2].Value);

            string title = !string.IsNullOrWhiteSpace(anchor) ? anchor : href;

            if (LooksLikeCosmeticName(title))
            {
                return title;
            }
        }

        return "";
    }

    private static bool LooksLikeCosmeticName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string title = value.Trim();

        if (title.Length < 2 || title.Length > 80)
        {
            return false;
        }

        if (title.StartsWith("File:", StringComparison.OrdinalIgnoreCase) ||
            title.StartsWith("Image:", StringComparison.OrdinalIgnoreCase) ||
            title.StartsWith("Category:", StringComparison.OrdinalIgnoreCase) ||
            title.Contains(".png", StringComparison.OrdinalIgnoreCase) ||
            title.Contains(".jpg", StringComparison.OrdinalIgnoreCase) ||
            title.Equals("Image", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Skip ID-like values and pure numbers.
        if (Regex.IsMatch(title, @"^[A-Z]{1,4}[A-Z0-9]{2,}$") ||
            Regex.IsMatch(title, @"^[0-9,]+$"))
        {
            return false;
        }

        return true;
    }

    private static int ExtractSmartPrice(string rowText)
    {
        if (string.IsNullOrWhiteSpace(rowText))
        {
            return -1;
        }

        if (Regex.IsMatch(rowText, @"(?i)\bfree\b|\b0\s*shiny\b|\b0\s*rocks\b"))
        {
            return 0;
        }

        // Prefer numbers close to Shiny Rocks/Rox text.
        Match closeMatch = Regex.Match(
            rowText,
            @"(?i)([0-9][0-9,]{0,5})\s*(?:shiny\s*(?:rocks|rox|rock)|rocks)|(?:shiny\s*(?:rocks|rox|rock)|rocks)\s*([0-9][0-9,]{0,5})"
        );

        if (closeMatch.Success)
        {
            string raw = !string.IsNullOrWhiteSpace(closeMatch.Groups[1].Value)
                ? closeMatch.Groups[1].Value
                : closeMatch.Groups[2].Value;

            if (int.TryParse(raw.Replace(",", ""), out int price))
            {
                return Math.Max(0, price);
            }
        }

        // If the row is from a table, the final number is often the price.
        MatchCollection numbers = Regex.Matches(rowText, @"\b([0-9][0-9,]{0,5})\b");

        if (numbers.Count > 0)
        {
            for (int i = numbers.Count - 1; i >= 0; i--)
            {
                if (int.TryParse(numbers[i].Groups[1].Value.Replace(",", ""), out int number))
                {
                    // Avoid years as prices.
                    if (number >= 2020 && number <= 2035)
                    {
                        continue;
                    }

                    return Math.Max(0, number);
                }
            }
        }

        return -1;
    }

    private static int ExtractYear(string rowText)
    {
        Match year = Regex.Match(rowText, @"\b(2021|2022|2023|2024|2025|2026)\b");

        if (year.Success && int.TryParse(year.Groups[1].Value, out int value))
        {
            return value;
        }

        return 0;
    }

    private static string CleanHtmlText(string html)
    {
        string noTags = Regex.Replace(html, "<.*?>", " ");
        string decoded = System.Net.WebUtility.HtmlDecode(noTags);
        decoded = Regex.Replace(decoded, @"\s+", " ").Trim();
        return decoded;
    }

    private static List<CatalogCosmetic> GetBuiltInCatalogByYear(int year)
    {
        if (year != 2021)
        {
            return new List<CatalogCosmetic>();
        }

        // Starter 2021 known-price catalog.
        // Price -1 = unknown. Price 0 = Free.
        return new List<CatalogCosmetic>
        {
            new("Aviators", 1000, 2021),
            new("Banana Hat", 2000, 2021),
            new("Cloche", 1000, 2021),
            new("Coconut", 2000, 2021),
            new("Cowboy Hat", 1000, 2021),
            new("Fez", 1000, 2021),
            new("Sunhat", 1000, 2021),
            new("Tortoiseshell Sunglasses", 1000, 2021),
            new("Top Hat", 1000, 2021),

            new("Baseball Cap", 1000, 2021),
            new("Basic Beanie", 1000, 2021),
            new("Cat Ears", 1000, 2021),
            new("Chefs Hat", 2500, 2021),
            new("Flower Crown", 2000, 2021),
            new("Forehead Mirror", 2000, 2021),
            new("Golden Head", 5000, 2021),
            new("Headphones1", 2000, 2021),
            new("Party Hat", 500, 2021),
            new("Pineapple Hat", 2000, 2021),
            new("Sweatband", 500, 2021),
            new("Monke Cap", 1000, 2021),
            new("Gorilla Face Cap", 1000, 2021),
            new("Banana Logo Cap", 1000, 2021),
            new("Ushanka", 1000, 2021),
            new("White Fedora", 1500, 2021),
            new("Witch Hat", 1500, 2021),

            new("Basic Earrings", 1000, 2021),
            new("Basic Scarf", 1000, 2021),
            new("Big Eyebrows", 1000, 2021),
            new("Boxy Sunglasses", 2500, 2021),
            new("Double Eyepatch", 1000, 2021),
            new("Eyebrow Stud", 1000, 2021),
            new("Goggles", 2000, 2021),
            new("Left Eyepatch", 500, 2021),
            new("Nose Ring", 1000, 2021),
            new("Right Eyepatch", 500, 2021),
            new("Round Sunglasses", 1500, 2021),
            new("Skull Mask", 2200, 2021),
            new("Surgical Mask", 500, 2021),
            new("Triangle Sunglasses", 2000, 2021),

            new("Bowtie", 500, 2021),
            new("Canyon Pin", 1000, 2021),
            new("City Pin", 1000, 2021),
            new("Crystals Pin", 1000, 2021),
            new("Gorilla Pin", 2000, 2021),
            new("Mountain Pin", 1000, 2021),
            new("Neck Scarf", 1000, 2021),
            new("Tree Pin", 1000, 2021),
            new("Banana Logo Tee", 1000, 2021),
            new("I Love GT Tee", 1000, 2021),
            new("Gorilla Tag Flag", 2000, 2021),

            new("AA Creator Badge", -2, 0),
            new("Finger Painter Badge", -2, 0),
            new("Illustrator Badge", -2, 0)
        }
        .OrderBy(c => c.Year <= 0 ? 9999 : c.Year)
        .ThenBy(c => c.Title)
        .ToList();
    }

    private static Task AddThumbnailsToCatalogAsync(List<CatalogCosmetic> items)
    {
        // Images are intentionally disabled for the stable public beta.
        // The catalog remains text-only so it is faster and more reliable.
        return Task.CompletedTask;
    }

    private static async Task<Dictionary<string, string>> LoadThumbnailsForTitlesAsync(HttpClient client, List<string> titles)
    {
        Dictionary<string, string> result = new(StringComparer.OrdinalIgnoreCase);

        if (titles.Count == 0)
        {
            return result;
        }

        const int batchSize = 40;

        for (int start = 0; start < titles.Count; start += batchSize)
        {
            List<string> batch = titles.Skip(start).Take(batchSize).ToList();
            string joinedTitles = string.Join("|", batch.Select(t => t.Replace("|", " ")));

            string url =
                "https://gorillatag.fandom.com/api.php?action=query&format=json&prop=pageimages&piprop=thumbnail&pithumbsize=96&titles=" +
                Uri.EscapeDataString(joinedTitles);

            string json = await client.GetStringAsync(url);

            using JsonDocument doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("query", out JsonElement query) ||
                !query.TryGetProperty("pages", out JsonElement pages))
            {
                continue;
            }

            foreach (JsonProperty pageProperty in pages.EnumerateObject())
            {
                JsonElement page = pageProperty.Value;

                if (!page.TryGetProperty("title", out JsonElement titleElement))
                {
                    continue;
                }

                string title = titleElement.GetString() ?? "";

                if (page.TryGetProperty("thumbnail", out JsonElement thumbnail) &&
                    thumbnail.TryGetProperty("source", out JsonElement sourceElement))
                {
                    string source = sourceElement.GetString() ?? "";

                    if (!string.IsNullOrWhiteSpace(title) && !string.IsNullOrWhiteSpace(source))
                    {
                        result[title] = source;
                    }
                }
            }
        }

        return result;
    }

    private static void OpenCreatorApplyPage()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = CreatorApplyUrl,
            UseShellExecute = true
        });
    }

    private TimeSpan GetTimeUntilNextReward()
    {
        if (!settings.IsTimerSet)
        {
            return TimeSpan.Zero;
        }

        TimeSpan timeLeft = settings.NextRewardUtc - DateTime.UtcNow;
        return timeLeft < TimeSpan.Zero ? TimeSpan.Zero : timeLeft;
    }

    private string FormatLiveTimer()
    {
        TimeSpan next = GetTimeUntilNextReward();
        int hours = (int)Math.Floor(next.TotalHours);
        return $"{hours:00}:{next.Minutes:00}:{next.Seconds:00}";
    }

    private (int remaining, double rewardsLeft, double daysLeft, double monthsLeft, DateTime eta) CalculateGoalTime()
    {
        int remaining = Math.Max(0, settings.GoalRox - settings.CurrentRox);
        double rewardsLeft = Math.Ceiling(remaining / (double)Math.Max(1, settings.RoxPerReward));

        TimeSpan next = GetTimeUntilNextReward();
        double totalHoursLeft = 0;

        if (remaining > 0 && rewardsLeft > 0)
        {
            totalHoursLeft = next.TotalHours + Math.Max(0, rewardsLeft - 1) * settings.RewardCycleHours;
        }

        double daysLeft = totalHoursLeft / 24.0;
        double monthsLeft = daysLeft / 30.44;
        DateTime eta = DateTime.Now.AddHours(totalHoursLeft);

        return (remaining, rewardsLeft, daysLeft, monthsLeft, eta);
    }

    private void UpdateStaticScreen()
    {
        CurrentRoxText.Text = settings.CurrentRox.ToString("N0");
        RewardText.Text = $"+{settings.RoxPerReward:N0} Shiny Rox";

        int goal = Math.Max(1, settings.GoalRox);
        double progress = Math.Clamp(settings.CurrentRox / (double)goal * 100.0, 0, 100);

        ProgressBar.Value = progress;
        ProgressText.Text = $"{progress:0}%";
    }

    private void UpdateLiveScreen()
    {
        LiveTimerText.Text = FormatLiveTimer();

        (int remaining, double rewardsLeft, double daysLeft, double monthsLeft, DateTime eta) = CalculateGoalTime();

        if (remaining <= 0)
        {
            RemainingText.Text = "Cosmetic goal reached";
            GoalTimeText.Text = "You have enough Shiny Rox.";
            HeaderEtaText.Text = "Ready";
            GoalDateText.Text = "Ready";
            GoalTimeOfDayText.Text = "";
            StatusText.Text = "Ready to buy";
            RewardsLeftText.Text = "No rewards needed";
            EtaFullText.Text = "Cosmetic goal reached";
            return;
        }

        RemainingText.Text = $"{remaining:N0} Shiny Rox needed";

        if (!settings.IsTimerSet)
        {
            GoalTimeText.Text = "Enter the next Shiny Rox timer, then click Set Timer.";
            HeaderEtaText.Text = "Set timer";
            GoalDateText.Text = "Set timer";
            GoalTimeOfDayText.Text = "";
            StatusText.Text = "Waiting for timer";
            RewardsLeftText.Text = $"{rewardsLeft:0} rewards needed";
            EtaFullText.Text = "Enter hours/minutes to calculate unlock date";
            return;
        }

        GoalTimeText.Text = $"About {monthsLeft:0.0} months / {daysLeft:0} days left.";
        HeaderEtaText.Text = eta.ToString("MMM d, yyyy");
        GoalDateText.Text = eta.ToString("MMM d, yyyy");
        GoalTimeOfDayText.Text = eta.ToString("h:mm tt");
        StatusText.Text = "Timer running";
        RewardsLeftText.Text = $"{rewardsLeft:0} rewards left";
        EtaFullText.Text = $"Estimated: {eta:f}";
    }

    private string GetNotificationText()
    {
        (int remaining, _, _, double monthsLeft, _) = CalculateGoalTime();

        if (remaining <= 0)
        {
            return "You have enough Shiny Rox.";
        }

        if (!settings.IsTimerSet)
        {
            return $"{settings.CurrentRox:N0} current | enter next timer to calculate unlock date";
        }

        return $"{settings.CurrentRox:N0} current | next reward in {FormatLiveTimer()} | {monthsLeft:0.0} months left";
    }

    private void ShowSmallNotification(string title, string message)
    {
        trayIcon.BalloonTipTitle = title;
        trayIcon.BalloonTipText = message;
        trayIcon.BalloonTipIcon = Forms.ToolTipIcon.Info;
        trayIcon.ShowBalloonTip(5000);
    }

    private sealed class CatalogCosmetic
    {
        public CatalogCosmetic()
        {
        }

        public CatalogCosmetic(string title, int price, int year)
        {
            Title = title;
            Price = price;
            Year = year;
            PageUrl = price == -2
                ? CreatorApplyUrl
                : "https://gorillatag.fandom.com/wiki/" + Uri.EscapeDataString(title.Replace(' ', '_'));
        }

        public string Title { get; set; } = "";
        public int Price { get; set; } = -1;
        public int Year { get; set; }
        public string ThumbnailUrl { get; set; } = "";
        public string PageUrl { get; set; } = "";

        public string PriceText => Price == -2
            ? "Creator badge • Not buyable"
            : Price < 0
                ? $"{YearText} • Price unknown"
                : Price == 0
                    ? $"{YearText} • Free"
                    : $"{YearText} • {Price:N0} Shiny Rox";

        public string PriceDisplay => Price == -2
            ? "Creator badge • Not buyable with Shiny Rox"
            : Price < 0
                ? "Price unknown"
                : Price == 0
                    ? "Price: Free"
                    : $"Price: {Price:N0} Shiny Rox";

        private string YearText => Year > 0 ? Year.ToString() : "Year unknown";
    }

    private sealed class CosmeticSearchCandidate
    {
        public CosmeticSearchCandidate()
        {
        }

        public CosmeticSearchCandidate(string title, int price)
        {
            Title = title;
            Price = price;
            PageUrl = price == -2
                ? CreatorApplyUrl
                : "https://gorillatag.fandom.com/wiki/" + Uri.EscapeDataString(title.Replace(' ', '_'));
        }

        public string Title { get; set; } = "";
        public int Price { get; set; }
        public string PageUrl { get; set; } = "";
        public int Confidence { get; set; }

        public override string ToString()
        {
            if (Price == -2)
            {
                return $"{Title} — Creator badge / Not buyable";
            }

            if (Price < 0)
            {
                return $"{Title} — Price unknown";
            }

            if (Price == 0)
            {
                return $"{Title} — Free";
            }

            return $"{Title} — {Price:N0} Shiny Rox";
        }
    }
}

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using TerminalHyperspace.Engine;
using TerminalHyperspace.UI;

namespace TerminalHyperspace;

public partial class MainWindow : Window
{
    private readonly GuiBridge _bridge = new();

    // (label, command-text-submitted-to-game)
    private static readonly (string Label, string Command)[] Commands =
    {
        ("─ Command Panel ─",    ""),
        ("Look",          "look"),
        ("⧫ Locator",       "locator"),
        ("⊞ Map",           "map"),
        ("≡ Status / Sheet","status"),
        ("∶ Inventory",     "inventory"),
        ("Equip…",        "equip"),
        ("Vehicles",      "vehicles"),
        ("Enter Ship",    "enter ship"),
        ("Enter Land",    "enter land"),
        ("Disembark",     "disembark"),
        ("─ Move ─",      ""),
        ("◓ North",         "north"),
        ("◒ South",         "south"),
        ("◑ East",          "east"),
        ("◐ West",          "west"),
        ("◷ Northeast",     "northeast"),
        ("◴ Northwest",     "northwest"),
        ("◶ Southeast",     "southeast"),
        ("◵ Southwest",     "southwest"),
        ("⇡ Up",            "up"),
        ("⇣ Down",          "down"),
        ("Dock",          "dock"),
        ("Land",          "land"),
        ("Jump",          "jump"),
        ("Explore",       "explore"),
        ("Board",         "board"),
        ("Leave",         "leave"),
        ("Airlock",       "airlock"),
        ("─ Action ─",    ""),
        ("◍◍ Search / Scan", "search"),
        ("⊜ Talk",          "talk"),
        ("✚ Use Medpack",   "use medpack"),
        ("≃ Rest",          "rest"),
        ("⊸ Journal",       "journal"),
        ("✦ Upgrade",       "upgrade"),
        ("⚅ Roll…",         "roll"),
        ("─ Commerce ─",  ""),
        ("ᶽ Shop",          "shop"),
        ("Armor Shop",    "ashop"),
        ("Vehicle Shop",  "vshop"),
        ("Sell Item",     "sell"),
        ("Sell Vehicle",  "sellv"),
        ("─ System ─",    ""),
        ("Save",          "save"),
        ("Load",          "load"),
        ("Saves",         "saves"),
        ("Help",          "help"),
        ("Quit",          "quit"),
    };

    public MainWindow()
    {
        InitializeComponent();
        BuildCommandSidebar();

        GuiBridge.Instance = _bridge;
        _bridge.OnWrite += OnBridgeWrite;
        _bridge.OnClear += OnBridgeClear;
        _bridge.OnRenderMap += OnBridgeRenderMap;
        _bridge.OnCharacterUpdate += OnBridgeCharacterUpdate;

        Opened += (_, _) =>
        {
            InputBox.Focus();
            var thread = new Thread(() =>
            {
                try { GameRunner.Run(); }
                catch (Exception ex)
                {
                    Dispatcher.UIThread.Post(() =>
                        AppendRun($"\n[fatal] {ex}\n", ConsoleColor.DarkRed));
                }
            }) { IsBackground = true, Name = "GameLoop" };
            thread.Start();
        };

        Closed += (_, _) =>
        {
            _bridge.SubmitInput("quit");
        };
    }

    private void BuildCommandSidebar()
    {
        WrapPanel? currentWrap = null;
        foreach (var (label, command) in Commands)
        {
            if (string.IsNullOrEmpty(command))
            {
                // Start a new wrap section. The divider is the first child of the
                // WrapPanel so it sits inline with the buttons that follow it
                // (and wraps onto its own row only when space runs out).
                currentWrap = new WrapPanel
                {
                    Orientation = Avalonia.Layout.Orientation.Horizontal,
                    Margin = new Avalonia.Thickness(0, 4, 0, 4)
                };
                currentWrap.Children.Add(new TextBlock
                {
                    Text = label,
                    Foreground = new SolidColorBrush(Color.Parse("#00783A")),
                    Margin = new Avalonia.Thickness(0, 0, 8, 0),
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                    FontFamily = Fonts.MonoBold
                });
                CommandPanel.Children.Add(currentWrap);
                continue;
            }

            // Defensive: if Commands starts with a button (no leading divider) make sure
            // we have somewhere to put it.
            if (currentWrap == null)
            {
                currentWrap = new WrapPanel { Orientation = Avalonia.Layout.Orientation.Horizontal };
                CommandPanel.Children.Add(currentWrap);
            }

            var button = new Button
            {
                Content = label,
                HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                Padding = new Avalonia.Thickness(8, 4),
                Margin = new Avalonia.Thickness(0, 0, 4, 4),
                FontFamily = Fonts.MonoBold,
                Foreground = Palette.GreenMidBrush
            };
            button.Click += (_, _) =>
            {
                Submit(command);
                InputBox.Focus();
            };
            currentWrap.Children.Add(button);
        }
    }

    private void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            Submit(InputBox.Text ?? "");
            InputBox.Text = "";
        }
    }

    private void OnSendClick(object? sender, RoutedEventArgs e)
    {
        Submit(InputBox.Text ?? "");
        InputBox.Text = "";
        InputBox.Focus();
    }

    private void Submit(string text)
    {
        // Echo back into the output for context
        Dispatcher.UIThread.Post(() => AppendRun($"{text}\n", ConsoleColor.Green));
        _bridge.SubmitInput(text);
    }

    private void OnBridgeWrite(string text, ConsoleColor color, bool newLine)
    {
        var payload = newLine ? text + "\n" : text;
        Dispatcher.UIThread.Post(() => AppendRun(payload, color));
    }

    private void OnBridgeClear()
    {
        Dispatcher.UIThread.Post(() => OutputBlock.Inlines?.Clear());
    }

    private void OnBridgeRenderMap(MapSnapshot snap)
        => Dispatcher.UIThread.Post(() => DrawMap(snap));

    private void OnBridgeCharacterUpdate(CharacterSnapshot snap)
        => Dispatcher.UIThread.Post(() =>
        {
            OvName.Text    = snap.Name;
            OvSpecies.Text = $"Species: {snap.Species}";
            OvRole.Text    = $"Role:    {snap.Role}";
            OvResolve.Text = $"Resolve: {snap.Resolve}/{snap.MaxResolve}";
            OvCredits.Text = $"Credits: {snap.Credits}";
            OvTurns.Text   = $"Turns:   {snap.TurnCount}";

            OvInventory.Children.Clear();
            if (snap.Inventory.Count == 0)
            {
                OvInventory.Children.Add(new TextBlock
                {
                    Text = "(empty)",
                    Foreground = new SolidColorBrush(Color.Parse("#666666")),
                    FontFamily = Fonts.MonoOblique,
                    FontSize = 12
                });
                return;
            }

            foreach (var entry in snap.Inventory)
            {
                var color = entry.IsMissionItem
                    ? Palette.GoldPollen
                    : entry.IsEquipped ? Palette.GreenMid : Color.Parse("#DDDDDD");

                var label = entry.Name;
                if (entry.IsEquipped) label += " ★";

                var tile = new Border
                {
                    Background = new SolidColorBrush(Color.Parse("#0F1A22")),
                    BorderBrush = new SolidColorBrush(Color.Parse("#2A3540")),
                    BorderThickness = new Avalonia.Thickness(1),
                    CornerRadius = new Avalonia.CornerRadius(3),
                    Padding = new Avalonia.Thickness(6, 3),
                    Margin = new Avalonia.Thickness(0, 0, 4, 4),
                    Child = new TextBlock
                    {
                        Text = label,
                        Foreground = new SolidColorBrush(color),
                        FontFamily = Fonts.MonoRegular,
                        FontSize = 12,
                        TextWrapping = TextWrapping.NoWrap
                    }
                };
                if (entry.IsMissionItem && !string.IsNullOrEmpty(entry.MissionDestination))
                    ToolTip.SetTip(tile, $"Deliver to: {entry.MissionDestination}");
                OvInventory.Children.Add(tile);
            }
        });

    private const int CellW = 150;
    private const int CellH = 70;
    private const int RoomW = 130;
    private const int RoomH = 44;
    private const int Pad = 30;

    private void DrawMap(MapSnapshot snap)
    {
        // Map pane is always visible now; just redraw with the new snapshot.
        MapCanvas.Children.Clear();
        MapTitle.Text = $"Map — {snap.Planet}";

        if (snap.Rooms.Count == 0) return;

        int minX = snap.Rooms.Min(r => r.X), maxX = snap.Rooms.Max(r => r.X);
        int minY = snap.Rooms.Min(r => r.Y), maxY = snap.Rooms.Max(r => r.Y);
        int gridCols = maxX - minX + 1;
        int gridRows = maxY - minY + 1;
        MapCanvas.Width  = gridCols * CellW + Pad * 2;
        MapCanvas.Height = gridRows * CellH + Pad * 2 + (snap.OrphanNames.Count + snap.NonCompassExits.Count) * 18 + 40;

        (double cx, double cy) Center(int gx, int gy) =>
            (Pad + (gx - minX) * CellW + CellW / 2.0,
             Pad + (gy - minY) * CellH + CellH / 2.0);

        var roomsById = snap.Rooms.ToDictionary(r => r.Id);

        // Edges first (drawn under rooms)
        var edgeBrush = new SolidColorBrush(Color.Parse("#3A6A8A"));
        foreach (var e in snap.Edges)
        {
            if (!roomsById.TryGetValue(e.FromId, out var a)) continue;
            if (!roomsById.TryGetValue(e.ToId, out var b)) continue;
            var (ax, ay) = Center(a.X, a.Y);
            var (bx, by) = Center(b.X, b.Y);
            MapCanvas.Children.Add(new Line
            {
                StartPoint = new Point(ax, ay),
                EndPoint = new Point(bx, by),
                Stroke = edgeBrush,
                StrokeThickness = 2,
            });
        }

        // Rooms
        foreach (var r in snap.Rooms)
        {
            var (cx, cy) = Center(r.X, r.Y);
            double left = cx - RoomW / 2.0;
            double top  = cy - RoomH / 2.0;

            var fill = Color.Parse("#22171B");
            var stroke = r.IsCurrent
                ? Color.Parse("#B8202C")
                : (r.Visited ? Color.Parse("#BF9C5E") : Color.Parse("#5B4E53"));
            var textColor = r.IsCurrent
                ? Color.Parse("#B8202C")
                : (r.Visited ? Color.Parse("#BF9C5E") : Color.Parse("#5B4E53"));

            var rect = new Rectangle
            {
                Width = RoomW,
                Height = RoomH,
                Fill = new SolidColorBrush(fill),
                Stroke = new SolidColorBrush(stroke),
                StrokeThickness = r.IsCurrent ? 2.5 : 2,
                RadiusX = 4, RadiusY = 4,
            };
            Canvas.SetLeft(rect, left);
            Canvas.SetTop(rect, top);
            MapCanvas.Children.Add(rect);

            var label = new TextBlock
            {
                Text = r.Visited ? r.Name : "???",
                Width = RoomW - 8,
                Height = RoomH - 4,
                Foreground = new SolidColorBrush(textColor),
                FontFamily = r.IsCurrent ? Fonts.MonoBold : Fonts.MonoRegular,
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            };
            Canvas.SetLeft(label, left + 4);
            Canvas.SetTop(label, top + 4);
            MapCanvas.Children.Add(label);
        }

        // Orphans + non-compass exits listed below the grid
        double y = Pad + gridRows * CellH + 20;
        if (snap.OrphanNames.Count > 0)
        {
            AddNote("Disconnected rooms (no compass route from here):", y, Color.Parse("#AAAAAA"), bold: true);
            y += 18;
            foreach (var n in snap.OrphanNames)
            {
                AddNote("  • " + n, y, Color.Parse("#888888"));
                y += 16;
            }
            y += 8;
        }
        if (snap.NonCompassExits.Count > 0)
        {
            AddNote("Non-compass exits from here:", y, Color.Parse("#AAAAAA"), bold: true);
            y += 18;
            foreach (var (dir, name) in snap.NonCompassExits)
            {
                AddNote($"  {dir} → {name}", y, Color.Parse("#888888"));
                y += 16;
            }
        }
    }

    private void AddNote(string text, double y, Color color, bool bold = false)
    {
        var tb = new TextBlock
        {
            Text = text,
            Foreground = new SolidColorBrush(color),
            FontFamily = bold ? Fonts.MonoBold : Fonts.MonoRegular,
            FontSize = 14,
        };
        Canvas.SetLeft(tb, Pad);
        Canvas.SetTop(tb, y);
        MapCanvas.Children.Add(tb);
    }

    private void AppendRun(string text, ConsoleColor color)
    {
        OutputBlock.Inlines ??= new InlineCollection();
        OutputBlock.Inlines.Add(new Run
        {
            Text = text,
            Foreground = new SolidColorBrush(MapColor(color))
        });

        // Auto-scroll to bottom
        Dispatcher.UIThread.Post(() => OutputScroller.ScrollToEnd(),
            DispatcherPriority.Background);
    }

    private static Color MapColor(ConsoleColor c) => c switch
    {
        ConsoleColor.Black       => Color.Parse("#000000"),
        ConsoleColor.DarkBlue    => Color.Parse("#3B5998"),
        ConsoleColor.DarkGreen   => Palette.GreenDark,
        ConsoleColor.DarkCyan    => Palette.CyanDark,
        ConsoleColor.DarkRed     => Color.Parse("#C0392B"),
        ConsoleColor.DarkMagenta => Color.Parse("#9B59B6"),
        ConsoleColor.DarkYellow  => Palette.OrangeTangerine,
        ConsoleColor.Gray        => Color.Parse("#BBBBBB"),
        ConsoleColor.DarkGray    => Color.Parse("#777777"),
        ConsoleColor.Blue        => Color.Parse("#5599FF"),
        ConsoleColor.Green       => Color.Parse("#4FFFA6"),
        ConsoleColor.Cyan        => Palette.CyanMid,
        ConsoleColor.Red         => Palette.PinkCoral,
        ConsoleColor.Magenta     => Color.Parse("#FF77FF"),
        ConsoleColor.Yellow      => Palette.GoldPollen,
        ConsoleColor.White       => Color.Parse("#EEEEEE"),
        _                        => Color.Parse("#DDDDDD"),
    };
}

using System;
using System.Collections.Generic;
using System.Windows.Media;

namespace AntigravityTokenOrb
{
    public class ThemePalette
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public bool IsLight { get; set; }

        // Panel & Cards
        public string WindowBg { get; set; }
        public string WindowBorder { get; set; }
        public string CardBg { get; set; }
        public string CardBorder { get; set; }
        public string TrackBg { get; set; }
        public string HoverBg { get; set; }
        public string SelectedRowBg { get; set; }
        public string SelectedRowBorder { get; set; }

        // Text
        public string TextPrimary { get; set; }
        public string TextSecondary { get; set; }
        public string TextMuted { get; set; }

        // Accents
        public string AccentPrimary { get; set; }
        public string AccentSecondary { get; set; }
        public string AccentOutput { get; set; }
        public string AccentCache { get; set; }
        public string AccentCost { get; set; }
        public string AccentActive { get; set; }
        public string AccentWarn { get; set; }
        public string AccentDanger { get; set; }

        // Orb Specific
        public string OrbCoreTop { get; set; }
        public string OrbCoreMid { get; set; }
        public string OrbCoreEdge { get; set; }
        public string OrbBorder { get; set; }
        public string OrbRingTrack { get; set; }
        public string OrbBottomPillBg { get; set; }
        public string PillBg { get; set; }
        public string PillBorder { get; set; }

        public SolidColorBrush Brush(string hex)
        {
            var c = (Color)ColorConverter.ConvertFromString(hex);
            var b = new SolidColorBrush(c);
            b.Freeze();
            return b;
        }

        public Color ColorOf(string hex)
        {
            return (Color)ColorConverter.ConvertFromString(hex);
        }
    }

    public static class ThemeManager
    {
        public static readonly List<ThemePalette> AllThemes = new List<ThemePalette>
        {
            new ThemePalette
            {
                Id = "neon",
                Name = "深空霓虹 (Cyber Neon)",
                IsLight = false,
                WindowBg = "#F50D111D",
                WindowBorder = "#2A3550",
                CardBg = "#131A2D",
                CardBorder = "#202B46",
                TrackBg = "#1A2338",
                HoverBg = "#223254",
                SelectedRowBg = "#1D2944",
                SelectedRowBorder = "#334B7A",
                TextPrimary = "#F8FAFC",
                TextSecondary = "#CBD5E1",
                TextMuted = "#7A8BA6",
                AccentPrimary = "#38BDF8",
                AccentSecondary = "#A78BFA",
                AccentOutput = "#F472B6",
                AccentCache = "#34D399",
                AccentCost = "#FBBF24",
                AccentActive = "#34D399",
                AccentWarn = "#F59E0B",
                AccentDanger = "#F43F5E",
                OrbCoreTop = "#F21C2640",
                OrbCoreMid = "#F40E1424",
                OrbCoreEdge = "#F8080B14",
                OrbBorder = "#2A3858",
                OrbRingTrack = "#1E2940",
                OrbBottomPillBg = "#88152238",
                PillBg = "#F00E1424",
                PillBorder = "#2E3E63"
            },
            new ThemePalette
            {
                Id = "emerald",
                Name = "翡翠矩阵 (Emerald Matrix)",
                IsLight = false,
                WindowBg = "#F5081412",
                WindowBorder = "#1C4539",
                CardBg = "#0E1F1B",
                CardBorder = "#18362E",
                TrackBg = "#122B24",
                HoverBg = "#183D32",
                SelectedRowBg = "#14342B",
                SelectedRowBorder = "#245C4C",
                TextPrimary = "#ECFDF5",
                TextSecondary = "#A7F3D0",
                TextMuted = "#649686",
                AccentPrimary = "#10B981",
                AccentSecondary = "#2DD4BF",
                AccentOutput = "#38BDF8",
                AccentCache = "#6EE7B7",
                AccentCost = "#FCD34D",
                AccentActive = "#34D399",
                AccentWarn = "#F59E0B",
                AccentDanger = "#FB7185",
                OrbCoreTop = "#F2133029",
                OrbCoreMid = "#F40A1C18",
                OrbCoreEdge = "#F805100D",
                OrbBorder = "#1D4A3D",
                OrbRingTrack = "#122E26",
                OrbBottomPillBg = "#880F2E25",
                PillBg = "#F00A1C18",
                PillBorder = "#1F4F41"
            },
            new ThemePalette
            {
                Id = "sunset",
                Name = "落日琥珀 (Sunset Amber)",
                IsLight = false,
                WindowBg = "#F5171118",
                WindowBorder = "#4A2E3A",
                CardBg = "#221822",
                CardBorder = "#3A2735",
                TrackBg = "#2E1E2B",
                HoverBg = "#402938",
                SelectedRowBg = "#352130",
                SelectedRowBorder = "#6B3D52",
                TextPrimary = "#FFF7ED",
                TextSecondary = "#FED7AA",
                TextMuted = "#A88B99",
                AccentPrimary = "#F59E0B",
                AccentSecondary = "#FB7185",
                AccentOutput = "#F472B6",
                AccentCache = "#34D399",
                AccentCost = "#FDE047",
                AccentActive = "#FB923C",
                AccentWarn = "#F59E0B",
                AccentDanger = "#F43F5E",
                OrbCoreTop = "#F233202A",
                OrbCoreMid = "#F41E131B",
                OrbCoreEdge = "#F8110A10",
                OrbBorder = "#523240",
                OrbRingTrack = "#33202B",
                OrbBottomPillBg = "#88331E28",
                PillBg = "#F01E131B",
                PillBorder = "#573545"
            },
            new ThemePalette
            {
                Id = "frost",
                Name = "浅色磨砂 (Frost Light)",
                IsLight = true,
                WindowBg = "#F6F4F7FC",
                WindowBorder = "#CBD5E1",
                CardBg = "#FFFFFF",
                CardBorder = "#E2E8F0",
                TrackBg = "#E2E8F0",
                HoverBg = "#E0F2FE",
                SelectedRowBg = "#EFF6FF",
                SelectedRowBorder = "#93C5FD",
                TextPrimary = "#0F172A",
                TextSecondary = "#334155",
                TextMuted = "#64748B",
                AccentPrimary = "#0284C7",
                AccentSecondary = "#6366F1",
                AccentOutput = "#DB2777",
                AccentCache = "#059669",
                AccentCost = "#D97706",
                AccentActive = "#10B981",
                AccentWarn = "#D97706",
                AccentDanger = "#E11D48",
                OrbCoreTop = "#FAFFFFFF",
                OrbCoreMid = "#F6F1F5F9",
                OrbCoreEdge = "#F6E2E8F0",
                OrbBorder = "#CBD5E1",
                OrbRingTrack = "#E2E8F0",
                OrbBottomPillBg = "#DDE0F2FE",
                PillBg = "#F6FFFFFF",
                PillBorder = "#CBD5E1"
            }
        };

        public static ThemePalette GetTheme(string id)
        {
            if (!string.IsNullOrEmpty(id))
            {
                foreach (var t in AllThemes)
                {
                    if (string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase))
                        return t;
                }
            }
            return AllThemes[0];
        }
    }
}

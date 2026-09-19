using System;
using UnityEngine;

namespace MainGame.UI.CinematicEffects
{
    public enum CinematicThemePreset
    {
        CyberCyan = 0,
        VillainCrimson = 1,
        ElectricPurple = 2,
        PlasmaBlue = 3,
        ToxicGreen = 4,
        GoldenPower = 5,
        OrangeEnergy = 6,
        Magenta = 7,
        Ice = 8,
        RetroArcade = 9,
        Industrial = 10,
        NightTech = 11,
        Custom = 100
    }

    /// <summary>
    /// ScriptableObject defining a full cinematic pixel-art color theme.
    /// Provides consistent, cohesive palettes for outlines, inner rims, glows, energy, and UI accents.
    /// </summary>
    [CreateAssetMenu(fileName = "CinematicTheme", menuName = "UI/Cinematic Color Theme", order = 210)]
    public class CinematicUIColorTheme : ScriptableObject
    {
        [Header("Theme Identity")]
        [SerializeField] private string m_ThemeName = "New Theme";
        [SerializeField] private CinematicThemePreset m_Preset = CinematicThemePreset.Custom;

        [Header("Primary Outline & Silhouette")]
        [Tooltip("Primary silhouette outline color (#26E0FF for Cyber, #FF2A47 for Villain).")]
        [SerializeField] private Color m_OutlineColor = new Color(0.149f, 0.878f, 1.0f, 1.0f);

        [Tooltip("Secondary silhouette shimmer / gradient accent.")]
        [SerializeField] private Color m_OutlineSecondaryColor = new Color(0.220f, 0.886f, 1.0f, 1.0f);

        [Header("Inner Rim & Bevel Contour")]
        [Tooltip("Inner edge highlight color illuminating character silhouette seams, robot panels, and sign bevels.")]
        [SerializeField] private Color m_InnerRimColor = new Color(1.0f, 0.878f, 0.349f, 1.0f);

        [Header("Perimeter Glow & Energy")]
        [Tooltip("Perimeter bloom / electric glow tint.")]
        [SerializeField] private Color m_GlowColor = new Color(0.149f, 0.878f, 1.0f, 1.0f);

        [Tooltip("Active energy conduit, spark, and scanning beam tint.")]
        [SerializeField] private Color m_EnergyColor = new Color(0.0f, 0.784f, 1.0f, 1.0f);

        [Tooltip("Ambient particles, data floats, and dust motes.")]
        [SerializeField] private Color m_ParticleColor = new Color(0.35f, 0.90f, 1.0f, 0.85f);

        [Header("Atmosphere & Modulation")]
        [Tooltip("Breathing brightness modulation tint.")]
        [SerializeField] private Color m_PulseColor = new Color(0.25f, 0.85f, 1.0f, 1.0f);

        [Tooltip("Secondary UI accent / badge highlight tint.")]
        [SerializeField] private Color m_SecondaryAccentColor = new Color(1.0f, 0.88f, 0.35f, 1.0f);

        [Tooltip("Warning, danger, or exit button reaction tint.")]
        [SerializeField] private Color m_WarningColor = new Color(1.0f, 0.165f, 0.28f, 1.0f);

        [Tooltip("Ambient backdrop environmental lighting tint.")]
        [SerializeField] private Color m_BackgroundAccentColor = new Color(0.08f, 0.12f, 0.22f, 1.0f);

        [Header("Special FX Accents")]
        [Tooltip("Glitch digital slice accent tint.")]
        [SerializeField] private Color m_GlitchColor = new Color(0.2f, 0.9f, 1.0f, 1.0f);

        [Tooltip("High-specular pixel glint tint.")]
        [SerializeField] private Color m_HighlightColor = new Color(1.0f, 1.0f, 1.0f, 1.0f);

        [Tooltip("Deep shadow perimeter accent tint.")]
        [SerializeField] private Color m_ShadowAccentColor = new Color(0.05f, 0.05f, 0.12f, 1.0f);

        // Properties
        public string ThemeName { get => m_ThemeName; set => m_ThemeName = value; }
        public CinematicThemePreset Preset { get => m_Preset; set => m_Preset = value; }

        public Color OutlineColor { get => m_OutlineColor; set => m_OutlineColor = value; }
        public Color OutlineSecondaryColor { get => m_OutlineSecondaryColor; set => m_OutlineSecondaryColor = value; }
        public Color InnerRimColor { get => m_InnerRimColor; set => m_InnerRimColor = value; }
        public Color GlowColor { get => m_GlowColor; set => m_GlowColor = value; }
        public Color EnergyColor { get => m_EnergyColor; set => m_EnergyColor = value; }
        public Color ParticleColor { get => m_ParticleColor; set => m_ParticleColor = value; }
        public Color PulseColor { get => m_PulseColor; set => m_PulseColor = value; }
        public Color SecondaryAccentColor { get => m_SecondaryAccentColor; set => m_SecondaryAccentColor = value; }
        public Color WarningColor { get => m_WarningColor; set => m_WarningColor = value; }
        public Color BackgroundAccentColor { get => m_BackgroundAccentColor; set => m_BackgroundAccentColor = value; }
        public Color GlitchColor { get => m_GlitchColor; set => m_GlitchColor = value; }
        public Color HighlightColor { get => m_HighlightColor; set => m_HighlightColor = value; }
        public Color ShadowAccentColor { get => m_ShadowAccentColor; set => m_ShadowAccentColor = value; }

        /// <summary>
        /// Populates this theme's colors based on one of the 12 built-in pixel-art presets.
        /// </summary>
        public void ApplyPreset(CinematicThemePreset preset)
        {
            m_Preset = preset;
            switch (preset)
            {
                case CinematicThemePreset.CyberCyan:
                    m_ThemeName = "Cyber Cyan";
                    m_OutlineColor = HexToColor("#26E0FF");
                    m_OutlineSecondaryColor = HexToColor("#38E2FF");
                    m_InnerRimColor = HexToColor("#FFE059");
                    m_GlowColor = HexToColor("#26E0FF");
                    m_EnergyColor = HexToColor("#00C8FF");
                    m_ParticleColor = HexToColor("#38E2FF");
                    m_PulseColor = HexToColor("#26E0FF");
                    m_SecondaryAccentColor = HexToColor("#FFE059");
                    m_WarningColor = HexToColor("#FF2A47");
                    m_BackgroundAccentColor = HexToColor("#0A1C28");
                    m_GlitchColor = HexToColor("#38E2FF");
                    m_HighlightColor = HexToColor("#E6FCFF");
                    m_ShadowAccentColor = HexToColor("#041018");
                    break;

                case CinematicThemePreset.VillainCrimson:
                    m_ThemeName = "Villain Crimson";
                    m_OutlineColor = HexToColor("#FF2A47");
                    m_OutlineSecondaryColor = HexToColor("#FF5368");
                    m_InnerRimColor = HexToColor("#FFB347");
                    m_GlowColor = HexToColor("#FF2A47");
                    m_EnergyColor = HexToColor("#FF1744");
                    m_ParticleColor = HexToColor("#FF5368");
                    m_PulseColor = HexToColor("#FF2A47");
                    m_SecondaryAccentColor = HexToColor("#FFB347");
                    m_WarningColor = HexToColor("#FF1744");
                    m_BackgroundAccentColor = HexToColor("#28060B");
                    m_GlitchColor = HexToColor("#FF5368");
                    m_HighlightColor = HexToColor("#FFE8EB");
                    m_ShadowAccentColor = HexToColor("#140205");
                    break;

                case CinematicThemePreset.ElectricPurple:
                    m_ThemeName = "Electric Purple";
                    m_OutlineColor = HexToColor("#B45CFF");
                    m_OutlineSecondaryColor = HexToColor("#E080FF");
                    m_InnerRimColor = HexToColor("#FFE066");
                    m_GlowColor = HexToColor("#9D4EDD");
                    m_EnergyColor = HexToColor("#C77DFF");
                    m_ParticleColor = HexToColor("#E080FF");
                    m_PulseColor = HexToColor("#B45CFF");
                    m_SecondaryAccentColor = HexToColor("#FFE066");
                    m_WarningColor = HexToColor("#FF2A47");
                    m_BackgroundAccentColor = HexToColor("#1E0A2D");
                    m_GlitchColor = HexToColor("#E080FF");
                    m_HighlightColor = HexToColor("#F7EBFF");
                    m_ShadowAccentColor = HexToColor("#100419");
                    break;

                case CinematicThemePreset.PlasmaBlue:
                    m_ThemeName = "Plasma Blue";
                    m_OutlineColor = HexToColor("#3AA7FF");
                    m_OutlineSecondaryColor = HexToColor("#61D4FF");
                    m_InnerRimColor = HexToColor("#B8F1FF");
                    m_GlowColor = HexToColor("#168CFF");
                    m_EnergyColor = HexToColor("#00B8FF");
                    m_ParticleColor = HexToColor("#61D4FF");
                    m_PulseColor = HexToColor("#3AA7FF");
                    m_SecondaryAccentColor = HexToColor("#B8F1FF");
                    m_WarningColor = HexToColor("#FF2A47");
                    m_BackgroundAccentColor = HexToColor("#071529");
                    m_GlitchColor = HexToColor("#61D4FF");
                    m_HighlightColor = HexToColor("#ECF8FF");
                    m_ShadowAccentColor = HexToColor("#030A14");
                    break;

                case CinematicThemePreset.ToxicGreen:
                    m_ThemeName = "Toxic Green";
                    m_OutlineColor = HexToColor("#63FF4F");
                    m_OutlineSecondaryColor = HexToColor("#A6FF75");
                    m_InnerRimColor = HexToColor("#E8FF7A");
                    m_GlowColor = HexToColor("#32FF32");
                    m_EnergyColor = HexToColor("#00E676");
                    m_ParticleColor = HexToColor("#A6FF75");
                    m_PulseColor = HexToColor("#63FF4F");
                    m_SecondaryAccentColor = HexToColor("#E8FF7A");
                    m_WarningColor = HexToColor("#FF2A47");
                    m_BackgroundAccentColor = HexToColor("#0B2308");
                    m_GlitchColor = HexToColor("#A6FF75");
                    m_HighlightColor = HexToColor("#F0FFE8");
                    m_ShadowAccentColor = HexToColor("#051203");
                    break;

                case CinematicThemePreset.GoldenPower:
                    m_ThemeName = "Golden Power";
                    m_OutlineColor = HexToColor("#FFD23F");
                    m_OutlineSecondaryColor = HexToColor("#FFE66D");
                    m_InnerRimColor = HexToColor("#FFF4B0");
                    m_GlowColor = HexToColor("#FFB703");
                    m_EnergyColor = HexToColor("#FFCA28");
                    m_ParticleColor = HexToColor("#FFE66D");
                    m_PulseColor = HexToColor("#FFD23F");
                    m_SecondaryAccentColor = HexToColor("#FFF4B0");
                    m_WarningColor = HexToColor("#FF2A47");
                    m_BackgroundAccentColor = HexToColor("#281F05");
                    m_GlitchColor = HexToColor("#FFE66D");
                    m_HighlightColor = HexToColor("#FFFDE8");
                    m_ShadowAccentColor = HexToColor("#140F02");
                    break;

                case CinematicThemePreset.OrangeEnergy:
                    m_ThemeName = "Orange Energy";
                    m_OutlineColor = HexToColor("#FF7A18");
                    m_OutlineSecondaryColor = HexToColor("#FF9F43");
                    m_InnerRimColor = HexToColor("#FFE0A3");
                    m_GlowColor = HexToColor("#FF5A00");
                    m_EnergyColor = HexToColor("#FF8C00");
                    m_ParticleColor = HexToColor("#FF9F43");
                    m_PulseColor = HexToColor("#FF7A18");
                    m_SecondaryAccentColor = HexToColor("#FFE0A3");
                    m_WarningColor = HexToColor("#FF2A47");
                    m_BackgroundAccentColor = HexToColor("#281204");
                    m_GlitchColor = HexToColor("#FF9F43");
                    m_HighlightColor = HexToColor("#FFF3EB");
                    m_ShadowAccentColor = HexToColor("#140801");
                    break;

                case CinematicThemePreset.Magenta:
                    m_ThemeName = "Magenta";
                    m_OutlineColor = HexToColor("#FF3CAC");
                    m_OutlineSecondaryColor = HexToColor("#FF6BCB");
                    m_InnerRimColor = HexToColor("#FFC2E8");
                    m_GlowColor = HexToColor("#FF1493");
                    m_EnergyColor = HexToColor("#FF4FD8");
                    m_ParticleColor = HexToColor("#FF6BCB");
                    m_PulseColor = HexToColor("#FF3CAC");
                    m_SecondaryAccentColor = HexToColor("#FFC2E8");
                    m_WarningColor = HexToColor("#FF1744");
                    m_BackgroundAccentColor = HexToColor("#260619");
                    m_GlitchColor = HexToColor("#FF6BCB");
                    m_HighlightColor = HexToColor("#FFEDF8");
                    m_ShadowAccentColor = HexToColor("#13020C");
                    break;

                case CinematicThemePreset.Ice:
                    m_ThemeName = "Ice";
                    m_OutlineColor = HexToColor("#8BE9FD");
                    m_OutlineSecondaryColor = HexToColor("#C8F7FF");
                    m_InnerRimColor = HexToColor("#FFFFFF");
                    m_GlowColor = HexToColor("#61E7FF");
                    m_EnergyColor = HexToColor("#A8F0FF");
                    m_ParticleColor = HexToColor("#C8F7FF");
                    m_PulseColor = HexToColor("#8BE9FD");
                    m_SecondaryAccentColor = HexToColor("#FFFFFF");
                    m_WarningColor = HexToColor("#FF2A47");
                    m_BackgroundAccentColor = HexToColor("#0A1C24");
                    m_GlitchColor = HexToColor("#C8F7FF");
                    m_HighlightColor = HexToColor("#F5FDFF");
                    m_ShadowAccentColor = HexToColor("#040E12");
                    break;

                case CinematicThemePreset.RetroArcade:
                    m_ThemeName = "Retro Arcade";
                    m_OutlineColor = HexToColor("#00F5D4");
                    m_OutlineSecondaryColor = HexToColor("#00BBF9");
                    m_InnerRimColor = HexToColor("#FEE440");
                    m_GlowColor = HexToColor("#00F5D4");
                    m_EnergyColor = HexToColor("#9B5DE5");
                    m_ParticleColor = HexToColor("#00BBF9");
                    m_PulseColor = HexToColor("#00F5D4");
                    m_SecondaryAccentColor = HexToColor("#FEE440");
                    m_WarningColor = HexToColor("#F15BB5");
                    m_BackgroundAccentColor = HexToColor("#0D1B2A");
                    m_GlitchColor = HexToColor("#00BBF9");
                    m_HighlightColor = HexToColor("#E8FFF9");
                    m_ShadowAccentColor = HexToColor("#040D14");
                    break;

                case CinematicThemePreset.Industrial:
                    m_ThemeName = "Industrial";
                    m_OutlineColor = HexToColor("#FF5A36");
                    m_OutlineSecondaryColor = HexToColor("#FF8A5B");
                    m_InnerRimColor = HexToColor("#FFD166");
                    m_GlowColor = HexToColor("#E63946");
                    m_EnergyColor = HexToColor("#FF6B35");
                    m_ParticleColor = HexToColor("#FF8A5B");
                    m_PulseColor = HexToColor("#FF5A36");
                    m_SecondaryAccentColor = HexToColor("#FFD166");
                    m_WarningColor = HexToColor("#D90429");
                    m_BackgroundAccentColor = HexToColor("#2B0E09");
                    m_GlitchColor = HexToColor("#FF8A5B");
                    m_HighlightColor = HexToColor("#FFF0EB");
                    m_ShadowAccentColor = HexToColor("#140603");
                    break;

                case CinematicThemePreset.NightTech:
                    m_ThemeName = "Night Tech";
                    m_OutlineColor = HexToColor("#4CC9F0");
                    m_OutlineSecondaryColor = HexToColor("#4895EF");
                    m_InnerRimColor = HexToColor("#F72585");
                    m_GlowColor = HexToColor("#4361EE");
                    m_EnergyColor = HexToColor("#3A86FF");
                    m_ParticleColor = HexToColor("#4895EF");
                    m_PulseColor = HexToColor("#4CC9F0");
                    m_SecondaryAccentColor = HexToColor("#F72585");
                    m_WarningColor = HexToColor("#F72585");
                    m_BackgroundAccentColor = HexToColor("#090C28");
                    m_GlitchColor = HexToColor("#4895EF");
                    m_HighlightColor = HexToColor("#EFF8FF");
                    m_ShadowAccentColor = HexToColor("#030514");
                    break;

                default:
                    break;
            }
        }

        /// <summary>
        /// Creates a runtime instance of any preset without requiring asset files on disk.
        /// </summary>
        public static CinematicUIColorTheme CreateRuntimePreset(CinematicThemePreset preset)
        {
            CinematicUIColorTheme theme = CreateInstance<CinematicUIColorTheme>();
            theme.ApplyPreset(preset);
            return theme;
        }

        public static Color HexToColor(string hex)
        {
            if (ColorUtility.TryParseHtmlString(hex, out Color col))
            {
                return col;
            }
            return Color.white;
        }
    }
}

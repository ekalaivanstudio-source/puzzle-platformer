using System;
using UnityEngine;
using MainGame.UI.Feedback;

namespace MainGame.UI.CinematicEffects
{
    public enum ScreenFXType
    {
        None = 0,
        Home_EnergyCore = 1,
        Level_RouteNetwork = 2,
        Collection_RobotScan = 3,
        Settings_SignalPrecision = 4,
        Credits_DataTransmission = 5,
        Exit_WarningImpact = 6
    }

    /// <summary>
    /// Screen-specific visual identity profile configuration.
    /// Each screen has a strictly differentiated theme, palette, duration, shader primary behavior,
    /// particle preset, and sound cue to eliminate the "every screen looks identical" visual problem.
    /// </summary>
    [Serializable]
    public class CinematicScreenFXProfile
    {
        public ScreenFXType ScreenType = ScreenFXType.None;
        public string ProfileName = "Default";

        [Header("Color Palette")]
        public Color PrimaryEnergyColor = Color.white;
        public Color SecondaryAccentColor = Color.cyan;
        public Color GlowColor = new Color(0.35f, 0.85f, 1.0f, 0.7f);

        [Header("Timing")]
        public float EntranceDuration = 0.32f;
        public float ExitDuration = 0.24f;

        [Header("Shader Behaviors")]
        public float BorderIntensity = 1.8f;
        public UIBorderDirection BorderDirection = UIBorderDirection.PerimeterClockwise;
        public float ShockwaveStrength = 0.05f;
        public float GlitchIntensity = 0.5f;

        [Header("Particle Tuning")]
        public UIParticleType ParticleType = UIParticleType.ElectricalSpark;
        public int ParticleBurstCount = 6;
        public float ParticleSpreadRadius = 24f;

        [Header("Audio Signatures")]
        public bool PlayEntranceSfx = true;
        public UISfxType EntranceSfx = UISfxType.Deploy;
        public bool PlayExitSfx = true;
        public UISfxType ExitSfx = UISfxType.Retract;
        public float SfxVolume = 0.8f;

        /// <summary>
        /// Factory providing typed presets with carefully differentiated values for each screen.
        /// </summary>
        public static CinematicScreenFXProfile GetProfile(ScreenFXType type)
        {
            var p = new CinematicScreenFXProfile
            {
                ScreenType = type
            };

            switch (type)
            {
                case ScreenFXType.Home_EnergyCore:
                    p.ProfileName = "Home // Energy Core";
                    p.PrimaryEnergyColor = new Color(0.35f, 0.85f, 1.0f, 1.0f);   // Electric cyan
                    p.SecondaryAccentColor = new Color(1.0f, 0.92f, 0.35f, 1.0f); // Core gold
                    p.GlowColor = new Color(0.30f, 0.75f, 1.0f, 0.85f);
                    p.EntranceDuration = 0.38f;
                    p.ExitDuration = 0.28f;
                    p.BorderIntensity = 2.2f;
                    p.BorderDirection = UIBorderDirection.PerimeterClockwise;
                    p.ShockwaveStrength = 0.07f;
                    p.GlitchIntensity = 0.4f;
                    p.ParticleType = UIParticleType.ElectricalSpark;
                    p.ParticleBurstCount = 8;
                    p.ParticleSpreadRadius = 28f;
                    p.EntranceSfx = UISfxType.RobotBoot;
                    p.ExitSfx = UISfxType.Retract;
                    p.SfxVolume = 0.85f;
                    break;

                case ScreenFXType.Level_RouteNetwork:
                    p.ProfileName = "Level Selection // Route Network";
                    p.PrimaryEnergyColor = new Color(1.0f, 0.82f, 0.22f, 1.0f);   // Amber route line
                    p.SecondaryAccentColor = new Color(0.35f, 0.90f, 1.0f, 1.0f); // Cyan node ring
                    p.GlowColor = new Color(1.0f, 0.75f, 0.15f, 0.75f);
                    p.EntranceDuration = 0.36f;
                    p.ExitDuration = 0.26f;
                    p.BorderIntensity = 1.6f;
                    p.BorderDirection = UIBorderDirection.LeftToRight;
                    p.ShockwaveStrength = 0.04f;
                    p.GlitchIntensity = 0.2f;
                    p.ParticleType = UIParticleType.EnergyBubble;
                    p.ParticleBurstCount = 6;
                    p.ParticleSpreadRadius = 24f;
                    p.EntranceSfx = UISfxType.Deploy;
                    p.ExitSfx = UISfxType.Retract;
                    p.SfxVolume = 0.80f;
                    break;

                case ScreenFXType.Collection_RobotScan:
                    p.ProfileName = "Collection // Robot Scan & Analysis";
                    p.PrimaryEnergyColor = new Color(0.35f, 0.88f, 1.0f, 0.95f);  // Holographic scan cyan
                    p.SecondaryAccentColor = new Color(0.35f, 1.0f, 0.65f, 1.0f); // Diagnostic green
                    p.GlowColor = new Color(0.25f, 0.82f, 1.0f, 0.75f);
                    p.EntranceDuration = 0.30f;
                    p.ExitDuration = 0.24f;
                    p.BorderIntensity = 1.9f;
                    p.BorderDirection = UIBorderDirection.TopToBottom;
                    p.ShockwaveStrength = 0.05f;
                    p.GlitchIntensity = 0.35f;
                    p.ParticleType = UIParticleType.DataParticle;
                    p.ParticleBurstCount = 8;
                    p.ParticleSpreadRadius = 32f;
                    p.EntranceSfx = UISfxType.RobotBoot;
                    p.ExitSfx = UISfxType.Retract;
                    p.SfxVolume = 0.82f;
                    break;

                case ScreenFXType.Settings_SignalPrecision:
                    p.ProfileName = "Settings // Signal Precision";
                    p.PrimaryEnergyColor = new Color(0.42f, 0.70f, 0.90f, 0.85f); // Crisp steel cyan
                    p.SecondaryAccentColor = new Color(0.95f, 0.98f, 1.0f, 0.90f);// Precision tick white
                    p.GlowColor = new Color(0.35f, 0.65f, 0.85f, 0.50f);
                    p.EntranceDuration = 0.28f;
                    p.ExitDuration = 0.22f;
                    p.BorderIntensity = 1.3f;
                    p.BorderDirection = UIBorderDirection.LeftToRight;
                    p.ShockwaveStrength = 0.02f;
                    p.GlitchIntensity = 0.15f;
                    p.ParticleType = UIParticleType.EnergyDot;
                    p.ParticleBurstCount = 4;
                    p.ParticleSpreadRadius = 18f;
                    p.EntranceSfx = UISfxType.SliderTick;
                    p.ExitSfx = UISfxType.Back;
                    p.SfxVolume = 0.70f;
                    break;

                case ScreenFXType.Credits_DataTransmission:
                    p.ProfileName = "Credits // Data Transmission";
                    p.PrimaryEnergyColor = new Color(0.30f, 0.95f, 0.60f, 0.95f); // Matrix data green
                    p.SecondaryAccentColor = new Color(1.0f, 0.32f, 0.68f, 0.90f);// Glitch magenta
                    p.GlowColor = new Color(0.25f, 0.90f, 0.55f, 0.65f);
                    p.EntranceDuration = 0.45f;
                    p.ExitDuration = 0.30f;
                    p.BorderIntensity = 1.5f;
                    p.BorderDirection = UIBorderDirection.PerimeterCounterClockwise;
                    p.ShockwaveStrength = 0.05f;
                    p.GlitchIntensity = 0.75f;
                    p.ParticleType = UIParticleType.PixelFragment;
                    p.ParticleBurstCount = 12;
                    p.ParticleSpreadRadius = 34f;
                    p.EntranceSfx = UISfxType.Deploy;
                    p.ExitSfx = UISfxType.Retract;
                    p.SfxVolume = 0.75f;
                    break;

                case ScreenFXType.Exit_WarningImpact:
                    p.ProfileName = "Exit // Warning & Impact";
                    p.PrimaryEnergyColor = new Color(1.0f, 0.28f, 0.20f, 1.0f);   // Warning red
                    p.SecondaryAccentColor = new Color(1.0f, 0.65f, 0.15f, 1.0f); // Hazard orange
                    p.GlowColor = new Color(1.0f, 0.22f, 0.12f, 0.88f);
                    p.EntranceDuration = 0.26f;
                    p.ExitDuration = 0.20f;
                    p.BorderIntensity = 2.5f;
                    p.BorderDirection = UIBorderDirection.TopToBottom;
                    p.ShockwaveStrength = 0.12f;
                    p.GlitchIntensity = 0.60f;
                    p.ParticleType = UIParticleType.ElectricalSpark;
                    p.ParticleBurstCount = 12;
                    p.ParticleSpreadRadius = 36f;
                    p.EntranceSfx = UISfxType.PopupSlam;
                    p.ExitSfx = UISfxType.Impact;
                    p.SfxVolume = 0.95f;
                    break;

                default:
                    p.ProfileName = "Standard Panel";
                    p.PrimaryEnergyColor = new Color(0.35f, 0.85f, 1.0f, 1.0f);
                    p.SecondaryAccentColor = Color.white;
                    p.GlowColor = new Color(0.35f, 0.85f, 1.0f, 0.6f);
                    p.EntranceDuration = 0.30f;
                    p.ExitDuration = 0.25f;
                    p.BorderIntensity = 1.5f;
                    p.BorderDirection = UIBorderDirection.PerimeterClockwise;
                    p.ShockwaveStrength = 0.05f;
                    p.GlitchIntensity = 0.3f;
                    p.ParticleType = UIParticleType.ElectricalSpark;
                    p.ParticleBurstCount = 6;
                    p.ParticleSpreadRadius = 24f;
                    p.EntranceSfx = UISfxType.Deploy;
                    p.ExitSfx = UISfxType.Retract;
                    p.SfxVolume = 0.80f;
                    break;
            }

            return p;
        }
    }
}

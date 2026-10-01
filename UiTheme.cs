using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace VRChatInstanceLogger
{
    public enum ThemePreset
    {
        GreenAero,
        BlueAero,
        LavenderAero,
        DarkGlass,
        MinimalWhite,
        OrangeAero,
        BlackTheme,
        RedTheme,
        YellowAero,
        TealAero,
        PinkAero,
        CyanAero
    }

    internal sealed class ThemePalette
    {
        public ThemePalette(
            Color background,
            Color surface,
            Color accent,
            Color accentHover,
            Color accentDown,
            Color textPrimary,
            Color textMuted,
            Color border,
            Color borderLight,
            Color skyTop,
            Color skyMid,
            Color skyBottom,
            Color logJoin,
            Color logLeave,
            Color logDanger,
            Color logFloodWarning,
            Color logGroupCheck,
            Color logGroupMatch,
            Color logRestricted,
            Color logInstance,
            Color logAuth)
        {
            Background = background;
            Surface = surface;
            Accent = accent;
            AccentHover = accentHover;
            AccentDown = accentDown;
            TextPrimary = textPrimary;
            TextMuted = textMuted;
            Border = border;
            BorderLight = borderLight;
            SkyTop = skyTop;
            SkyMid = skyMid;
            SkyBottom = skyBottom;
            LogJoin = logJoin;
            LogLeave = logLeave;
            LogDanger = logDanger;
            LogFloodWarning = logFloodWarning;
            LogGroupCheck = logGroupCheck;
            LogGroupMatch = logGroupMatch;
            LogRestricted = logRestricted;
            LogInstance = logInstance;
            LogAuth = logAuth;
        }

        public Color Background { get; }
        public Color Surface { get; }
        public Color Accent { get; }
        public Color AccentHover { get; }
        public Color AccentDown { get; }
        public Color TextPrimary { get; }
        public Color TextMuted { get; }
        public Color Border { get; }
        public Color BorderLight { get; }
        public Color SkyTop { get; }
        public Color SkyMid { get; }
        public Color SkyBottom { get; }
        public Color LogJoin { get; }
        public Color LogLeave { get; }
        public Color LogDanger { get; }
        public Color LogFloodWarning { get; }
        public Color LogGroupCheck { get; }
        public Color LogGroupMatch { get; }
        public Color LogRestricted { get; }
        public Color LogInstance { get; }
        public Color LogAuth { get; }
    }

    /// <summary>
    /// A Frutiger Aero theme system with multiple preset palettes. Applied at runtime so the
    /// generated Designer files remain untouched.
    /// </summary>
    internal static class UiTheme
    {
        public static ThemePreset CurrentPreset { get; private set; } = ThemePreset.GreenAero;

        private static readonly Dictionary<ThemePreset, ThemePalette> Presets = new Dictionary<ThemePreset, ThemePalette>
        {
            [ThemePreset.GreenAero] = new ThemePalette(
                Color.FromArgb(232, 247, 238),
                Color.FromArgb(252, 255, 253),
                Color.FromArgb(35, 160, 99),
                Color.FromArgb(73, 196, 128),
                Color.FromArgb(22, 119, 73),
                Color.FromArgb(14, 49, 38),
                Color.FromArgb(82, 112, 99),
                Color.FromArgb(173, 214, 186),
                Color.FromArgb(121, 203, 158),
                Color.FromArgb(175, 234, 208),
                Color.FromArgb(230, 247, 236),
                Color.FromArgb(255, 255, 255),
                Color.FromArgb(17, 149, 89),
                Color.FromArgb(196, 121, 55),
                Color.FromArgb(136, 53, 42),
                Color.FromArgb(170, 74, 48),
                Color.FromArgb(106, 82, 166),
                Color.FromArgb(182, 75, 60),
                Color.FromArgb(42, 123, 91),
                Color.FromArgb(17, 122, 86),
                Color.FromArgb(24, 140, 105)),
            [ThemePreset.BlueAero] = new ThemePalette(
                Color.FromArgb(226, 240, 253),
                Color.FromArgb(255, 255, 255),
                Color.FromArgb(23, 121, 200),
                Color.FromArgb(58, 160, 240),
                Color.FromArgb(19, 95, 168),
                Color.FromArgb(18, 50, 79),
                Color.FromArgb(92, 116, 136),
                Color.FromArgb(169, 199, 224),
                Color.FromArgb(120, 170, 214),
                Color.FromArgb(190, 224, 255),
                Color.FromArgb(228, 242, 255),
                Color.FromArgb(255, 255, 255),
                Color.FromArgb(31, 157, 87),
                Color.FromArgb(199, 122, 22),
                Color.FromArgb(128, 0, 0),
                Color.FromArgb(160, 40, 40),
                Color.FromArgb(122, 63, 191),
                Color.FromArgb(192, 69, 58),
                Color.FromArgb(46, 111, 176),
                Color.FromArgb(20, 119, 194),
                Color.FromArgb(20, 138, 150)),
            [ThemePreset.LavenderAero] = new ThemePalette(
                Color.FromArgb(238, 235, 248),
                Color.FromArgb(255, 255, 255),
                Color.FromArgb(119, 97, 186),
                Color.FromArgb(157, 131, 228),
                Color.FromArgb(86, 68, 150),
                Color.FromArgb(38, 31, 68),
                Color.FromArgb(94, 90, 119),
                Color.FromArgb(202, 191, 231),
                Color.FromArgb(154, 136, 213),
                Color.FromArgb(207, 198, 248),
                Color.FromArgb(239, 235, 250),
                Color.FromArgb(255, 255, 255),
                Color.FromArgb(82, 112, 185),
                Color.FromArgb(170, 129, 54),
                Color.FromArgb(135, 60, 80),
                Color.FromArgb(170, 93, 109),
                Color.FromArgb(119, 96, 176),
                Color.FromArgb(177, 88, 96),
                Color.FromArgb(82, 94, 161),
                Color.FromArgb(88, 103, 186),
                Color.FromArgb(89, 134, 181)),
            [ThemePreset.DarkGlass] = new ThemePalette(
                Color.FromArgb(18, 25, 34),
                Color.FromArgb(31, 38, 46),
                Color.FromArgb(74, 196, 160),
                Color.FromArgb(104, 220, 186),
                Color.FromArgb(35, 120, 104),
                Color.FromArgb(231, 242, 245),
                Color.FromArgb(160, 180, 190),
                Color.FromArgb(67, 90, 102),
                Color.FromArgb(90, 120, 136),
                Color.FromArgb(28, 39, 53),
                Color.FromArgb(42, 54, 70),
                Color.FromArgb(18, 25, 34),
                Color.FromArgb(40, 210, 130),
                Color.FromArgb(201, 142, 78),
                Color.FromArgb(204, 101, 88),
                Color.FromArgb(205, 118, 97),
                Color.FromArgb(161, 137, 243),
                Color.FromArgb(174, 101, 111),
                Color.FromArgb(73, 172, 137),
                Color.FromArgb(63, 160, 130),
                Color.FromArgb(74, 181, 154)),
            [ThemePreset.MinimalWhite] = new ThemePalette(
                Color.FromArgb(244, 246, 248),
                Color.FromArgb(255, 255, 255),
                Color.FromArgb(34, 93, 108),
                Color.FromArgb(78, 126, 145),
                Color.FromArgb(17, 65, 77),
                Color.FromArgb(24, 33, 40),
                Color.FromArgb(100, 110, 117),
                Color.FromArgb(210, 216, 221),
                Color.FromArgb(164, 177, 188),
                Color.FromArgb(230, 236, 239),
                Color.FromArgb(246, 249, 250),
                Color.FromArgb(255, 255, 255),
                Color.FromArgb(26, 140, 122),
                Color.FromArgb(167, 113, 42),
                Color.FromArgb(142, 58, 58),
                Color.FromArgb(166, 76, 60),
                Color.FromArgb(101, 87, 147),
                Color.FromArgb(158, 77, 72),
                Color.FromArgb(39, 119, 110),
                Color.FromArgb(33, 95, 99),
                Color.FromArgb(39, 115, 123)),
            [ThemePreset.OrangeAero] = new ThemePalette(
                Color.FromArgb(255, 240, 226),
                Color.FromArgb(255, 253, 250),
                Color.FromArgb(224, 117, 32),
                Color.FromArgb(245, 151, 66),
                Color.FromArgb(176, 77, 14),
                Color.FromArgb(74, 38, 18),
                Color.FromArgb(130, 96, 70),
                Color.FromArgb(235, 183, 132),
                Color.FromArgb(249, 205, 160),
                Color.FromArgb(255, 214, 166),
                Color.FromArgb(255, 238, 218),
                Color.FromArgb(255, 255, 255),
                Color.FromArgb(25, 151, 86),
                Color.FromArgb(194, 119, 38),
                Color.FromArgb(153, 65, 30),
                Color.FromArgb(187, 79, 38),
                Color.FromArgb(128, 78, 150),
                Color.FromArgb(177, 82, 54),
                Color.FromArgb(188, 103, 47),
                Color.FromArgb(188, 91, 24),
                Color.FromArgb(191, 101, 29)),
            [ThemePreset.BlackTheme] = new ThemePalette(
                Color.FromArgb(14, 16, 19),
                Color.FromArgb(24, 27, 31),
                Color.FromArgb(76, 82, 92),
                Color.FromArgb(112, 120, 132),
                Color.FromArgb(42, 47, 54),
                Color.White,
                Color.FromArgb(210, 216, 224),
                Color.FromArgb(58, 64, 73),
                Color.FromArgb(83, 91, 103),
                Color.FromArgb(25, 29, 35),
                Color.FromArgb(34, 39, 47),
                Color.FromArgb(12, 14, 17),
                Color.FromArgb(49, 185, 126),
                Color.FromArgb(201, 142, 78),
                Color.FromArgb(204, 101, 88),
                Color.FromArgb(205, 118, 97),
                Color.FromArgb(161, 137, 243),
                Color.FromArgb(174, 101, 111),
                Color.FromArgb(73, 172, 137),
                Color.FromArgb(63, 160, 130),
                Color.FromArgb(74, 181, 154)),
            [ThemePreset.RedTheme] = new ThemePalette(
                Color.FromArgb(255, 235, 235),
                Color.FromArgb(255, 252, 252),
                Color.FromArgb(207, 48, 55),
                Color.FromArgb(239, 88, 94),
                Color.FromArgb(151, 25, 31),
                Color.FromArgb(72, 22, 25),
                Color.FromArgb(132, 84, 87),
                Color.FromArgb(235, 166, 170),
                Color.FromArgb(248, 199, 202),
                Color.FromArgb(255, 190, 194),
                Color.FromArgb(255, 232, 233),
                Color.FromArgb(255, 255, 255),
                Color.FromArgb(20, 149, 88),
                Color.FromArgb(194, 121, 55),
                Color.FromArgb(154, 44, 42),
                Color.FromArgb(185, 61, 60),
                Color.FromArgb(106, 82, 166),
                Color.FromArgb(182, 75, 60),
                Color.FromArgb(42, 123, 91),
                Color.FromArgb(17, 122, 86),
                Color.FromArgb(24, 140, 105)),
            [ThemePreset.YellowAero] = new ThemePalette(
                Color.FromArgb(255, 249, 218),
                Color.FromArgb(255, 255, 248),
                Color.FromArgb(218, 157, 24),
                Color.FromArgb(242, 190, 56),
                Color.FromArgb(168, 111, 10),
                Color.FromArgb(70, 52, 15),
                Color.FromArgb(128, 108, 65),
                Color.FromArgb(235, 211, 139),
                Color.FromArgb(248, 226, 166),
                Color.FromArgb(255, 226, 142),
                Color.FromArgb(255, 246, 213),
                Color.FromArgb(255, 255, 255),
                Color.FromArgb(30, 148, 90),
                Color.FromArgb(190, 123, 34),
                Color.FromArgb(155, 67, 35),
                Color.FromArgb(185, 86, 43),
                Color.FromArgb(119, 84, 164),
                Color.FromArgb(178, 85, 58),
                Color.FromArgb(42, 126, 101),
                Color.FromArgb(165, 124, 20),
                Color.FromArgb(190, 143, 27)),
            [ThemePreset.TealAero] = new ThemePalette(
                Color.FromArgb(224, 247, 244),
                Color.FromArgb(252, 255, 255),
                Color.FromArgb(21, 155, 154),
                Color.FromArgb(66, 195, 190),
                Color.FromArgb(12, 105, 108),
                Color.FromArgb(15, 59, 62),
                Color.FromArgb(77, 112, 113),
                Color.FromArgb(164, 215, 211),
                Color.FromArgb(113, 193, 188),
                Color.FromArgb(174, 235, 228),
                Color.FromArgb(228, 248, 245),
                Color.FromArgb(255, 255, 255),
                Color.FromArgb(21, 151, 94),
                Color.FromArgb(194, 126, 45),
                Color.FromArgb(140, 53, 49),
                Color.FromArgb(177, 76, 65),
                Color.FromArgb(113, 82, 173),
                Color.FromArgb(177, 79, 78),
                Color.FromArgb(35, 130, 125),
                Color.FromArgb(20, 132, 155),
                Color.FromArgb(23, 154, 151)),
            [ThemePreset.PinkAero] = new ThemePalette(
                Color.FromArgb(255, 232, 243),
                Color.FromArgb(255, 252, 254),
                Color.FromArgb(214, 73, 133),
                Color.FromArgb(238, 113, 164),
                Color.FromArgb(157, 35, 91),
                Color.FromArgb(72, 24, 49),
                Color.FromArgb(133, 84, 105),
                Color.FromArgb(236, 174, 199),
                Color.FromArgb(248, 205, 222),
                Color.FromArgb(255, 190, 218),
                Color.FromArgb(255, 231, 241),
                Color.FromArgb(255, 255, 255),
                Color.FromArgb(24, 148, 91),
                Color.FromArgb(193, 122, 53),
                Color.FromArgb(151, 44, 74),
                Color.FromArgb(184, 63, 91),
                Color.FromArgb(118, 78, 171),
                Color.FromArgb(181, 72, 104),
                Color.FromArgb(42, 123, 107),
                Color.FromArgb(190, 64, 128),
                Color.FromArgb(210, 78, 144)),
            [ThemePreset.CyanAero] = new ThemePalette(
                Color.FromArgb(225, 247, 255),
                Color.FromArgb(252, 255, 255),
                Color.FromArgb(0, 155, 188),
                Color.FromArgb(48, 194, 221),
                Color.FromArgb(0, 101, 132),
                Color.FromArgb(12, 52, 67),
                Color.FromArgb(76, 112, 124),
                Color.FromArgb(161, 214, 228),
                Color.FromArgb(105, 188, 210),
                Color.FromArgb(167, 232, 255),
                Color.FromArgb(226, 248, 255),
                Color.FromArgb(255, 255, 255),
                Color.FromArgb(22, 149, 92),
                Color.FromArgb(193, 124, 42),
                Color.FromArgb(141, 52, 48),
                Color.FromArgb(176, 74, 64),
                Color.FromArgb(113, 80, 173),
                Color.FromArgb(176, 76, 80),
                Color.FromArgb(36, 126, 139),
                Color.FromArgb(0, 124, 164),
                Color.FromArgb(0, 151, 177))
        };

        private static ThemePalette Current => Presets.TryGetValue(CurrentPreset, out var palette) ? palette : Presets[ThemePreset.GreenAero];

        public static Color Background => Current.Background;
        public static Color Surface => Current.Surface;
        public static Color Accent => Current.Accent;
        public static Color AccentHover => Current.AccentHover;
        public static Color AccentDown => Current.AccentDown;
        public static Color Disabled => Current.TextMuted;
        public static Color TextPrimary => Current.TextPrimary;
        public static Color TextMuted => Current.TextMuted;
        public static Color Border => Current.Border;
        public static Color BorderLight => Current.BorderLight;
        private static Color SkyTop => Current.SkyTop;
        private static Color SkyMid => Current.SkyMid;
        private static Color SkyBottom => Current.SkyBottom;
        public static Color LogJoin => Current.LogJoin;
        public static Color LogLeave => Current.LogLeave;
        public static Color LogDanger => Current.LogDanger;
        public static Color LogFloodWarning => Current.LogFloodWarning;
        public static Color LogGroupCheck => Current.LogGroupCheck;
        public static Color LogGroupMatch => Current.LogGroupMatch;
        public static Color LogRestricted => Current.LogRestricted;
        public static Color LogInstance => Current.LogInstance;
        public static Color LogAuth => Current.LogAuth;

        /// <summary>Applies the theme to a form and all of its controls.</summary>
        public static void Apply(Form form)
        {
            ApplyTheme(form, CurrentPreset);
        }

        public static void ApplyTheme(Form form, ThemePreset preset, string? customBackgroundPath = null)
        {
            try
            {
                CurrentPreset = preset;

                form.BackColor = Background;
                form.ForeColor = TextPrimary;
                form.BackgroundImage = null;
                form.BackgroundImageLayout = ImageLayout.None;

                SetDoubleBuffered(form);
                form.Paint -= FormBackgroundPaintHandler;
                form.Paint += FormBackgroundPaintHandler;
                form.Resize -= FormResizeHandler;
                form.Resize += FormResizeHandler;
                ThemeControls(form.Controls);
                SetWindowIcon(form);

                form.HandleCreated -= WindowHandleCreatedHandler;
                form.HandleCreated += WindowHandleCreatedHandler;
                if (form.IsHandleCreated)
                    ApplyLightTitleBar(form);
            }
            catch
            {
                CurrentPreset = ThemePreset.GreenAero;
                form.BackColor = Presets[ThemePreset.GreenAero].Background;
                form.ForeColor = Presets[ThemePreset.GreenAero].TextPrimary;
                form.BackgroundImage = null;
                form.BackgroundImageLayout = ImageLayout.None;
                try
                {
                    ThemeControls(form.Controls);
                    if (form.IsHandleCreated)
                        ApplyLightTitleBar(form);
                }
                catch
                {
                    // The app must remain stable even if one theme paint step fails.
                }
            }
        }

        private static readonly PaintEventHandler FormBackgroundPaintHandler = (s, e) =>
        {
            if (s is not Form form)
                return;

            PaintAeroBackground(e.Graphics, form.ClientRectangle);
        };

        private static readonly EventHandler FormResizeHandler = (s, e) =>
        {
            if (s is not Form form)
                return;

            form.Invalidate();
        };

        private static readonly EventHandler WindowHandleCreatedHandler = (s, e) =>
        {
            if (s is not Form form)
                return;

            ApplyLightTitleBar(form);
        };

        /// <summary>Fills an area with the sky-blue-to-white Frutiger Aero wash.</summary>
        public static void PaintAeroBackground(Graphics g, Rectangle area)
        {
            if (area.Width <= 0 || area.Height <= 0)
                return;

            using var brush = new LinearGradientBrush(
                new Rectangle(area.X, area.Y - 1, area.Width, area.Height + 2),
                SkyTop, SkyBottom, LinearGradientMode.Vertical);
            brush.InterpolationColors = new ColorBlend(3)
            {
                Colors = new[] { SkyTop, SkyMid, SkyBottom },
                Positions = new[] { 0f, 0.55f, 1f }
            };
            g.FillRectangle(brush, area);
        }

        // Uses the executable's own icon (set via <ApplicationIcon>) for the window/title bar
        // so it matches the taskbar icon without shipping a separate .ico next to the exe.
        private static void SetWindowIcon(Form form)
        {
            try
            {
                var exePath = Application.ExecutablePath;
                var icon = System.Drawing.Icon.ExtractAssociatedIcon(exePath);
                if (icon != null)
                    form.Icon = icon;
            }
            catch
            {
                // If extraction fails, keep the default icon.
            }
        }

        private static void ThemeControls(Control.ControlCollection controls)
        {
            foreach (Control ctrl in controls)
            {
                switch (ctrl)
                {
                    case Button btn:
                        StyleButton(btn);
                        break;
                    case TextBox tb:
                        tb.BorderStyle = BorderStyle.FixedSingle;
                        tb.BackColor = Surface;
                        tb.ForeColor = TextPrimary;
                        break;
                    case RichTextBox rtb:
                        rtb.BorderStyle = BorderStyle.None;
                        rtb.BackColor = Surface;
                        rtb.ForeColor = TextPrimary;
                        break;
                    case ComboBox comboBox:
                        comboBox.BackColor = Surface;
                        comboBox.ForeColor = TextPrimary;
                        break;
                    case TabPage tabPage:
                        tabPage.BackColor = Background;
                        tabPage.ForeColor = TextPrimary;
                        break;
                    case TabControl tabControl:
                        tabControl.BackColor = Background;
                        tabControl.ForeColor = TextPrimary;
                        break;
                    case CheckBox chk:
                        chk.ForeColor = TextPrimary;
                        chk.BackColor = Color.Transparent;
                        break;
                    case Label lbl:
                        lbl.BackColor = Color.Transparent;
                        lbl.ForeColor = TextPrimary;
                        break;
                    case Panel pnl:
                        pnl.BackColor = Background;
                        break;
                }

                if (ctrl.HasChildren)
                    ThemeControls(ctrl.Controls);
            }
        }

        // Per-button hover/press state for the glossy paint.
        private sealed class ButtonVisualState
        {
            public bool Hover;
            public bool Pressed;
        }

        private static readonly Dictionary<Button, ButtonVisualState> buttonStates = new();

        /// <summary>Gives a button the glossy Frutiger Aero glass look, including disabled state.</summary>
        public static void StyleButton(Button btn)
        {
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            btn.UseVisualStyleBackColor = false;
            btn.BackColor = Background;
            btn.ForeColor = Color.White;
            btn.Cursor = Cursors.Hand;
            btn.TextAlign = ContentAlignment.MiddleCenter;
            try { btn.Font = new Font("Segoe UI Semibold", 9.75f, FontStyle.Bold); } catch { /* fall back to default font */ }

            if (!buttonStates.ContainsKey(btn))
            {
                var state = new ButtonVisualState();
                buttonStates[btn] = state;
                btn.MouseEnter += (s, e) => { state.Hover = true; btn.Invalidate(); };
                btn.MouseLeave += (s, e) => { state.Hover = false; state.Pressed = false; btn.Invalidate(); };
                btn.MouseDown += (s, e) => { state.Pressed = true; btn.Invalidate(); };
                btn.MouseUp += (s, e) => { state.Pressed = false; btn.Invalidate(); };
                btn.EnabledChanged += (s, e) => btn.Invalidate();
                btn.Resize += (s, e) => { UpdateButtonRegion(btn); btn.Invalidate(); };
                btn.Paint += AeroButton_Paint;
            }

            UpdateButtonRegion(btn);
            btn.Invalidate();
            if (btn.IsHandleCreated)
                btn.Refresh();
        }

        private static void UpdateButtonRegion(Button btn)
        {
            try
            {
                int radius = Math.Max(8, Math.Min(18, btn.Height / 2));
                using var path = RoundedRect(new Rectangle(0, 0, btn.Width, btn.Height), radius);
                btn.Region = new Region(path);
            }
            catch
            {
                // Ignore region failures on older/unsupported control states.
            }
        }

        private static void AeroButton_Paint(object? sender, PaintEventArgs e)
        {
            if (sender is not Button btn)
                return;

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            // Erase the base with the parent backdrop so the rounded corners stay clean.
            g.Clear(btn.Parent?.BackColor ?? Background);

            var rect = new Rectangle(0, 0, Math.Max(1, btn.Width - 1), Math.Max(1, btn.Height - 1));
            int radius = Math.Max(4, Math.Min(14, btn.Height / 2));

            buttonStates.TryGetValue(btn, out var st);
            bool hover = st?.Hover ?? false;
            bool pressed = st?.Pressed ?? false;

            Color top, bottom, border, textColor;
            if (CurrentPreset == ThemePreset.DarkGlass)
            {
                if (!btn.Enabled)
                {
                    top = Color.FromArgb(51, 59, 69);
                    bottom = Color.FromArgb(34, 41, 50);
                    border = Color.FromArgb(74, 89, 103);
                    textColor = TextMuted;
                }
                else if (pressed)
                {
                    top = Color.FromArgb(32, 116, 98);
                    bottom = Color.FromArgb(22, 72, 66);
                    border = Color.FromArgb(91, 224, 184);
                    textColor = Color.White;
                }
                else if (hover)
                {
                    top = Color.FromArgb(75, 172, 146);
                    bottom = Color.FromArgb(37, 112, 99);
                    border = Color.FromArgb(122, 234, 198);
                    textColor = Color.White;
                }
                else
                {
                    top = Color.FromArgb(54, 92, 94);
                    bottom = Color.FromArgb(30, 62, 66);
                    border = Color.FromArgb(79, 164, 151);
                    textColor = Color.White;
                }
            }
            else if (!btn.Enabled)
            {
                top = Color.FromArgb(234, 240, 246);
                bottom = Color.FromArgb(202, 214, 225);
                border = Color.FromArgb(185, 199, 213);
                textColor = TextMuted;
            }
            else if (pressed)
            {
                top = CurrentPreset == ThemePreset.BlueAero ? Color.FromArgb(58, 160, 240) : CurrentPreset == ThemePreset.LavenderAero ? Color.FromArgb(157, 131, 228) : CurrentPreset == ThemePreset.MinimalWhite ? Color.FromArgb(165, 175, 185) : CurrentPreset == ThemePreset.OrangeAero ? Color.FromArgb(245, 151, 66) : CurrentPreset == ThemePreset.BlackTheme ? Color.FromArgb(90, 98, 112) : CurrentPreset == ThemePreset.RedTheme ? Color.FromArgb(239, 88, 94) : CurrentPreset == ThemePreset.YellowAero ? Color.FromArgb(242, 190, 56) : CurrentPreset == ThemePreset.TealAero ? Color.FromArgb(66, 195, 190) : CurrentPreset == ThemePreset.PinkAero ? Color.FromArgb(238, 113, 164) : CurrentPreset == ThemePreset.CyanAero ? Color.FromArgb(48, 194, 221) : AccentDown;
                bottom = CurrentPreset == ThemePreset.BlueAero ? Color.FromArgb(19, 95, 168) : CurrentPreset == ThemePreset.LavenderAero ? Color.FromArgb(86, 68, 150) : CurrentPreset == ThemePreset.MinimalWhite ? Color.FromArgb(105, 115, 125) : CurrentPreset == ThemePreset.OrangeAero ? Color.FromArgb(176, 77, 14) : CurrentPreset == ThemePreset.BlackTheme ? Color.FromArgb(42, 47, 54) : CurrentPreset == ThemePreset.RedTheme ? Color.FromArgb(151, 25, 31) : CurrentPreset == ThemePreset.YellowAero ? Color.FromArgb(168, 111, 10) : CurrentPreset == ThemePreset.TealAero ? Color.FromArgb(12, 105, 108) : CurrentPreset == ThemePreset.PinkAero ? Color.FromArgb(157, 35, 91) : CurrentPreset == ThemePreset.CyanAero ? Color.FromArgb(0, 101, 132) : Color.FromArgb(15, 80, 140);
                border = CurrentPreset == ThemePreset.BlueAero ? Color.FromArgb(14, 76, 136) : CurrentPreset == ThemePreset.LavenderAero ? Color.FromArgb(72, 56, 128) : CurrentPreset == ThemePreset.MinimalWhite ? Color.FromArgb(85, 95, 105) : CurrentPreset == ThemePreset.OrangeAero ? Color.FromArgb(145, 62, 12) : CurrentPreset == ThemePreset.BlackTheme ? Color.FromArgb(112, 120, 132) : CurrentPreset == ThemePreset.RedTheme ? Color.FromArgb(125, 20, 26) : CurrentPreset == ThemePreset.YellowAero ? Color.FromArgb(143, 91, 6) : CurrentPreset == ThemePreset.TealAero ? Color.FromArgb(8, 82, 85) : CurrentPreset == ThemePreset.PinkAero ? Color.FromArgb(135, 24, 76) : CurrentPreset == ThemePreset.CyanAero ? Color.FromArgb(0, 78, 104) : Color.FromArgb(12, 70, 120);
                textColor = Color.White;
            }
            else if (hover)
            {
                top = CurrentPreset == ThemePreset.BlueAero ? Color.FromArgb(166, 225, 255) : CurrentPreset == ThemePreset.LavenderAero ? Color.FromArgb(205, 191, 255) : CurrentPreset == ThemePreset.MinimalWhite ? Color.FromArgb(245, 247, 249) : CurrentPreset == ThemePreset.OrangeAero ? Color.FromArgb(255, 205, 150) : CurrentPreset == ThemePreset.BlackTheme ? Color.FromArgb(125, 133, 146) : CurrentPreset == ThemePreset.RedTheme ? Color.FromArgb(255, 170, 174) : CurrentPreset == ThemePreset.YellowAero ? Color.FromArgb(255, 239, 151) : CurrentPreset == ThemePreset.TealAero ? Color.FromArgb(151, 239, 234) : CurrentPreset == ThemePreset.PinkAero ? Color.FromArgb(255, 185, 216) : CurrentPreset == ThemePreset.CyanAero ? Color.FromArgb(159, 242, 255) : Color.FromArgb(137, 225, 171);
                bottom = CurrentPreset == ThemePreset.BlueAero ? Color.FromArgb(58, 160, 240) : CurrentPreset == ThemePreset.LavenderAero ? Color.FromArgb(157, 131, 228) : CurrentPreset == ThemePreset.MinimalWhite ? Color.FromArgb(190, 200, 210) : CurrentPreset == ThemePreset.OrangeAero ? Color.FromArgb(245, 151, 66) : CurrentPreset == ThemePreset.BlackTheme ? Color.FromArgb(80, 88, 100) : CurrentPreset == ThemePreset.RedTheme ? Color.FromArgb(239, 88, 94) : CurrentPreset == ThemePreset.YellowAero ? Color.FromArgb(242, 190, 56) : CurrentPreset == ThemePreset.TealAero ? Color.FromArgb(66, 195, 190) : CurrentPreset == ThemePreset.PinkAero ? Color.FromArgb(238, 113, 164) : CurrentPreset == ThemePreset.CyanAero ? Color.FromArgb(48, 194, 221) : Color.FromArgb(53, 180, 105);
                border = CurrentPreset == ThemePreset.BlueAero ? Color.FromArgb(19, 95, 168) : CurrentPreset == ThemePreset.LavenderAero ? Color.FromArgb(119, 97, 186) : CurrentPreset == ThemePreset.MinimalWhite ? Color.FromArgb(140, 150, 160) : CurrentPreset == ThemePreset.OrangeAero ? Color.FromArgb(224, 117, 32) : CurrentPreset == ThemePreset.BlackTheme ? Color.FromArgb(150, 158, 172) : CurrentPreset == ThemePreset.RedTheme ? Color.FromArgb(207, 48, 55) : CurrentPreset == ThemePreset.YellowAero ? Color.FromArgb(218, 157, 24) : CurrentPreset == ThemePreset.TealAero ? Color.FromArgb(21, 155, 154) : CurrentPreset == ThemePreset.PinkAero ? Color.FromArgb(214, 73, 133) : CurrentPreset == ThemePreset.CyanAero ? Color.FromArgb(0, 125, 153) : Color.FromArgb(19, 104, 68);
                textColor = CurrentPreset == ThemePreset.MinimalWhite ? Color.FromArgb(35, 45, 55) : Color.White;
            }
            else
            {
                top = CurrentPreset == ThemePreset.BlueAero ? Color.FromArgb(112, 204, 248) : CurrentPreset == ThemePreset.LavenderAero ? Color.FromArgb(184, 166, 242) : CurrentPreset == ThemePreset.MinimalWhite ? Color.FromArgb(232, 236, 240) : CurrentPreset == ThemePreset.OrangeAero ? Color.FromArgb(255, 183, 104) : CurrentPreset == ThemePreset.BlackTheme ? Color.FromArgb(82, 90, 103) : CurrentPreset == ThemePreset.RedTheme ? Color.FromArgb(255, 125, 130) : CurrentPreset == ThemePreset.YellowAero ? Color.FromArgb(255, 224, 116) : CurrentPreset == ThemePreset.TealAero ? Color.FromArgb(105, 222, 217) : CurrentPreset == ThemePreset.PinkAero ? Color.FromArgb(255, 145, 190) : CurrentPreset == ThemePreset.CyanAero ? Color.FromArgb(91, 225, 247) : Color.FromArgb(96, 208, 137);
                bottom = CurrentPreset == ThemePreset.BlueAero ? Color.FromArgb(23, 121, 200) : CurrentPreset == ThemePreset.LavenderAero ? Color.FromArgb(119, 97, 186) : CurrentPreset == ThemePreset.MinimalWhite ? Color.FromArgb(175, 183, 191) : CurrentPreset == ThemePreset.OrangeAero ? Color.FromArgb(224, 117, 32) : CurrentPreset == ThemePreset.BlackTheme ? Color.FromArgb(30, 34, 40) : CurrentPreset == ThemePreset.RedTheme ? Color.FromArgb(207, 48, 55) : CurrentPreset == ThemePreset.YellowAero ? Color.FromArgb(218, 157, 24) : CurrentPreset == ThemePreset.TealAero ? Color.FromArgb(21, 155, 154) : CurrentPreset == ThemePreset.PinkAero ? Color.FromArgb(214, 73, 133) : CurrentPreset == ThemePreset.CyanAero ? Color.FromArgb(0, 155, 188) : Color.FromArgb(31, 149, 91);
                border = CurrentPreset == ThemePreset.BlueAero ? Color.FromArgb(19, 95, 168) : CurrentPreset == ThemePreset.LavenderAero ? Color.FromArgb(86, 68, 150) : CurrentPreset == ThemePreset.MinimalWhite ? Color.FromArgb(145, 155, 165) : CurrentPreset == ThemePreset.OrangeAero ? Color.FromArgb(190, 91, 18) : CurrentPreset == ThemePreset.BlackTheme ? Color.FromArgb(92, 100, 114) : CurrentPreset == ThemePreset.RedTheme ? Color.FromArgb(170, 30, 36) : CurrentPreset == ThemePreset.YellowAero ? Color.FromArgb(177, 116, 8) : CurrentPreset == ThemePreset.TealAero ? Color.FromArgb(12, 111, 113) : CurrentPreset == ThemePreset.PinkAero ? Color.FromArgb(174, 42, 101) : CurrentPreset == ThemePreset.CyanAero ? Color.FromArgb(0, 124, 164) : Color.FromArgb(19, 104, 68);
                textColor = CurrentPreset == ThemePreset.MinimalWhite ? Color.FromArgb(35, 45, 55) : Color.White;
            }

            using var path = RoundedRect(rect, radius);
            using (var body = new LinearGradientBrush(
                new Rectangle(rect.X, rect.Y, rect.Width, rect.Height + 1), top, bottom, LinearGradientMode.Vertical))
            {
                g.FillPath(body, path);
            }

            // Glossy highlight across the top half — the signature Aero shine.
            var savedClip = g.Clip;
            g.SetClip(path);
            int glossH = Math.Max(2, rect.Height / 2);
            using (var gloss = new LinearGradientBrush(
                new Rectangle(rect.X, rect.Y, rect.Width, glossH + 1),
                Color.FromArgb(pressed ? 40 : CurrentPreset == ThemePreset.DarkGlass ? 85 : 150, 255, 255, 255),
                Color.FromArgb(pressed ? 8 : CurrentPreset == ThemePreset.DarkGlass ? 16 : 40, 255, 255, 255),
                LinearGradientMode.Vertical))
            {
                g.FillRectangle(gloss, rect.X, rect.Y, rect.Width, glossH);
            }
            g.Clip = savedClip;

            using (var pen = new Pen(border))
                g.DrawPath(pen, path);

            TextRenderer.DrawText(g, btn.Text, btn.Font, new Rectangle(0, 0, btn.Width, btn.Height), textColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        private static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            if (d <= 0 || d > r.Width || d > r.Height)
            {
                path.AddRectangle(r);
                return path;
            }

            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private static void SetDoubleBuffered(Control control)
        {
            try
            {
                typeof(Control)
                    .GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic)?
                    .SetValue(control, true, null);
            }
            catch
            {
                // Non-critical; the UI still renders without double buffering.
            }
        }

        private static void ApplyLightTitleBar(Form form)
        {
            try
            {
                int useDark = CurrentPreset == ThemePreset.DarkGlass || CurrentPreset == ThemePreset.BlackTheme ? 1 : 0;
                // DWMWA_USE_IMMERSIVE_DARK_MODE = 20 (or 19 on early Windows 10 builds).
                if (DwmSetWindowAttribute(form.Handle, 20, ref useDark, sizeof(int)) != 0)
                    DwmSetWindowAttribute(form.Handle, 19, ref useDark, sizeof(int));
            }
            catch
            {
                // dwmapi unavailable (very old Windows) — ignore; the client area is still themed.
            }
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int pvAttribute, int cbAttribute);
    }
}

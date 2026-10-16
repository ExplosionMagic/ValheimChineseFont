using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine.UI;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace ValheimChineseFont
{
    [BepInPlugin(PluginId, PluginName, PluginVersion)]
    public sealed class ChineseFontPlugin : BaseUnityPlugin
    {
        public const string PluginId = "dev.valheim-cn.font-replacer";
        public const string PluginName = "Valheim Chinese Font Replacer";
        public const string PluginVersion = "0.1.0";

        private static ChineseFontPlugin _instance;
        private Harmony _harmony;

        // 静态字段，供 Harmony 补丁类访问
        internal static Font DreamHanSansFont;
        internal static Font DreamHanSerifFont;

        private Font _dreamHanSansFont;
        private Font _dreamHanSerifFont;

        // 配置项
        private ConfigEntry<string> _sansRegularFile;
        private ConfigEntry<string> _sansBoldFile;
        private ConfigEntry<string> _serifRegularFile;
        private ConfigEntry<string> _serifBoldFile;
        private ConfigEntry<string> _preloadCharsFile;
        private ConfigEntry<int> _atlasSize;

        private const string DefaultPreloadChars =
            "0123456789" +
            "abcdefghijklmnopqrstuvwxyz" +
            "ABCDEFGHIJKLMNOPQRSTUVWXYZ" +
            " !\"#$%&'()*+,-./:;<=>?@[\\]^_`{|}~" +
            "，。！？；：、（）【】《》“”‘’—…·　" +
            "的一是了我不人在他有这个上们来到时大地为子中你说生国年着就那和要她出也得" +
            "里后自以会家可下而过天去能对小多然于心学么之都好看起发当没成只如事把还用" +
            "第样道想作种开美总从无情己面最女但现前些所同日手又行意动方期它头经长儿回" +
            "位分爱老因很给名法间斯知世什两次使身者被高已亲其进此话常与活正感";

        private string _pluginDirectory;
        private string _sansRegularPath;
        private string _sansBoldPath;
        private string _serifRegularPath;
        private string _serifBoldPath;
        private string _preloadChars;

        private TMP_FontAsset _sansFallback;
        private TMP_FontAsset _serifFallback;
        private readonly HashSet<int> _patchedAveriaAssets = new HashSet<int>();
        private bool _installed;
        private bool _jotunnPatched;

        private void Awake()
        {
            _instance = this;
            _pluginDirectory = Path.GetDirectoryName(typeof(ChineseFontPlugin).Assembly.Location);

            _sansRegularFile = Config.Bind("Fonts", "SansRegular",
                "DreamHanSansCN-Regular.ttf",
                "Sans 常规字体文件名（相对于插件目录）");
            _sansBoldFile = Config.Bind("Fonts", "SansBold", "",
                "Sans 加粗字体文件名（留空则复用常规字体）");
            _serifRegularFile = Config.Bind("Fonts", "SerifRegular",
                "DreamHanSerifCN-Regular.ttf",
                "Serif 常规字体文件名（相对于插件目录）");
            _serifBoldFile = Config.Bind("Fonts", "SerifBold", "",
                "Serif 加粗字体文件名（留空则复用常规字体）");
            _preloadCharsFile = Config.Bind("Fonts", "PreloadCharsFile",
                "ChineseChars.txt",
                "预加载字符表（相对于插件目录，缺失则使用内置兜底表）");
            _atlasSize = Config.Bind("Fonts", "AtlasSize", 4096,
                "字体图集尺寸，建议 2048 或 4096");

            _harmony = new Harmony(PluginId);
            _harmony.PatchAll(Assembly.GetExecutingAssembly());

            StartCoroutine(InstallWhenResourcesAreReady());
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }

        private IEnumerator InstallWhenResourcesAreReady()
        {
            for (var attempt = 1; attempt <= 300; attempt++)
            {
                if (TryInstall()) yield break;
                yield return null;
            }
            Logger.LogError("300 帧内未检测到游戏的 Averia 字体资产，替换中止。");
        }

        private bool TryInstall()
        {
            if (_installed) return true;

            var allAssets = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            var averiaAssets = allAssets.Where(a => a != null && (
                a.name.StartsWith("Valheim-AveriaSansLibre", StringComparison.OrdinalIgnoreCase) ||
                a.name.StartsWith("Valheim-AveriaSerifLibre", StringComparison.OrdinalIgnoreCase)
            )).ToList();
            if (averiaAssets.Count == 0) return false;

            _sansRegularPath = ResolveFontPath(_sansRegularFile.Value);
            _serifRegularPath = ResolveFontPath(_serifRegularFile.Value);
            _sansBoldPath = ResolveFontPath(_sansBoldFile.Value);
            _serifBoldPath = ResolveFontPath(_serifBoldFile.Value);

            if (_sansRegularPath == null || _serifRegularPath == null)
            {
                Logger.LogError(
                    "未找到必需的字体文件。请确认 Fonts 目录下存在 " +
                    "DreamHanSansCN-Regular.ttf 和 DreamHanSerifCN-Regular.ttf。"
                );
                _installed = true;
                return true;
            }

            Logger.LogInfo($"Sans: {Path.GetFileName(_sansRegularPath)}");
            Logger.LogInfo($"Serif: {Path.GetFileName(_serifRegularPath)}");

            // 3. 创建 UnityEngine.Font（用路径直接加载 dynamic 字体）
            try
            {
                _dreamHanSansFont = Font.CreateDynamicFontFromOSFont(_sansRegularPath, 32);
                _dreamHanSerifFont = Font.CreateDynamicFontFromOSFont(_serifRegularPath, 32);

                if (_dreamHanSansFont == null || _dreamHanSerifFont == null)
                {
                    Logger.LogError(
                        $"字体加载失败：Sans={_dreamHanSansFont != null}, Serif={_dreamHanSerifFont != null}"
                    );
                    _installed = true;
                    return true;
                }

                _dreamHanSansFont.hideFlags = HideFlags.HideAndDontSave;
                _dreamHanSerifFont.hideFlags = HideFlags.HideAndDontSave;

                DreamHanSansFont = _dreamHanSansFont;
                DreamHanSerifFont = _dreamHanSerifFont;

                // 诊断日志：确认两个字体是否真的不同
                Logger.LogInfo(
                    $"Sans: name={_dreamHanSansFont.name}, " +
                    $"ID={_dreamHanSansFont.GetInstanceID()}, " +
                    $"dynamic={_dreamHanSansFont.dynamic}"
                );
                Logger.LogInfo(
                    $"Serif: name={_dreamHanSerifFont.name}, " +
                    $"ID={_dreamHanSerifFont.GetInstanceID()}, " +
                    $"dynamic={_dreamHanSerifFont.dynamic}"
                );
                Logger.LogInfo($"同一对象？{ReferenceEquals(_dreamHanSansFont, _dreamHanSerifFont)}");
            }
            catch (Exception e)
            {
                Logger.LogError($"创建 UnityEngine.Font 失败：{e.Message}");
                _installed = true;
                return true;
            }

            _preloadChars = LoadPreloadChars();
            Logger.LogInfo($"预加载字符数：{_preloadChars.Length}");

            var sourceFonts = Resources.FindObjectsOfTypeAll<Font>();
            var sansSource = sourceFonts.FirstOrDefault(f => f.name == "NotoSans-Regular");
            var serifSource = sourceFonts.FirstOrDefault(f => f.name == "NotoSerif-Regular");
            if (sansSource != null)
                _sansFallback = CreateFallback(sansSource, "ValheimCN-NotoSans-Fallback");
            if (serifSource != null)
                _serifFallback = CreateFallback(serifSource, "ValheimCN-NotoSerif-Fallback");

            EnableFallbackMaterialPresetMatching();

            var patched = PatchLoadedFontAssets();
            StartCoroutine(PatchFontsAsTheyLoad());

            Logger.LogInfo($"中文字体替换就绪：本次修补 {patched} 个 Averia 资产。");

            var uiPatched = PatchExistingUITexts();
            Logger.LogInfo($"首次替换 {uiPatched} 个 UnityEngine.UI.Text 的字体。");

            ApplyJotunnPatches();

            _installed = true;
            return true;
        }

        // ---- Jotunn Hook ----

        private void ApplyJotunnPatches()
        {
            if (_jotunnPatched) return;

            Type guiManagerType = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                guiManagerType = asm.GetType("Jotunn.Managers.GUIManager");
                if (guiManagerType != null) break;
            }

            if (guiManagerType == null)
            {
                Logger.LogInfo("未检测到 Jotunn，跳过 GUIManager.CreateText Hook。");
                return;
            }

            try
            {
                var createTextMethods = guiManagerType
                    .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(m => m.Name == "CreateText")
                    .ToList();

                if (createTextMethods.Count == 0)
                {
                    Logger.LogWarning("未找到 Jotunn GUIManager.CreateText 方法。");
                    return;
                }

                var prefix = new HarmonyMethod(typeof(JotunnCreateTextPatch).GetMethod(
                    nameof(JotunnCreateTextPatch.Prefix),
                    BindingFlags.Static | BindingFlags.NonPublic
                ));

                foreach (var method in createTextMethods)
                    _harmony.Patch(method, prefix: prefix);

                _jotunnPatched = true;
                Logger.LogInfo($"已 Hook Jotunn GUIManager.CreateText（{createTextMethods.Count} 个重载）。");
            }
            catch (Exception e)
            {
                Logger.LogWarning($"Hook Jotunn GUIManager.CreateText 失败：{e.Message}");
            }
        }

        // ---- 首次遍历已有 UI Text ----

        private int PatchExistingUITexts()
        {
            if (_dreamHanSansFont == null || _dreamHanSerifFont == null) return 0;

            var replaced = 0;
            foreach (var text in UnityEngine.Object.FindObjectsOfType<Text>(true))
            {
                if (text == null) continue;

                Font target = PickFontFor(text.font);
                if (target == null || text.font == target) continue;

                text.font = target;
                replaced++;
            }
            return replaced;
        }

        private Font PickFontFor(Font original)
        {
            if (original == null) return _dreamHanSerifFont;

            var name = original.name ?? "";
            if (name.IndexOf("AveriaSerif", StringComparison.OrdinalIgnoreCase) >= 0)
                return _dreamHanSerifFont;
            if (name.IndexOf("AveriaSans", StringComparison.OrdinalIgnoreCase) >= 0)
                return _dreamHanSansFont;

            return null;
        }

        // ---- 路径解析 ----

        private string ResolveFontPath(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return null;
            if (Path.IsPathRooted(fileName) && File.Exists(fileName)) return fileName;
            var candidate = Path.Combine(_pluginDirectory, fileName);
            if (File.Exists(candidate)) return candidate;
            candidate = Path.Combine(_pluginDirectory, "Fonts", fileName);
            if (File.Exists(candidate)) return candidate;
            return null;
        }

        private string LoadPreloadChars()
        {
            var filePath = ResolveFontPath(_preloadCharsFile.Value);
            if (filePath != null)
            {
                try
                {
                    var text = File.ReadAllText(filePath);
                    var chars = new string(text.Where(c => !char.IsWhiteSpace(c)).Distinct().ToArray());
                    if (chars.Length > 0) return chars;
                }
                catch (Exception e)
                {
                    Logger.LogWarning($"读取字符表失败：{e.Message}，使用内置兜底表。");
                }
            }
            return DefaultPreloadChars;
        }

        // ---- TMP 字体创建 ----

        private TMP_FontAsset CreateFallback(Font source, string assetName)
        {
            var asset = TMP_FontAsset.CreateFontAsset(
                source, 64, 8, GlyphRenderMode.SDFAA,
                _atlasSize.Value, _atlasSize.Value,
                AtlasPopulationMode.Dynamic, true
            );
            return PrepareFallback(asset, assetName);
        }

        private TMP_FontAsset PrepareFallback(TMP_FontAsset asset, string assetName)
        {
            if (asset == null) return null;
            asset.name = assetName;
            asset.hideFlags = HideFlags.HideAndDontSave;
            asset.isMultiAtlasTexturesEnabled = true;
            return asset;
        }

        // ---- TMP 材质预设 ----

        private void EnableFallbackMaterialPresetMatching()
        {
            if (TMP_Settings.matchMaterialPreset) return;
            var field = typeof(TMP_Settings).GetField(
                "m_matchMaterialPreset",
                BindingFlags.Instance | BindingFlags.NonPublic
            );
            if (field == null || TMP_Settings.instance == null) return;
            try { field.SetValue(TMP_Settings.instance, true); }
            catch { }
        }

        // ---- 持续修补 TMP ----

        private IEnumerator PatchFontsAsTheyLoad()
        {
            var wait = new WaitForSecondsRealtime(2f);
            while (true)
            {
                PatchLoadedFontAssets();
                yield return wait;
            }
        }

        private int PatchLoadedFontAssets()
        {
            var patched = 0;
            foreach (var asset in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
            {
                if (asset == null) continue;
                if (asset == _sansFallback || asset == _serifFallback) continue;

                if (IsValheimAveriaSans(asset))
                {
                    if (PopulateAveriaAsset(asset, isSerif: false)) patched++;
                    continue;
                }
                if (IsValheimAveriaSerif(asset))
                {
                    if (PopulateAveriaAsset(asset, isSerif: true)) patched++;
                }
            }
            return patched;
        }

        private bool IsValheimAveriaSans(TMP_FontAsset asset) =>
            asset.name.StartsWith("Valheim-AveriaSansLibre", StringComparison.OrdinalIgnoreCase);

        private bool IsValheimAveriaSerif(TMP_FontAsset asset) =>
            asset.name.StartsWith("Valheim-AveriaSerifLibre", StringComparison.OrdinalIgnoreCase);

        private bool PopulateAveriaAsset(TMP_FontAsset asset, bool isSerif)
        {
            if (!_patchedAveriaAssets.Add(asset.GetInstanceID())) return false;

            bool isBold = asset.name.IndexOf("Bold", StringComparison.OrdinalIgnoreCase) >= 0;

            string sourcePath;
            if (isSerif)
                sourcePath = (isBold && _serifBoldPath != null) ? _serifBoldPath : _serifRegularPath;
            else
                sourcePath = (isBold && _sansBoldPath != null) ? _sansBoldPath : _sansRegularPath;

            var sourceField = typeof(TMP_FontAsset).GetField(
                "m_SourceFontFile", BindingFlags.Instance | BindingFlags.NonPublic);
            var sourcePathField = typeof(TMP_FontAsset).GetField(
                "m_SourceFontFilePath", BindingFlags.Instance | BindingFlags.NonPublic);
            if (sourceField == null || sourcePathField == null)
            {
                Logger.LogError($"无法为 {asset.name} 注入源字体。");
                return true;
            }

            try
            {
                sourceField.SetValue(asset, null);
                sourcePathField.SetValue(asset, sourcePath);
            }
            catch (Exception e)
            {
                Logger.LogError($"注入源字体失败 {asset.name}：{e.Message}");
                return true;
            }

            asset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            asset.isMultiAtlasTexturesEnabled = true;

            var safetyFallback = isSerif ? _serifFallback : _sansFallback;
            if (safetyFallback != null) AddAssetFallback(asset, safetyFallback);

            var charactersToAdd = new string(
                _preloadChars.Where(c => !asset.HasCharacter(c)).ToArray());
            if (charactersToAdd.Length > 0)
                asset.TryAddCharacters(charactersToAdd, out var _, true);

            var unresolved = _preloadChars.Count(c => !asset.HasCharacter(c));
            if (unresolved > 0)
                Logger.LogWarning($"{asset.name} 仍有 {unresolved} 个预加载字符未成功加入。");

            Logger.LogInfo(
                $"已就地填充 {asset.name}（源：{Path.GetFileName(sourcePath)}），" +
                $"图集纹理数：{asset.atlasTextureCount}。"
            );

            foreach (var text in Resources.FindObjectsOfTypeAll<TMP_Text>())
                if (text != null && text.font == asset) text.SetAllDirty();
            return true;
        }

        private static bool AddAssetFallback(TMP_FontAsset asset, TMP_FontAsset fallback)
        {
            if (asset == null || fallback == null) return false;
            if (asset.fallbackFontAssetTable == null)
                asset.fallbackFontAssetTable = new List<TMP_FontAsset>();
            if (asset.fallbackFontAssetTable.Contains(fallback)) return false;
            asset.fallbackFontAssetTable.Insert(0, fallback);
            return true;
        }
    }

    // ===================== Harmony 补丁类 =====================

    internal static class JotunnCreateTextPatch
    {
        internal static void Prefix(ref Font font)
        {
            if (ChineseFontPlugin.DreamHanSansFont == null ||
                ChineseFontPlugin.DreamHanSerifFont == null) return;

            if (font == null)
            {
                font = ChineseFontPlugin.DreamHanSerifFont;
                return;
            }

            var name = font.name ?? "";
            if (name.IndexOf("AveriaSerif", StringComparison.OrdinalIgnoreCase) >= 0)
                font = ChineseFontPlugin.DreamHanSerifFont;
            else if (name.IndexOf("AveriaSans", StringComparison.OrdinalIgnoreCase) >= 0)
                font = ChineseFontPlugin.DreamHanSansFont;
        }
    }

    [HarmonyPatch(typeof(Text), "set_font")]
    internal static class TextSetFontPatch
    {
        static void Prefix(ref Font value)
        {
            if (ChineseFontPlugin.DreamHanSansFont == null ||
                ChineseFontPlugin.DreamHanSerifFont == null) return;

            if (value == null)
            {
                value = ChineseFontPlugin.DreamHanSerifFont;
                return;
            }

            var name = value.name ?? "";
            if (name.IndexOf("AveriaSerif", StringComparison.OrdinalIgnoreCase) >= 0)
                value = ChineseFontPlugin.DreamHanSerifFont;
            else if (name.IndexOf("AveriaSans", StringComparison.OrdinalIgnoreCase) >= 0)
                value = ChineseFontPlugin.DreamHanSansFont;
        }
    }
}
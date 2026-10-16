using BepInEx;
using BepInEx.Configuration;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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

        // ---- 配置项 ----
        private ConfigEntry<string> _sansRegularFile;
        private ConfigEntry<string> _sansBoldFile;
        private ConfigEntry<string> _serifRegularFile;
        private ConfigEntry<string> _serifBoldFile;
        private ConfigEntry<string> _preloadCharsFile;
        private ConfigEntry<int> _atlasSize;

        // ---- 兜底字符 ----
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

        // 从 DreamHan 创建的 TMP 字体，作为 Averia 的 fallback
        private TMP_FontAsset _customSans;
        private TMP_FontAsset _customSerif;
        // 从游戏内置 Noto 创建的安全 fallback（可选）
        private TMP_FontAsset _sansFallback;
        private TMP_FontAsset _serifFallback;

        private readonly HashSet<int> _patchedAveriaAssets = new HashSet<int>();
        private bool _installed;

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

            StartCoroutine(InstallWhenResourcesAreReady());
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

            // 1. 检查游戏 Averia 字体资产是否已加载
            var allAssets = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            var averiaAssets = allAssets.Where(a => a != null && (
                a.name.StartsWith("Valheim-AveriaSansLibre", StringComparison.OrdinalIgnoreCase) ||
                a.name.StartsWith("Valheim-AveriaSerifLibre", StringComparison.OrdinalIgnoreCase)
            )).ToList();
            if (averiaAssets.Count == 0) return false;

            // 2. 解析字体文件路径
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

            // 3. 加载预加载字符集
            _preloadChars = LoadPreloadChars();
            Logger.LogInfo($"预加载字符数：{_preloadChars.Length}");

            // 4. 从 DreamHan 字体文件创建 TMP_FontAsset（作为 Averia 的 fallback）
            _customSans = CreateFallback(_sansRegularPath, "ValheimCN-Sans");
            _customSerif = CreateFallback(_serifRegularPath, "ValheimCN-Serif");

            if (_customSans == null || _customSerif == null)
            {
                Logger.LogError("无法从 DreamHan 字体文件创建 TMP_FontAsset。");
                _installed = true;
                return true;
            }

            // 5. 从游戏内置 Noto 创建安全 fallback（可选）
            var sourceFonts = Resources.FindObjectsOfTypeAll<Font>();
            var sansSource = sourceFonts.FirstOrDefault(f => f.name == "NotoSans-Regular");
            var serifSource = sourceFonts.FirstOrDefault(f => f.name == "NotoSerif-Regular");
            if (sansSource != null)
                _sansFallback = CreateFallback(sansSource, "ValheimCN-NotoSans-Fallback");
            if (serifSource != null)
                _serifFallback = CreateFallback(serifSource, "ValheimCN-NotoSerif-Fallback");

            // 6. 启用 TMP 材质预设匹配（让 fallback 字符也带上原版描边）
            EnableFallbackMaterialPresetMatching();

            // 7. 给已加载的 Averia 挂上 DreamHan fallback
            var patched = PatchLoadedFontAssets();

            // 8. 持续监控后续加载的 Averia 资产
            StartCoroutine(PatchFontsAsTheyLoad());

            Logger.LogInfo($"中文字体回退链就绪：本次修补 {patched} 个 Averia 资产。");
            _installed = true;
            return true;
        }

        // ---- 字体路径 / 字符表解析 ----

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

        // ---- 字体资源创建 ----

        private TMP_FontAsset CreateFallback(string fontPath, string assetName)
        {
            var asset = TMP_FontAsset.CreateFontAsset(
                fontPath,
                0,
                64,
                8,
                GlyphRenderMode.SDFAA,
                _atlasSize.Value,
                _atlasSize.Value
            );
            return PrepareFallback(asset, assetName);
        }

        private TMP_FontAsset CreateFallback(Font source, string assetName)
        {
            var asset = TMP_FontAsset.CreateFontAsset(
                source,
                64,
                8,
                GlyphRenderMode.SDFAA,
                _atlasSize.Value,
                _atlasSize.Value,
                AtlasPopulationMode.Dynamic,
                true
            );
            return PrepareFallback(asset, assetName);
        }

        private TMP_FontAsset PrepareFallback(TMP_FontAsset asset, string assetName)
        {
            if (asset == null) return null;
            asset.name = assetName;
            asset.hideFlags = HideFlags.HideAndDontSave;
            asset.isMultiAtlasTexturesEnabled = true;

            // 让 fallback 也能支持描边（当 TMP 从 fallback 取字形时）
            if (asset.material != null)
                asset.material.EnableKeyword("OUTLINE_ON");

            return asset;
        }

        // ---- TMP 材质预设匹配 ----

        private void EnableFallbackMaterialPresetMatching()
        {
            if (TMP_Settings.matchMaterialPreset) return;
            var field = typeof(TMP_Settings).GetField(
                "m_matchMaterialPreset",
                BindingFlags.Instance | BindingFlags.NonPublic
            );
            if (field == null || TMP_Settings.instance == null)
            {
                Logger.LogWarning("无法启用 TMP 材质匹配，描边效果可能与原版不一致。");
                return;
            }
            try { field.SetValue(TMP_Settings.instance, true); }
            catch (Exception e)
            {
                Logger.LogWarning($"启用 TMP 材质匹配失败：{e.Message}");
                return;
            }
            foreach (var text in Resources.FindObjectsOfTypeAll<TMP_Text>())
                if (text != null) text.SetAllDirty();
            Logger.LogInfo("已启用 TMP 回退材质预设匹配。");
        }

        // ---- 回退链核心逻辑 ----

        private IEnumerator PatchFontsAsTheyLoad()
        {
            var wait = new WaitForSecondsRealtime(1f);
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
                if (asset == _customSans || asset == _customSerif) continue;
                if (asset == _sansFallback || asset == _serifFallback) continue;

                // 用 GetInstanceID 去重（同名资产可能被多次枚举）
                if (!_patchedAveriaAssets.Add(asset.GetInstanceID())) continue;

                if (IsValheimAveriaSans(asset))
                {
                    // 把 DreamHan Sans 挂在 Averia Sans 前面
                    if (AddAssetFallback(asset, _customSans)) patched++;
                    // 再加 Noto 作为最终安全网
                    if (_sansFallback != null) AddAssetFallback(asset, _sansFallback);
                    continue;
                }
                if (IsValheimAveriaSerif(asset))
                {
                    if (AddAssetFallback(asset, _customSerif)) patched++;
                    if (_serifFallback != null) AddAssetFallback(asset, _serifFallback);
                }
            }
            return patched;
        }

        private bool IsValheimAveriaSans(TMP_FontAsset asset) =>
            asset.name.StartsWith("Valheim-AveriaSansLibre", StringComparison.OrdinalIgnoreCase);

        private bool IsValheimAveriaSerif(TMP_FontAsset asset) =>
            asset.name.StartsWith("Valheim-AveriaSerifLibre", StringComparison.OrdinalIgnoreCase);

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
}
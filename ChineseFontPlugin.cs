using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
<<<<<<< Updated upstream
=======
using System.Runtime.InteropServices;
using UnityEngine.UI;
>>>>>>> Stashed changes
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;



namespace ValheimChineseFont
{
    [BepInPlugin(PluginId, PluginName, PluginVersion)]
    [BepInDependency("com.jotunn.jotunn", BepInDependency.DependencyFlags.SoftDependency)]

    public sealed class ChineseFontPlugin : BaseUnityPlugin
    {
<<<<<<< Updated upstream
        private Harmony _harmony;
        public const string PluginId = "dev.valheim-cn.font-patcher";
        public const string PluginName = "Valheim Chinese Font patcher";
        public const string PluginVersion = "0.1.0";

        private static ChineseFontPlugin _instance;
        
        private TMP_FontAsset _customSans;
        private TMP_FontAsset _customSerif;
        
        private Material _sansOutlineMaterial;
        private Material _serifOutlineMaterial;
        
        internal static readonly Dictionary<string, TMP_FontAsset> FontReplacements = new Dictionary<string, TMP_FontAsset>();
=======
        [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
        private static extern int AddFontResource(string lpFileName);

        [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
        private static extern bool RemoveFontResource(string lpFileName);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        private const int WM_FONTCHANGE = 0x001D;
        private static readonly IntPtr HWND_BROADCAST = new IntPtr(0xffff);

        public const string PluginId = "dev.valheim-cn.font-replacer";
        public const string PluginName = "Valheim Chinese Font Replacer";
        public const string PluginVersion = "0.1.0";

        private static ChineseFontPlugin _instance;
        private Harmony _harmony;
>>>>>>> Stashed changes

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
            _harmony = new Harmony(PluginId);
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

<<<<<<< Updated upstream
            _harmony.PatchAll(Assembly.GetExecutingAssembly());

            InstallFontsSynchronously();

=======
            // 应用静态 Harmony 补丁（Text.set_font）
            _harmony = new Harmony(PluginId);
            _harmony.PatchAll(Assembly.GetExecutingAssembly());

>>>>>>> Stashed changes
            StartCoroutine(InstallWhenResourcesAreReady());
        }
        private void InstallFontsSynchronously()
        {
            try
            {
                _sansRegularPath = ResolveFontPath(_sansRegularFile.Value);
                _serifRegularPath = ResolveFontPath(_serifRegularFile.Value);
                _sansBoldPath = ResolveFontPath(_sansBoldFile.Value);
                _serifBoldPath = ResolveFontPath(_serifBoldFile.Value);

                if (_sansRegularPath == null || _serifRegularPath == null)
                {
                    Logger.LogError("字体文件未找到，无法提前注册。");
                    return;
                }

                _customSans = CreateFallback(_sansRegularPath, "ValheimCN-Sans");
                _customSerif = CreateFallback(_serifRegularPath, "ValheimCN-Serif");

                if (_customSans == null || _customSerif == null)
                {
                    Logger.LogError("无法同步创建字体资产。");
                    return;
                }

                FontReplacements["Valheim-AveriaSansLibre"] = _customSans;
                FontReplacements["Valheim-AveriaSerifLibre"] = _customSerif;
                FontReplacements["Valheim-AveriaSansLibre-Bold"] = _customSans;
                FontReplacements["Valheim-AveriaSerifLibre-Bold"] = _customSerif;

                Logger.LogInfo($"已提前注册 {FontReplacements.Count} 个字体替换条目。");
            }
            catch (Exception e)
            {
                Logger.LogError($"同步注册字体失败：{e}");
            }
        }

        private void ApplyMaterialPreset(TMP_FontAsset custom, Material template)
        {
            if (custom == null || template == null || custom.material == null) return;

            // 用新字体的 atlas 作为基础
            var newMat = new Material(custom.material);
            // 从原版材质复制 shader 参数（outline、shadow、glow 等）
            TMP_MaterialManager.CopyMaterialPresetProperties(template, newMat);
            newMat.hideFlags = HideFlags.HideAndDontSave;

            // 替换字体的默认材质
            custom.material = newMat;
            custom.material.name = custom.name + " Outline";
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();

            if (!string.IsNullOrEmpty(_sansRegularPath))
                RemoveFontResource(_sansRegularPath);
            if (!string.IsNullOrEmpty(_serifRegularPath))
                RemoveFontResource(_serifRegularPath);

            SendMessage(HWND_BROADCAST, WM_FONTCHANGE, IntPtr.Zero, IntPtr.Zero);
        }

        private IEnumerator InstallWhenResourcesAreReady()
        {
<<<<<<< Updated upstream
            yield return null;

            // 等游戏第一场景加载完，TMP_Text 组件才存在
            yield return new WaitForSecondsRealtime(3f);

            var replaced = ReplaceTextFonts();

            // 持续监控：游戏运行中动态创建的 UI（如 XPortal 面板）
            StartCoroutine(PatchFontsAsTheyLoad());

            Logger.LogInfo($"中文字体替换就绪：本次替换 {replaced} 个 TMP_Text 的字体引用。");
        }
        private IEnumerator PatchFontsAsTheyLoad()
        {
            var wait = new WaitForSecondsRealtime(1f);
            while (true)
=======
            for (var attempt = 1; attempt <= 300; attempt++)
>>>>>>> Stashed changes
            {
                ReplaceTextFonts();
                yield return wait;
            }
        }

        /// <summary>
        /// 遍历所有 TMP_Text，如果引用的字体在替换表里，就换成自定义字体。
        /// </summary>
        private int ReplaceTextFonts()
        {
            var replaced = 0;
            foreach (var text in Resources.FindObjectsOfTypeAll<TMP_Text>())
            {
                if (text == null || text.font == null) continue;

                if (FontReplacements.TryGetValue(text.font.name, out var replacement))
                {
                    var sourceMat = text.fontSharedMaterial;
                    bool isSans = replacement == _customSans;

                    // ↓↓↓ 首次遇到原版材质时，以它作为模板复制描边属性 ↓↓↓
                    if (sourceMat != null)
                    {
                        if (isSans && _sansOutlineMaterial == null)
                        {
                            _sansOutlineMaterial = new Material(replacement.material);
                            TMP_MaterialManager.CopyMaterialPresetProperties(sourceMat, _sansOutlineMaterial);
                            _sansOutlineMaterial.hideFlags = HideFlags.HideAndDontSave;
                            _sansOutlineMaterial.name = "ValheimCN-Sans-Outline";
                            Logger.LogInfo("已从原版 Sans 材质捕获描边预设。");
                        }
                        else if (!isSans && _serifOutlineMaterial == null)
                        {
                            _serifOutlineMaterial = new Material(replacement.material);
                            TMP_MaterialManager.CopyMaterialPresetProperties(sourceMat, _serifOutlineMaterial);
                            _serifOutlineMaterial.hideFlags = HideFlags.HideAndDontSave;
                            _serifOutlineMaterial.name = "ValheimCN-Serif-Outline";
                            Logger.LogInfo("已从原版 Serif 材质捕获描边预设。");
                        }
                    }

                    // 替换字体
                    text.font = replacement;

                    // 套用已捕获的描边材质
                    if (isSans && _sansOutlineMaterial != null)
                        text.fontSharedMaterial = _sansOutlineMaterial;
                    else if (!isSans && _serifOutlineMaterial != null)
                        text.fontSharedMaterial = _serifOutlineMaterial;

                    replaced++;
                }
            }
            return replaced;
        }

        private bool TryInstall()
        {
            if (_installed) return true;

            // 1. 检查 Averia 资产
            var allAssets = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            var averiaAssets = allAssets.Where(a => a != null && (
                a.name.StartsWith("Valheim-AveriaSansLibre", StringComparison.OrdinalIgnoreCase) ||
                a.name.StartsWith("Valheim-AveriaSerifLibre", StringComparison.OrdinalIgnoreCase)
            )).ToList();
            if (averiaAssets.Count == 0) return false;

            // 2. 解析字体文件路径 // 移到 Awake
            //_sansRegularPath = ResolveFontPath(_sansRegularFile.Value);
            //_serifRegularPath = ResolveFontPath(_serifRegularFile.Value);
            //_sansBoldPath = ResolveFontPath(_sansBoldFile.Value);
            //_serifBoldPath = ResolveFontPath(_serifBoldFile.Value);

            if (_sansRegularPath == null || _serifRegularPath == null)
            {
                Logger.LogError(
                    "未找到必需的字体文件。请确认 Fonts 目录下的文件名为 " +
                    "DreamHanSansCN-Regular.ttf 和 DreamHanSerifCN-Regular.ttf。"
                );
                _installed = true;
                return true;
            }

            Logger.LogInfo($"Sans: {Path.GetFileName(_sansRegularPath)}");
            Logger.LogInfo($"Serif: {Path.GetFileName(_serifRegularPath)}");

            // 3. 用 CreateDynamicFontFromOSFont 直接加载 TTF 路径
            //    Unity 内部会调 AddFontResourceEx(FR_PRIVATE) 私加载字体，不写入系统注册表。
            try
            {
                _dreamHanSansFont = Font.CreateDynamicFontFromOSFont(_sansRegularPath, 32);
                _dreamHanSerifFont = Font.CreateDynamicFontFromOSFont(_serifRegularPath, 32);

                if (_dreamHanSansFont == null || _dreamHanSerifFont == null)
                {
                    Logger.LogError(
                        $"字体加载失败：Sans={_dreamHanSansFont != null}, Serif={_dreamHanSerifFont != null}。" +
                        $"SansPath={_sansRegularPath}, SerifPath={_serifRegularPath}"
                    );
                    _installed = true;
                    return true;
                }

                _dreamHanSansFont.hideFlags = HideFlags.HideAndDontSave;
                _dreamHanSerifFont.hideFlags = HideFlags.HideAndDontSave;

                DreamHanSansFont = _dreamHanSansFont;
                DreamHanSerifFont = _dreamHanSerifFont;

                Logger.LogInfo(
                    $"DreamHan Dynamic Font OK：Sans={_dreamHanSansFont.name} " +
                    $"(dynamic={_dreamHanSansFont.dynamic}), " +
                    $"Serif={_dreamHanSerifFont.name} (dynamic={_dreamHanSerifFont.dynamic})"
                );
            }
            catch (Exception e)
            {
                Logger.LogError($"创建 UnityEngine.Font 失败：{e.Message}");
                _installed = true;
                return true;
            }

            // 4. 加载预加载字符集
            _preloadChars = LoadPreloadChars();
            Logger.LogInfo($"预加载字符数：{_preloadChars.Length}");

            // 5. 创建 Noto 安全 fallback（可选）
            var sourceFonts = Resources.FindObjectsOfTypeAll<Font>();
            var sansSource = sourceFonts.FirstOrDefault(f => f.name == "NotoSans-Regular");
            var serifSource = sourceFonts.FirstOrDefault(f => f.name == "NotoSerif-Regular");
            if (sansSource != null)
                _sansFallback = CreateFallback(sansSource, "ValheimCN-NotoSans-Fallback");
            if (serifSource != null)
                _serifFallback = CreateFallback(serifSource, "ValheimCN-NotoSerif-Fallback");

<<<<<<< Updated upstream
            // 创建 DreamHan 自定义字体（这才是真正要替换游戏 Averia 的字体）
            var customSans = CreateFallback(_sansRegularPath, "ValheimCN-Sans");
            var customSerif = CreateFallback(_serifRegularPath, "ValheimCN-Serif");

            if (customSans == null || customSerif == null)
            {
                Logger.LogError("无法创建 DreamHan 字体资产。");
                _installed = true;
                return true;
            }

            // 注册到替换表（键名要和游戏原始资产名完全一致）
            FontReplacements["Valheim-AveriaSansLibre"] = customSans;
            FontReplacements["Valheim-AveriaSerifLibre"] = customSerif;
            FontReplacements["Valheim-AveriaSansLibre-Bold"] = customSans;   // 或你单独的 Bold
            FontReplacements["Valheim-AveriaSerifLibre-Bold"] = customSerif;

            Logger.LogInfo($"已注册 {FontReplacements.Count} 个字体替换条目。");

            // 5. 启用 TMP 材质预设匹配（保留原版描边效果）
=======
            // 6. TMP 材质预设匹配
>>>>>>> Stashed changes
            EnableFallbackMaterialPresetMatching();

            // 7. 就地填充 Averia TMP 资产 + 持续监控
            var patched = PatchLoadedFontAssets();
            StartCoroutine(PatchFontsAsTheyLoad());

            Logger.LogInfo($"中文字体替换就绪：本次修补 {patched} 个 Averia 资产。");

            // 8. 首次遍历 Unity UI Text（替换已存在的）
            var uiPatched = PatchExistingUITexts();
            Logger.LogInfo($"首次替换 {uiPatched} 个 UnityEngine.UI.Text 的字体。");

            // 9. 尝试 Hook Jotunn GUIManager.CreateText
            ApplyJotunnPatches();

            _installed = true;
            return true;
        }
<<<<<<< Updated upstream
        
        // ---- 字体路径 / 字符表解析 ----
=======

        // ---- Jotunn Hook ----

        /// <summary>
        /// 尝试 Hook Jotunn GUIManager.CreateText。Jotunn 未安装时安全跳过。
        /// </summary>
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
                // CreateText 有多个重载，这里找签名含 Font 参数的那个
                var createTextMethods = guiManagerType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
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
                {
                    _harmony.Patch(method, prefix: prefix);
                }

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

        /// <summary>
        /// 根据原字体名决定用 DreamHan Sans 还是 Serif。
        /// null（Jotunn 找不到 Averia）→ 用 Serif（对齐 XPortal 的标题/正文用法）。
        /// </summary>
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
>>>>>>> Stashed changes

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

        private TMP_FontAsset CreateFallback(string fontPath, string assetName)
        {
            var asset = TMP_FontAsset.CreateFontAsset(
                fontPath,
                0,                              // 采样点大小（0 = 自动）
                64,
                8,
                GlyphRenderMode.SDFAA,
                _atlasSize.Value,
                _atlasSize.Value
            );
            return PrepareFallback(asset, assetName);
        }

        private TMP_FontAsset PrepareFallback(TMP_FontAsset asset, string assetName)
        {
            if (asset == null) return null;
            asset.name = assetName;
            asset.hideFlags = HideFlags.HideAndDontSave;
            asset.isMultiAtlasTexturesEnabled = true;
            
            if (asset.material != null)
            {
                asset.material.EnableKeyword("OUTLINE_ON");
                // 不设置 _OutlineWidth，默认 0，无可见描边
                // 不设置 _OutlineColor，默认黑色
                // 这样只有显式设置 outlineWidth 的文本才会有描边
            
            }
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
            if (field == null || TMP_Settings.instance == null)
            {
                Logger.LogWarning("无法启用 TMP 材质匹配。");
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

        // ---- 持续修补（仅 TMP 资产） ----

<<<<<<< Updated upstream
        //private IEnumerator PatchFontsAsTheyLoad()
        //{
        //    var wait = new WaitForSecondsRealtime(1f);
        //    while (true)
        //    {
        //        PatchLoadedFontAssets();
        //        yield return wait;
        //    }
        //}
=======
        private IEnumerator PatchFontsAsTheyLoad()
        {
            var wait = new WaitForSecondsRealtime(2f);   // 2 秒一次，降低开销
            while (true)
            {
                PatchLoadedFontAssets();
                yield return wait;
            }
        }
>>>>>>> Stashed changes

        // 为所有非自定义字体资产添加回退
        private int PatchLoadedFontAssets()
        {
            var patched = 0;
            foreach (var asset in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
            {
                if (asset == null || asset == _sansFallback || asset == _serifFallback)
                    continue;

                // 确保所有 Averia 字体都有回退链
                if (IsValheimAveriaSans(asset))
                {
                    // 强制添加回退，即使资产已经被“填充”过
                    if (AddAssetFallback(asset, _sansFallback))
                        patched++;
                    continue;
                }
                if (IsValheimAveriaSerif(asset))
                {
                    if (AddAssetFallback(asset, _serifFallback))
                        patched++;
                    continue;
                }

                // 为其他字体（如 Norse）也添加通用回退
                var preferredFallback = asset.name.Contains("Serif") ? _serifFallback : _sansFallback;
                if (AddAssetFallback(asset, preferredFallback))
                    patched++;
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
                "m_SourceFontFile",
                BindingFlags.Instance | BindingFlags.NonPublic
            );
            var sourcePathField = typeof(TMP_FontAsset).GetField(
                "m_SourceFontFilePath",
                BindingFlags.Instance | BindingFlags.NonPublic
            );
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
                _preloadChars.Where(c => !asset.HasCharacter(c)).ToArray()
            );
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

<<<<<<< Updated upstream
    [HarmonyPatch(typeof(Resources), nameof(Resources.FindObjectsOfTypeAll), new Type[] { typeof(Type) })]
    internal static class ResourcesFindPatch
    {
        internal static Material SansOutlineTemplate;
        internal static Material SerifOutlineTemplate;

        static void Postfix(Type type, ref UnityEngine.Object[] __result)
        {
            if (type != typeof(TMP_FontAsset) || __result == null) return;

            for (int i = 0; i < __result.Length; i++)
            {
                var font = __result[i] as TMP_FontAsset;
                if (font == null) continue;

                if (font.name.StartsWith("Valheim-AveriaSansLibre", StringComparison.OrdinalIgnoreCase))
                {
                    if (SansOutlineTemplate == null && font.material != null)
                        SansOutlineTemplate = font.material;
                }
                else if (font.name.StartsWith("Valheim-AveriaSerifLibre", StringComparison.OrdinalIgnoreCase))
                {
                    if (SerifOutlineTemplate == null && font.material != null)
                        SerifOutlineTemplate = font.material;
                }

                if (ChineseFontPlugin.FontReplacements.TryGetValue(font.name, out var replacement))
                {
                    __result[i] = replacement;
                }
            }
        }
    }

    [HarmonyPatch(typeof(Resources), nameof(Resources.Load),
          new Type[] { typeof(string), typeof(Type) })]
    internal static class ResourcesLoadPatch
    {
        static bool Prefix(string path, Type systemTypeInstance, ref UnityEngine.Object __result)
        {
            if (systemTypeInstance != typeof(TMP_FontAsset)) return true;

            if (ChineseFontPlugin.FontReplacements.TryGetValue(path, out var replacement))
            {
                __result = replacement;
                return false;
            }
            return true;
=======
    // ===================== Harmony 补丁类 =====================

    /// <summary>
    /// Hook Jotunn GUIManager.CreateText，从源头替换 font 参数。
    /// Jotunn 未安装时这个类不会被打 Patch，但依然编译在 DLL 里，不影响。
    /// </summary>
    internal static class JotunnCreateTextPatch
    {
        internal static void Prefix(ref Font font)
        {
            if (ChineseFontPlugin.DreamHanSansFont == null ||
                ChineseFontPlugin.DreamHanSerifFont == null) return;

            if (font == null)
            {
                // Jotunn 找不到 Averia 时传入 null → 用 Serif（对齐 XPortal 标题/正文）
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

    /// <summary>
    /// 兜底：Hook UnityEngine.UI.Text.set_font。
    /// 任何代码给 Text 设置 null 或 Averia 字体时自动替换成 DreamHan。
    /// 覆盖非 Jotunn 创建的 UI Text。
    /// </summary>
    [HarmonyPatch(typeof(Text), "set_font")]
    internal static class TextSetFontPatch
    {
        static void Prefix(ref Font value)
        {
            if (ChineseFontPlugin.DreamHanSansFont == null ||
                ChineseFontPlugin.DreamHanSerifFont == null) return;

            if (value == null)
            {
                // 默认 Serif，对齐 XPortal 的 null 兜底
                value = ChineseFontPlugin.DreamHanSerifFont;
                return;
            }

            var name = value.name ?? "";
            if (name.IndexOf("AveriaSerif", StringComparison.OrdinalIgnoreCase) >= 0)
                value = ChineseFontPlugin.DreamHanSerifFont;
            else if (name.IndexOf("AveriaSans", StringComparison.OrdinalIgnoreCase) >= 0)
                value = ChineseFontPlugin.DreamHanSansFont;
>>>>>>> Stashed changes
        }
    }
}
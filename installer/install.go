package main

import (
	"archive/zip"
	"bytes"
	"crypto/sha256"
	"encoding/hex"
	"encoding/json"
	"errors"
	"fmt"
	"io"
	"io/fs"
	"net/http"
	"os"
	"path/filepath"
	"regexp"
	"runtime"
	"sort"
	"strings"
	"time"
)

const (
	bepInExURL    = "https://thunderstore.io/package/download/denikson/BepInExPack_Valheim/5.4.2333/"
	bepInExSHA256 = "5dd24ccbcaa9260f714b200f23c4c15547e2aa5f06906cafcc0dee56db1bf716"
	pluginDirName = "ValheimVietHoa"
)

type vanillaManifest struct {
	Version         string `json:"version"`
	ValheimBuildID  string `json:"valheim_build_id"`
	SourceSHA256    string `json:"source_sha256"`
	OutputSHA256    string `json:"output_sha256"`
	SourceSize      int64  `json:"source_size"`
	OutputSize      int64  `json:"output_size"`
	TranslationKeys int    `json:"translation_keys"`
}

func sha256Bytes(data []byte) string {
	sum := sha256.Sum256(data)
	return hex.EncodeToString(sum[:])
}

func sha256File(path string) (string, error) {
	file, err := os.Open(path)
	if err != nil {
		return "", err
	}
	defer file.Close()
	hash := sha256.New()
	if _, err := io.Copy(hash, file); err != nil {
		return "", err
	}
	return hex.EncodeToString(hash.Sum(nil)), nil
}

func resourcesPath(gameDir string) (string, error) {
	candidates := []string{
		filepath.Join(gameDir, "valheim_Data", "resources.assets"),
		filepath.Join(gameDir, "valheim.app", "Contents", "Resources", "Data", "resources.assets"),
		filepath.Join(gameDir, "Contents", "Resources", "Data", "resources.assets"),
		filepath.Join(gameDir, "resources.assets"),
	}
	if filepath.Base(gameDir) == "resources.assets" {
		candidates = append([]string{gameDir}, candidates...)
	}
	for _, candidate := range candidates {
		if info, err := os.Stat(candidate); err == nil && !info.IsDir() {
			return candidate, nil
		}
	}
	return "", fmt.Errorf("không tìm thấy resources.assets trong %s", gameDir)
}

func normalizeGameDir(path string) (string, error) {
	absolute, err := filepath.Abs(strings.Trim(path, `"`))
	if err != nil {
		return "", err
	}
	if filepath.Base(absolute) == "resources.assets" {
		parent := filepath.Dir(absolute)
		if filepath.Base(parent) == "valheim_Data" {
			absolute = filepath.Dir(parent)
		} else if filepath.Base(parent) == "Data" {
			for range 4 {
				absolute = filepath.Dir(absolute)
			}
			absolute = filepath.Dir(absolute)
		}
	}
	if filepath.Base(absolute) == "valheim_Data" || filepath.Ext(absolute) == ".app" {
		absolute = filepath.Dir(absolute)
	}
	if _, err := resourcesPath(absolute); err != nil {
		return "", err
	}
	return absolute, nil
}

var vdfPathPattern = regexp.MustCompile(`(?m)"path"\s+"([^"]+)"`)

func steamLibraries(vdf string) []string {
	var result []string
	for _, match := range vdfPathPattern.FindAllStringSubmatch(vdf, -1) {
		path := strings.ReplaceAll(match[1], `\\`, `\`)
		result = append(result, filepath.Clean(path))
	}
	return result
}

func defaultSteamRoots() []string {
	home, _ := os.UserHomeDir()
	switch runtime.GOOS {
	case "windows":
		return []string{
			filepath.Join(os.Getenv("ProgramFiles(x86)"), "Steam"),
			filepath.Join(os.Getenv("ProgramFiles"), "Steam"),
		}
	case "darwin":
		return []string{filepath.Join(home, "Library", "Application Support", "Steam")}
	default:
		return []string{
			filepath.Join(home, ".local", "share", "Steam"),
			filepath.Join(home, ".steam", "steam"),
		}
	}
}

func detectGameDirs() []string {
	seen := map[string]bool{}
	var roots []string
	for _, root := range defaultSteamRoots() {
		if root == "" || seen[root] {
			continue
		}
		seen[root] = true
		roots = append(roots, root)
		data, err := os.ReadFile(filepath.Join(root, "steamapps", "libraryfolders.vdf"))
		if err == nil {
			for _, library := range steamLibraries(string(data)) {
				if !seen[library] {
					seen[library] = true
					roots = append(roots, library)
				}
			}
		}
	}
	var games []string
	for _, root := range roots {
		game := filepath.Join(root, "steamapps", "common", "Valheim")
		if _, err := resourcesPath(game); err == nil {
			games = append(games, game)
		}
	}
	sort.Strings(games)
	return games
}

func writeAtomic(path string, data []byte, mode fs.FileMode) error {
	if err := os.MkdirAll(filepath.Dir(path), 0o755); err != nil {
		return err
	}
	temporary, err := os.CreateTemp(filepath.Dir(path), filepath.Base(path)+".*.tmp")
	if err != nil {
		return err
	}
	temporaryName := temporary.Name()
	defer os.Remove(temporaryName)
	if _, err := temporary.Write(data); err != nil {
		temporary.Close()
		return err
	}
	if err := temporary.Sync(); err != nil {
		temporary.Close()
		return err
	}
	if err := temporary.Chmod(mode); err != nil {
		temporary.Close()
		return err
	}
	if err := temporary.Close(); err != nil {
		return err
	}
	return os.Rename(temporaryName, path)
}

func installVanilla(gameDir string, assets fs.FS) error {
	var manifest vanillaManifest
	manifestData, err := fs.ReadFile(assets, "assets/vanilla-manifest.json")
	if err != nil {
		return err
	}
	if err := json.Unmarshal(manifestData, &manifest); err != nil {
		return err
	}
	resources, err := resourcesPath(gameDir)
	if err != nil {
		return err
	}
	actualHash, err := sha256File(resources)
	if err != nil {
		return err
	}
	if actualHash == manifest.OutputSHA256 {
		fmt.Println("Bản không mod đã được cài đúng phiên bản.")
		return nil
	}
	if actualHash != manifest.SourceSHA256 {
		return fmt.Errorf("từ chối vá build Valheim không tương thích (SHA-256 %s); hãy Verify integrity/update game rồi dùng release tương ứng", actualHash)
	}
	original, err := os.ReadFile(resources)
	if err != nil {
		return err
	}
	if int64(len(original)) != manifest.SourceSize {
		return errors.New("kích thước resources.assets không khớp manifest")
	}
	backup := resources + ".valheim-vn.backup"
	if _, err := os.Stat(backup); errors.Is(err, os.ErrNotExist) {
		if err := writeAtomic(backup, original, 0o644); err != nil {
			return fmt.Errorf("tạo backup: %w", err)
		}
	} else if err != nil {
		return fmt.Errorf("kiểm tra backup: %w", err)
	} else if backupHash, err := sha256File(backup); err != nil {
		return fmt.Errorf("đọc backup: %w", err)
	} else if backupHash != manifest.SourceSHA256 {
		return errors.New("backup hiện có sai hash; hãy đổi tên file backup đó trước khi thử lại")
	}
	patch, err := fs.ReadFile(assets, "assets/resources.assets.bsdiff")
	if err != nil {
		return err
	}
	patched, err := applyBSDiff(original, patch)
	if err != nil {
		return err
	}
	if int64(len(patched)) != manifest.OutputSize || sha256Bytes(patched) != manifest.OutputSHA256 {
		return errors.New("bản vá tạo kết quả sai hash; resources.assets gốc chưa bị thay đổi")
	}
	if err := writeAtomic(resources, patched, 0o644); err != nil {
		return fmt.Errorf("cài resources.assets: %w", err)
	}
	fmt.Printf("Đã cài bản không mod cho Valheim build %s; backup: %s\n", manifest.ValheimBuildID, backup)
	return nil
}

func restoreVanilla(gameDir string, assets fs.FS) error {
	var manifest vanillaManifest
	data, err := fs.ReadFile(assets, "assets/vanilla-manifest.json")
	if err != nil {
		return err
	}
	if err := json.Unmarshal(data, &manifest); err != nil {
		return err
	}
	resources, err := resourcesPath(gameDir)
	if err != nil {
		return err
	}
	backup := resources + ".valheim-vn.backup"
	currentHash, err := sha256File(resources)
	if err != nil {
		return err
	}
	if currentHash == manifest.SourceSHA256 {
		fmt.Println("resources.assets đã là bản gốc.")
		return nil
	}
	if currentHash != manifest.OutputSHA256 {
		return errors.New("từ chối khôi phục vì resources.assets hiện tại không phải bản do installer này tạo")
	}
	backupData, err := os.ReadFile(backup)
	if err != nil {
		return fmt.Errorf("không có backup hợp lệ: %w", err)
	}
	if sha256Bytes(backupData) != manifest.SourceSHA256 {
		return errors.New("backup sai hash; dùng Steam Verify integrity thay thế")
	}
	if err := writeAtomic(resources, backupData, 0o644); err != nil {
		return err
	}
	if err := os.Remove(backup); err != nil {
		return fmt.Errorf("đã khôi phục nhưng không xóa được backup: %w", err)
	}
	fmt.Println("Đã khôi phục resources.assets gốc.")
	return nil
}

func downloadBepInEx(client *http.Client) ([]byte, error) {
	request, err := http.NewRequest(http.MethodGet, bepInExURL, nil)
	if err != nil {
		return nil, err
	}
	request.Header.Set("User-Agent", "ValheimVietHoa-Installer/0.3.0")
	response, err := client.Do(request)
	if err != nil {
		return nil, err
	}
	defer response.Body.Close()
	if response.StatusCode != http.StatusOK {
		return nil, fmt.Errorf("Thunderstore trả về HTTP %d", response.StatusCode)
	}
	data, err := io.ReadAll(io.LimitReader(response.Body, 16<<20))
	if err != nil {
		return nil, err
	}
	if sha256Bytes(data) != bepInExSHA256 {
		return nil, errors.New("BepInExPack tải về sai SHA-256")
	}
	return data, nil
}

func extractBepInEx(gameDir string, archive []byte) error {
	reader, err := zip.NewReader(bytes.NewReader(archive), int64(len(archive)))
	if err != nil {
		return err
	}
	const prefix = "BepInExPack_Valheim/"
	cleanRoot, err := filepath.Abs(gameDir)
	if err != nil {
		return err
	}
	for _, item := range reader.File {
		if !strings.HasPrefix(item.Name, prefix) {
			continue
		}
		relative := strings.TrimPrefix(item.Name, prefix)
		if relative == "" {
			continue
		}
		target := filepath.Join(cleanRoot, filepath.FromSlash(relative))
		if target != cleanRoot && !strings.HasPrefix(target, cleanRoot+string(os.PathSeparator)) {
			return errors.New("BepInEx ZIP chứa đường dẫn không an toàn")
		}
		if item.FileInfo().IsDir() {
			if err := os.MkdirAll(target, 0o755); err != nil {
				return err
			}
			continue
		}
		stream, err := item.Open()
		if err != nil {
			return err
		}
		data, readErr := io.ReadAll(stream)
		closeErr := stream.Close()
		if readErr != nil {
			return readErr
		}
		if closeErr != nil {
			return closeErr
		}
		mode := fs.FileMode(0o644)
		if strings.HasSuffix(relative, ".sh") {
			mode = 0o755
		}
		if err := writeAtomic(target, data, mode); err != nil {
			return err
		}
	}
	return nil
}

var pluginFiles = map[string]string{
	"assets/ValheimVietnameseFont.dll":   "ValheimVietnameseFont.dll",
	"assets/ValheimVietHoa.json":         "Translations/Vietnamese/ValheimVietHoa.json",
	"assets/SVN-Norse Regular.otf":       "SVN-Norse Regular.otf",
	"assets/SVN-Norse Bold.otf":          "SVN-Norse Bold.otf",
	"assets/ValheimVN-Sans-Regular.ttf":  "ValheimVN-Sans-Regular.ttf",
	"assets/ValheimVN-Serif-Regular.ttf": "ValheimVN-Serif-Regular.ttf",
	"assets/OFL-Averia.txt":              "OFL-Averia.txt",
	"assets/OFL-Noto.txt":                "OFL-Noto.txt",
}

func installMod(gameDir string, assets fs.FS, client *http.Client) error {
	if _, err := resourcesPath(gameDir); err != nil {
		return err
	}
	if _, err := os.Stat(filepath.Join(gameDir, "BepInEx", "core", "BepInEx.dll")); errors.Is(err, os.ErrNotExist) {
		fmt.Println("Chưa có BepInEx; đang tải BepInExPack Valheim 5.4.2333 từ Thunderstore…")
		archive, err := downloadBepInEx(client)
		if err != nil {
			return err
		}
		if err := extractBepInEx(gameDir, archive); err != nil {
			return err
		}
	} else if err != nil {
		return fmt.Errorf("kiểm tra BepInEx: %w", err)
	}
	pluginDir := filepath.Join(gameDir, "BepInEx", "plugins", pluginDirName)
	for source, relative := range pluginFiles {
		data, err := fs.ReadFile(assets, source)
		if err != nil {
			return err
		}
		if err := writeAtomic(filepath.Join(pluginDir, filepath.FromSlash(relative)), data, 0o644); err != nil {
			return err
		}
	}
	fmt.Printf("Đã cài bản mod tối giản tại %s (BepInEx + 1 plugin, không Jötunn).\n", pluginDir)
	if runtime.GOOS != "windows" {
		fmt.Println("Hãy khởi chạy qua start_game_bepinex.sh; xem hướng dẫn release để đặt Steam Launch Options.")
	}
	return nil
}

func uninstallMod(gameDir string) error {
	pluginDir := filepath.Join(gameDir, "BepInEx", "plugins", pluginDirName)
	for _, relative := range pluginFiles {
		path := filepath.Join(pluginDir, filepath.FromSlash(relative))
		if err := os.Remove(path); err != nil && !errors.Is(err, os.ErrNotExist) {
			return err
		}
	}
	_ = os.Remove(filepath.Join(pluginDir, "Translations", "Vietnamese"))
	_ = os.Remove(filepath.Join(pluginDir, "Translations"))
	_ = os.Remove(pluginDir)
	fmt.Println("Đã gỡ riêng Valheim Việt Hóa; BepInEx và các mod khác được giữ nguyên.")
	return nil
}

func httpClient() *http.Client {
	return &http.Client{Timeout: 2 * time.Minute}
}

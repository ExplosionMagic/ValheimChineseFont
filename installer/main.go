package main

import (
	"bufio"
	"embed"
	"flag"
	"fmt"
	"io"
	"os"
	"strings"
)

//go:embed assets/*
var embeddedAssets embed.FS

func ask(reader *bufio.Reader, prompt string) (string, error) {
	fmt.Print(prompt)
	line, err := reader.ReadString('\n')
	if err != nil && err != io.EOF {
		return "", err
	}
	return strings.TrimSpace(line), nil
}

func chooseGameDir(reader *bufio.Reader, supplied string, assumeYes bool) (string, error) {
	if supplied != "" {
		return normalizeGameDir(supplied)
	}
	found := detectGameDirs()
	if len(found) == 1 && assumeYes {
		return found[0], nil
	}
	if len(found) > 0 {
		fmt.Println("Đã tự phát hiện:")
		for index, path := range found {
			fmt.Printf("  %d. %s\n", index+1, path)
		}
		answer, err := ask(reader, "Chọn số (Enter = 1), hoặc dán đường dẫn khác: ")
		if err != nil {
			return "", err
		}
		if answer == "" || answer == "1" {
			return found[0], nil
		}
		for index, path := range found {
			if answer == fmt.Sprint(index+1) {
				return path, nil
			}
		}
		return chooseGameDir(reader, answer, true)
	}
	answer, err := ask(reader, "Không tự tìm thấy Valheim. Dán đường dẫn thư mục game: ")
	if err != nil {
		return "", err
	}
	return chooseGameDir(reader, answer, true)
}

func chooseMethod(reader *bufio.Reader, method string) (string, error) {
	method = strings.ToLower(strings.TrimSpace(method))
	if method != "" {
		switch method {
		case "mod", "vanilla", "uninstall-mod", "uninstall-vanilla":
			return method, nil
		default:
			return "", fmt.Errorf("method không hợp lệ: %s", method)
		}
	}
	fmt.Println("Cách cài:")
	fmt.Println("  1. Mod tối giản — tự cài BepInEx nếu thiếu; không Jötunn (khuyên dùng)")
	fmt.Println("  2. Không mod — backup rồi vá trực tiếp resources.assets")
	fmt.Println("  3. Gỡ bản mod")
	fmt.Println("  4. Khôi phục resources.assets gốc")
	answer, err := ask(reader, "Chọn 1–4: ")
	if err != nil {
		return "", err
	}
	methods := map[string]string{"1": "mod", "2": "vanilla", "3": "uninstall-mod", "4": "uninstall-vanilla"}
	if selected, ok := methods[answer]; ok {
		return selected, nil
	}
	return "", fmt.Errorf("lựa chọn không hợp lệ: %s", answer)
}

func run() error {
	gameDirFlag := flag.String("game-dir", "", "đường dẫn thư mục Valheim")
	methodFlag := flag.String("method", "", "mod, vanilla, uninstall-mod, hoặc uninstall-vanilla")
	yesFlag := flag.Bool("yes", false, "dùng đường dẫn tự phát hiện mà không hỏi")
	flag.Parse()
	fmt.Println("Valheim Việt Hóa Installer 0.3.0")
	fmt.Println("Hãy thoát hẳn Valheim trước khi tiếp tục.")
	reader := bufio.NewReader(os.Stdin)
	gameDir, err := chooseGameDir(reader, *gameDirFlag, *yesFlag)
	if err != nil {
		return err
	}
	method, err := chooseMethod(reader, *methodFlag)
	if err != nil {
		return err
	}
	fmt.Printf("Thư mục game: %s\n", gameDir)
	switch method {
	case "mod":
		return installMod(gameDir, embeddedAssets, httpClient())
	case "vanilla":
		return installVanilla(gameDir, embeddedAssets)
	case "uninstall-mod":
		return uninstallMod(gameDir)
	case "uninstall-vanilla":
		return restoreVanilla(gameDir, embeddedAssets)
	}
	return nil
}

func main() {
	if err := run(); err != nil {
		fmt.Fprintf(os.Stderr, "Lỗi: %v\n", err)
		fmt.Fprintln(os.Stderr, "Installer không bao giờ vá resources.assets của build không đúng hash.")
		os.Exit(1)
	}
}

package main

import (
	"archive/zip"
	"bytes"
	"io/fs"
	"os"
	"path/filepath"
	"testing"
)

func TestSteamLibraries(t *testing.T) {
	input := `"libraryfolders" { "0" { "path" "C:\\Program Files (x86)\\Steam" } "1" { "path" "D:\\SteamLibrary" } }`
	got := steamLibraries(input)
	if len(got) != 2 {
		t.Fatalf("got %d paths: %#v", len(got), got)
	}
}

func TestDecodeOffset(t *testing.T) {
	positive := []byte{42, 0, 0, 0, 0, 0, 0, 0}
	negative := []byte{42, 0, 0, 0, 0, 0, 0, 0x80}
	if got, _ := decodeOffset(positive); got != 42 {
		t.Fatalf("positive: %d", got)
	}
	if got, _ := decodeOffset(negative); got != -42 {
		t.Fatalf("negative: %d", got)
	}
}

func TestExtractBepInExRejectsTraversal(t *testing.T) {
	var data bytes.Buffer
	writer := zip.NewWriter(&data)
	file, err := writer.Create("BepInExPack_Valheim/../../outside")
	if err != nil {
		t.Fatal(err)
	}
	_, _ = file.Write([]byte("bad"))
	if err := writer.Close(); err != nil {
		t.Fatal(err)
	}
	if err := extractBepInEx(t.TempDir(), data.Bytes()); err == nil {
		t.Fatal("expected traversal error")
	}
}

func TestWriteAtomic(t *testing.T) {
	path := filepath.Join(t.TempDir(), "nested", "file")
	if err := writeAtomic(path, []byte("ok"), 0o600); err != nil {
		t.Fatal(err)
	}
	data, err := os.ReadFile(path)
	if err != nil || string(data) != "ok" {
		t.Fatalf("read: %q, %v", data, err)
	}
	info, _ := os.Stat(path)
	if info.Mode().Perm() != fs.FileMode(0o600) {
		t.Fatalf("mode: %o", info.Mode().Perm())
	}
}

func TestNormalizeGameDirFromResourcesFile(t *testing.T) {
	root := t.TempDir()
	resources := filepath.Join(root, "Valheim", "valheim_Data", "resources.assets")
	if err := os.MkdirAll(filepath.Dir(resources), 0o755); err != nil {
		t.Fatal(err)
	}
	if err := os.WriteFile(resources, []byte("test"), 0o644); err != nil {
		t.Fatal(err)
	}
	got, err := normalizeGameDir(resources)
	if err != nil {
		t.Fatal(err)
	}
	if got != filepath.Join(root, "Valheim") {
		t.Fatalf("got %s", got)
	}
}

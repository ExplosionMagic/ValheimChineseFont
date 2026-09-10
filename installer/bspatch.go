package main

import (
	"bytes"
	"compress/bzip2"
	"errors"
	"fmt"
	"io"
)

func decodeOffset(buf []byte) (int64, error) {
	if len(buf) != 8 {
		return 0, errors.New("invalid bsdiff integer")
	}
	value := int64(buf[7] & 0x7f)
	for i := 6; i >= 0; i-- {
		value = value*256 + int64(buf[i])
	}
	if buf[7]&0x80 != 0 {
		value = -value
	}
	return value, nil
}

func readOffset(reader io.Reader) (int64, error) {
	buf := make([]byte, 8)
	if _, err := io.ReadFull(reader, buf); err != nil {
		return 0, err
	}
	return decodeOffset(buf)
}

func applyBSDiff(old, patch []byte) ([]byte, error) {
	if len(patch) < 32 || string(patch[:8]) != "BSDIFF40" {
		return nil, errors.New("invalid BSDIFF40 patch")
	}
	ctrlLen, err := decodeOffset(patch[8:16])
	if err != nil {
		return nil, err
	}
	diffLen, err := decodeOffset(patch[16:24])
	if err != nil {
		return nil, err
	}
	newSize, err := decodeOffset(patch[24:32])
	if err != nil {
		return nil, err
	}
	if ctrlLen < 0 || diffLen < 0 || newSize < 0 || newSize > 1<<30 || 32+ctrlLen+diffLen > int64(len(patch)) {
		return nil, errors.New("invalid bsdiff block sizes")
	}
	ctrl := bzip2.NewReader(bytes.NewReader(patch[32 : 32+ctrlLen]))
	diff := bzip2.NewReader(bytes.NewReader(patch[32+ctrlLen : 32+ctrlLen+diffLen]))
	extra := bzip2.NewReader(bytes.NewReader(patch[32+ctrlLen+diffLen:]))
	result := make([]byte, int(newSize))
	var oldPos, newPos int64
	for newPos < newSize {
		addLen, err := readOffset(ctrl)
		if err != nil {
			return nil, fmt.Errorf("read control add length: %w", err)
		}
		copyLen, err := readOffset(ctrl)
		if err != nil {
			return nil, fmt.Errorf("read control copy length: %w", err)
		}
		seekLen, err := readOffset(ctrl)
		if err != nil {
			return nil, fmt.Errorf("read control seek length: %w", err)
		}
		if addLen < 0 || copyLen < 0 || newPos+addLen > newSize || newPos+addLen+copyLen > newSize {
			return nil, errors.New("corrupt bsdiff control tuple")
		}
		if _, err := io.ReadFull(diff, result[newPos:newPos+addLen]); err != nil {
			return nil, fmt.Errorf("read diff block: %w", err)
		}
		for i := int64(0); i < addLen; i++ {
			if oldPos+i >= 0 && oldPos+i < int64(len(old)) {
				result[newPos+i] += old[oldPos+i]
			}
		}
		newPos += addLen
		oldPos += addLen
		if _, err := io.ReadFull(extra, result[newPos:newPos+copyLen]); err != nil {
			return nil, fmt.Errorf("read extra block: %w", err)
		}
		newPos += copyLen
		oldPos += seekLen
	}
	return result, nil
}

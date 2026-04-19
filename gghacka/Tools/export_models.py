"""
Download models cho sherpa-onnx (da pre-built san, chi can download).

Cach dung:
    pip install huggingface_hub
    python export_models.py

Sau khi xong, copy thu muc 'models/' vao Assets/StreamingAssets/
"""

import os
import sys
import subprocess


def ensure_huggingface_hub():
    try:
        import huggingface_hub
    except ImportError:
        print("[*] Cai dat huggingface_hub...")
        subprocess.check_call([sys.executable, "-m", "pip", "install", "huggingface_hub"])


def download_from_hf(repo_id, output_dir):
    """Download model tu HuggingFace."""
    from huggingface_hub import snapshot_download

    print(f"\n[*] Downloading {repo_id}...")
    snapshot_download(
        repo_id=repo_id,
        local_dir=output_dir,
        ignore_patterns=["*.md", ".gitattributes"],
    )
    print(f"[OK] -> {output_dir}")


def download_from_github(url, output_dir):
    """Download va extract tar.bz2 tu GitHub releases."""
    filename = url.split("/")[-1]
    dirname = filename.replace(".tar.bz2", "")

    if os.path.exists(output_dir):
        print(f"[SKIP] {output_dir} da ton tai")
        return

    print(f"\n[*] Downloading {filename}...")
    subprocess.check_call(["curl", "-SL", "-O", url])

    print(f"[*] Extracting...")
    subprocess.check_call(["tar", "xf", filename])
    os.remove(filename)

    if os.path.exists(dirname) and dirname != output_dir:
        os.rename(dirname, output_dir)

    print(f"[OK] -> {output_dir}")


def main():
    output_base = "models"
    os.makedirs(output_base, exist_ok=True)
    os.chdir(output_base)

    print("=" * 60)
    print("SHERPA-ONNX MODEL DOWNLOADER")
    print("Download pre-built models cho Unity + Quest 3")
    print("=" * 60)

    ensure_huggingface_hub()

    # -------------------------------------------------------
    # 1. Moonshine-base-vi (pre-built, ~135 MB)
    #    Format: .ort (OnnxRuntime optimized)
    #    Files: encoder_model.ort, decoder_model_merged.ort, tokens.txt
    # -------------------------------------------------------
    download_from_hf(
        repo_id="csukuangfj2/sherpa-onnx-moonshine-base-vi-quantized-2026-02-27",
        output_dir="moonshine-base-vi",
    )

    # -------------------------------------------------------
    # 2. Zipformer-vi-30M-int8 (pre-built, ~25 MB, nhe nhat)
    #    Format: .onnx
    #    Files: encoder.int8.onnx, decoder.onnx, joiner.int8.onnx, tokens.txt
    # -------------------------------------------------------
    download_from_github(
        url="https://github.com/k2-fsa/sherpa-onnx/releases/download/asr-models/sherpa-onnx-zipformer-vi-30M-int8-2026-02-09.tar.bz2",
        output_dir="zipformer-vi-30M",
    )

    # -------------------------------------------------------
    # 3. Zipformer-vi-int8 (pre-built, ~57 MB, chinh xac hon)
    #    Format: .onnx
    #    Files: encoder.int8.onnx, decoder.onnx, joiner.int8.onnx, tokens.txt
    # -------------------------------------------------------
    download_from_github(
        url="https://github.com/k2-fsa/sherpa-onnx/releases/download/asr-models/sherpa-onnx-zipformer-vi-int8-2025-04-20.tar.bz2",
        output_dir="zipformer-vi-full",
    )

    # -------------------------------------------------------
    # In ket qua
    # -------------------------------------------------------
    print("\n" + "=" * 60)
    print("XONG! 3 models da download:")
    print(f"  1. moonshine-base-vi/  (Moonshine v2, tieng Viet)")
    print(f"  2. zipformer-vi-30M/   (Zipformer 30M, nhe nhat)")
    print(f"  3. zipformer-vi-full/  (Zipformer day du, chinh xac hon)")
    print()
    print("BUOC TIEP THEO:")
    print(f"  Copy thu muc '{os.path.abspath('.')}' vao:")
    print(f"  Assets/StreamingAssets/models/")
    print("=" * 60)


if __name__ == "__main__":
    main()

# Huong dan setup Sherpa-ONNX cho Unity (Quest 3)

## Buoc 1: Download models

```bash
cd "D:\Unity Project\gghacka\Tools"
pip install huggingface_hub
python export_models.py
```

Script se download 3 models:
- moonshine-base-vi (Moonshine v2, ~135MB, nhanh + chinh xac)
- zipformer-vi-30M (Zipformer 30M, ~25MB, nhe nhat)
- zipformer-vi-full (Zipformer full, ~57MB, chinh xac hon)

## Buoc 2: Copy models vao Unity

Copy thu muc `Tools/models/` vao `Assets/StreamingAssets/models/`

Cau truc:
```
Assets/StreamingAssets/models/
  moonshine-base-vi/
    encoder_model.ort
    decoder_model_merged.ort
    tokens.txt
  zipformer-vi-30M/
    encoder.int8.onnx
    decoder.onnx
    joiner.int8.onnx
    tokens.txt
    bpe.model
  zipformer-vi-full/
    encoder-epoch-12-avg-8.int8.onnx
    decoder-epoch-12-avg-8.onnx
    joiner-epoch-12-avg-8.int8.onnx
    tokens.txt
    bpe.model
```

## Buoc 3: Cai sherpa-onnx Unity package

```bash
npm install -g openupm-cli
cd "D:\Unity Project\gghacka"
openupm add com.ponyudev.sherpa-onnx
```

Hoac them thu cong vao Packages/manifest.json:
```json
{
  "scopedRegistries": [
    {
      "name": "OpenUPM",
      "url": "https://package.openupm.com",
      "scopes": ["com.ponyudev"]
    }
  ],
  "dependencies": {
    "com.ponyudev.sherpa-onnx": "1.12.35"
  }
}
```

## Buoc 4: Uncomment code trong SherpaSTTTest.cs

Mo file Assets/Scripts/SherpaSTTTest.cs va uncomment phan sherpa-onnx code
(da danh dau ro trong file).

## Buoc 5: Test

1. Tao empty GameObject trong Scene
2. Gan component SherpaSTTTest
3. Chon model bang 3 nut: Zip30M | ZipFull | Moonshine
4. Nhan Record > noi > Stop > xem ket qua + thoi gian

## Luu y cho Quest 3

- Script tu dong copy models tu StreamingAssets sang persistentDataPath
- Lan dau chay co the mat vai giay de copy
- Quest 3 khong ho tro CUDA, chi chay CPU
- Model nhe (Zipformer 30M) se chay nhanh nhat tren Quest 3

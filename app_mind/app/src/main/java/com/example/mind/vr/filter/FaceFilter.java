package com.example.mind.vr.filter;

import com.example.mind.R;

/**
 * Định nghĩa các filter preset.
 * Mỗi filter gồm các sticker đặt tại vị trí landmark trên khuôn mặt.
 */
public class FaceFilter {

    public static final int NONE = -1;

    /** Vị trí đặt sticker dựa trên face landmark index */
    public static class Sticker {
        public final int drawableRes;   // icon resource
        public final int landmarkIndex; // MediaPipe landmark index (0-477)
        public final float scale;       // kích thước tương đối so với khuôn mặt
        public final float offsetX;     // offset X (tỷ lệ)
        public final float offsetY;     // offset Y (tỷ lệ)

        public Sticker(int drawableRes, int landmarkIndex, float scale, float offsetX, float offsetY) {
            this.drawableRes = drawableRes;
            this.landmarkIndex = landmarkIndex;
            this.scale = scale;
            this.offsetX = offsetX;
            this.offsetY = offsetY;
        }
    }

    /** Preset filter: các bộ sticker khác nhau */
    public static class Preset {
        public final String name;
        public final int thumbnailRes;  // icon hiển thị ở filter selector
        public final Sticker[] stickers;

        public Preset(String name, int thumbnailRes, Sticker... stickers) {
            this.name = name;
            this.thumbnailRes = thumbnailRes;
            this.stickers = stickers;
        }
    }

    // Landmark index references:
    // 10  = trán (forehead top)
    // 1   = mũi (nose tip)
    // 234 = má trái
    // 454 = má phải
    // 152 = cằm
    // 33  = mắt trái (outer)
    // 263 = mắt phải (outer)
    // 127 = tai trái
    // 356 = tai phải

    public static final Preset[] PRESETS = {
            // Filter 1: Monster Crown — quái vật trên đầu
            new Preset("Crown", R.drawable.icon_0,
                    new Sticker(R.drawable.icon_0, 10, 2.0f, 0f, -0.8f)  // trên trán
            ),

            // Filter 2: Cheek Buddies — quái vật 2 bên má
            new Preset("Buddies", R.drawable.icon_note_3,
                    new Sticker(R.drawable.icon_note_3, 234, 0.9f, -0.25f, 0f),  // má trái
                    new Sticker(R.drawable.icon_note_4, 454, 0.9f, 0.25f, 0f)    // má phải
            ),

            // Filter 3: Monster Hat — quái vật đội đầu + tai
            new Preset("Hat", R.drawable.icon_1,
                    new Sticker(R.drawable.icon_1, 10, 1.8f, 0f, -0.75f),   // trên đầu
                    new Sticker(R.drawable.icon_38, 127, 0.7f, -0.15f, 0f), // tai trái
                    new Sticker(R.drawable.icon_39, 356, 0.7f, 0.15f, 0f)   // tai phải
            ),

            // Filter 4: Nose Pet — quái vật trên mũi
            new Preset("Nose", R.drawable.icon_note_6,
                    new Sticker(R.drawable.icon_note_6, 1, 1.0f, 0f, -0.05f)  // mũi
            ),

            // Filter 5: Full Party — nhiều quái vật xung quanh
            new Preset("Party", R.drawable.icon_2,
                    new Sticker(R.drawable.icon_2, 10, 1.6f, 0f, -0.7f),       // đầu
                    new Sticker(R.drawable.icon_note_7, 234, 0.75f, -0.3f, 0f), // má trái
                    new Sticker(R.drawable.icon_note_8, 454, 0.75f, 0.3f, 0f),  // má phải
                    new Sticker(R.drawable.icon_40, 152, 0.7f, 0f, 0.2f)        // cằm
            ),
    };
}

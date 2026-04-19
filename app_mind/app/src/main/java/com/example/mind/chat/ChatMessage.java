package com.example.mind.chat;

public class ChatMessage {
    public static final int TYPE_MINDY = 0;
    public static final int TYPE_USER = 1;
    public static final int TYPE_MOOD_PICKER = 2;

    public final String text;
    public final int type;
    public int selectedMood = -1; // for TYPE_MOOD_PICKER: -1 = chưa chọn

    public ChatMessage(String text, int type) {
        this.text = text;
        this.type = type;
    }
}

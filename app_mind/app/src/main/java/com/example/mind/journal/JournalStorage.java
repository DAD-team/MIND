package com.example.mind.journal;

import android.content.Context;

import org.json.JSONArray;
import org.json.JSONException;
import org.json.JSONObject;

import java.io.BufferedReader;
import java.io.FileInputStream;
import java.io.FileOutputStream;
import java.io.InputStreamReader;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.List;

public class JournalStorage {

    private static final String FILE_NAME = "journals.json";

    public static class Entry {
        public long id;
        public String title;
        public String content;
        public long timestamp; // millis
        public int iconIndex;  // index into NOTE_ICONS array

        public Entry(long id, String title, String content, long timestamp, int iconIndex) {
            this.id = id;
            this.title = title;
            this.content = content;
            this.timestamp = timestamp;
            this.iconIndex = iconIndex;
        }

        JSONObject toJson() throws JSONException {
            JSONObject obj = new JSONObject();
            obj.put("id", id);
            obj.put("title", title);
            obj.put("content", content);
            obj.put("timestamp", timestamp);
            obj.put("iconIndex", iconIndex);
            return obj;
        }

        static Entry fromJson(JSONObject obj) throws JSONException {
            return new Entry(
                    obj.getLong("id"),
                    obj.getString("title"),
                    obj.getString("content"),
                    obj.getLong("timestamp"),
                    obj.getInt("iconIndex")
            );
        }
    }

    public static List<Entry> loadEntries(Context context) {
        List<Entry> entries = new ArrayList<>();
        try {
            FileInputStream fis = context.openFileInput(FILE_NAME);
            BufferedReader reader = new BufferedReader(new InputStreamReader(fis, StandardCharsets.UTF_8));
            StringBuilder sb = new StringBuilder();
            String line;
            while ((line = reader.readLine()) != null) {
                sb.append(line);
            }
            reader.close();

            JSONArray arr = new JSONArray(sb.toString());
            for (int i = 0; i < arr.length(); i++) {
                entries.add(Entry.fromJson(arr.getJSONObject(i)));
            }
        } catch (Exception e) {
            // File doesn't exist yet or parse error — return empty list
        }
        return entries;
    }

    public static void saveEntries(Context context, List<Entry> entries) {
        try {
            JSONArray arr = new JSONArray();
            for (Entry entry : entries) {
                arr.put(entry.toJson());
            }
            FileOutputStream fos = context.openFileOutput(FILE_NAME, Context.MODE_PRIVATE);
            fos.write(arr.toString().getBytes(StandardCharsets.UTF_8));
            fos.close();
        } catch (Exception e) {
            e.printStackTrace();
        }
    }

    public static void addEntry(Context context, String title, String content) {
        List<Entry> entries = loadEntries(context);
        long id = System.currentTimeMillis();
        int iconIndex = (int) (Math.abs(id * 2654435761L) % JournalActivity.NOTE_ICONS.length);
        entries.add(0, new Entry(id, title, content, id, iconIndex));
        saveEntries(context, entries);
    }

    public static void deleteEntry(Context context, long id) {
        List<Entry> entries = loadEntries(context);
        entries.removeIf(e -> e.id == id);
        saveEntries(context, entries);
    }
}

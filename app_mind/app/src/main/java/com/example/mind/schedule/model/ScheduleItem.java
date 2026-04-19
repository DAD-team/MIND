package com.example.mind.schedule.model;

import org.json.JSONObject;

/**
 * Một môn học trong lịch tuần.
 */
public class ScheduleItem {
    public String id;          // UUID từ server
    public String subject;     // Tên môn
    public String room;        // Phòng học
    public int dayOfWeek;      // 0=T2, 1=T3, 2=T4, 3=T5, 4=T6, 5=T7, 6=CN
    public String startTime;   // "07:30"
    public String endTime;     // "09:30"
    public int eventType;      // 0=Học thường, 1=Thi, 2=Deadline đồ án, 3=Nộp bài, 4=Thuyết trình

    public ScheduleItem() {}

    public ScheduleItem(String subject, String room, int dayOfWeek, String startTime, String endTime) {
        this(subject, room, dayOfWeek, startTime, endTime, 0);
    }

    public ScheduleItem(String subject, String room, int dayOfWeek, String startTime, String endTime, int eventType) {
        this.subject = subject;
        this.room = room;
        this.dayOfWeek = dayOfWeek;
        this.startTime = startTime;
        this.endTime = endTime;
        this.eventType = eventType;
    }

    /** Parse từ JSON response của API */
    public static ScheduleItem fromJson(JSONObject obj) {
        ScheduleItem item = new ScheduleItem();
        item.id = obj.optString("id", null);
        item.subject = obj.optString("subject", "");
        item.room = obj.optString("room", "");
        item.dayOfWeek = obj.optInt("day_of_week", 0);
        item.startTime = obj.optString("start_time", "");
        item.endTime = obj.optString("end_time", "");
        item.eventType = obj.optInt("event_type", 0);
        return item;
    }

    /** Tạo JSON body cho POST/PATCH request */
    public JSONObject toCreateJson() {
        JSONObject obj = new JSONObject();
        try {
            obj.put("subject", subject);
            obj.put("day_of_week", dayOfWeek);
            obj.put("start_time", startTime);
            obj.put("end_time", endTime);
            if (room != null && !room.isEmpty()) {
                obj.put("room", room);
            }
            obj.put("event_type", eventType);
        } catch (Exception ignored) {}
        return obj;
    }

    // Event types for Tier 1 Signal 3 (Academic Pressure)
    public static final int EVENT_NORMAL = 0;
    public static final int EVENT_EXAM = 1;
    public static final int EVENT_PROJECT_DEADLINE = 2;
    public static final int EVENT_ASSIGNMENT = 3;
    public static final int EVENT_PRESENTATION = 4;

    public static final String[] EVENT_TYPE_NAMES = {
            "Học thường", "Thi", "Deadline đồ án", "Nộp bài tập", "Thuyết trình"
    };

    /** Trọng số theo tài liệu: Thi=3, Deadline=2, Nộp bài=1, Thuyết trình=1, Học thường=0 */
    public static final int[] EVENT_WEIGHTS = { 0, 3, 2, 1, 1 };

    public String getEventTypeName() {
        if (eventType >= 0 && eventType < EVENT_TYPE_NAMES.length) {
            return EVENT_TYPE_NAMES[eventType];
        }
        return EVENT_TYPE_NAMES[0];
    }

    public int getEventWeight() {
        if (eventType >= 0 && eventType < EVENT_WEIGHTS.length) {
            return EVENT_WEIGHTS[eventType];
        }
        return 0;
    }

    public static final String[] DAY_NAMES = {
            "Thứ 2", "Thứ 3", "Thứ 4", "Thứ 5", "Thứ 6", "Thứ 7", "Chủ nhật"
    };

    public String getDayName() {
        if (dayOfWeek >= 0 && dayOfWeek < DAY_NAMES.length) {
            return DAY_NAMES[dayOfWeek];
        }
        return "";
    }
}

package com.example.mind.schedule;

import android.content.Context;

import com.example.mind.auth.ApiClient;
import com.example.mind.schedule.model.ScheduleItem;

import org.json.JSONArray;
import org.json.JSONObject;

import java.util.ArrayList;
import java.util.List;

/**
 * Quản lý lịch học qua API backend.
 */
public class ScheduleStorage {

    public interface ScheduleListCallback {
        void onSuccess(List<ScheduleItem> items);
        void onFailure(String error);
    }

    public interface ScheduleItemCallback {
        void onSuccess(ScheduleItem item);
        void onFailure(String error);
    }

    public interface DeleteCallback {
        void onSuccess();
        void onFailure(String error);
    }

    /** GET /schedules — lấy toàn bộ lịch học của user */
    public static void load(Context context, ScheduleListCallback callback) {
        ApiClient api = new ApiClient(context);
        api.getSchedules(new ApiClient.ApiCallback() {
            @Override
            public void onSuccess(String responseBody) {
                try {
                    JSONObject root = new JSONObject(responseBody);
                    JSONArray arr = root.getJSONArray("schedules");
                    List<ScheduleItem> items = new ArrayList<>();
                    for (int i = 0; i < arr.length(); i++) {
                        items.add(ScheduleItem.fromJson(arr.getJSONObject(i)));
                    }
                    callback.onSuccess(items);
                } catch (Exception e) {
                    callback.onFailure("Parse error: " + e.getMessage());
                }
            }

            @Override
            public void onFailure(String error) {
                callback.onFailure(error);
            }
        });
    }

    /** POST /schedules — tạo lịch học mới */
    public static void addItem(Context context, ScheduleItem item, ScheduleItemCallback callback) {
        ApiClient api = new ApiClient(context);
        String json = item.toCreateJson().toString();
        api.createSchedule(json, new ApiClient.ApiCallback() {
            @Override
            public void onSuccess(String responseBody) {
                try {
                    ScheduleItem created = ScheduleItem.fromJson(new JSONObject(responseBody));
                    callback.onSuccess(created);
                } catch (Exception e) {
                    callback.onFailure("Parse error: " + e.getMessage());
                }
            }

            @Override
            public void onFailure(String error) {
                callback.onFailure(error);
            }
        });
    }

    /** PATCH /schedules/{id} — cập nhật lịch học */
    public static void updateItem(Context context, ScheduleItem item, ScheduleItemCallback callback) {
        if (item.id == null) {
            callback.onFailure("Không có ID để cập nhật");
            return;
        }
        ApiClient api = new ApiClient(context);
        String json = item.toCreateJson().toString();
        api.updateSchedule(item.id, json, new ApiClient.ApiCallback() {
            @Override
            public void onSuccess(String responseBody) {
                try {
                    ScheduleItem updated = ScheduleItem.fromJson(new JSONObject(responseBody));
                    callback.onSuccess(updated);
                } catch (Exception e) {
                    callback.onFailure("Parse error: " + e.getMessage());
                }
            }

            @Override
            public void onFailure(String error) {
                callback.onFailure(error);
            }
        });
    }

    /** DELETE /schedules/{id} — xoá lịch học */
    public static void deleteItem(Context context, ScheduleItem item, DeleteCallback callback) {
        if (item.id == null) {
            callback.onFailure("Không có ID để xoá");
            return;
        }
        ApiClient api = new ApiClient(context);
        api.deleteSchedule(item.id, new ApiClient.ApiCallback() {
            @Override
            public void onSuccess(String responseBody) {
                callback.onSuccess();
            }

            @Override
            public void onFailure(String error) {
                callback.onFailure(error);
            }
        });
    }
}

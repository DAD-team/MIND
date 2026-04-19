package com.example.mind.auth;

import android.content.Context;
import android.util.Log;

import java.io.File;
import java.io.IOException;
import java.util.concurrent.TimeUnit;

import okhttp3.Call;
import okhttp3.Callback;
import okhttp3.MediaType;
import okhttp3.MultipartBody;
import okhttp3.OkHttpClient;
import okhttp3.Request;
import okhttp3.RequestBody;
import okhttp3.Response;

/**
 * HTTP client gọi API backend.
 * Base URL đọc động từ Firebase RTDB (server_info/tunnel_url).
 * Header: Authorization: Bearer <Firebase ID Token>
 *
 * Endpoints theo API_REFERENCE.md
 */
public class ApiClient {

    private static final String TAG = "ApiClient";

    private final OkHttpClient client;
    private final OkHttpClient uploadClient;
    private final TokenManager tokenManager;

    public ApiClient(Context context) {
        this.client = new OkHttpClient.Builder()
                .connectTimeout(30, TimeUnit.SECONDS)
                .readTimeout(30, TimeUnit.SECONDS)
                .build();

        this.uploadClient = new OkHttpClient.Builder()
                .connectTimeout(30, TimeUnit.SECONDS)
                .writeTimeout(120, TimeUnit.SECONDS)
                .readTimeout(300, TimeUnit.SECONDS)
                .build();

        this.tokenManager = new TokenManager(context);
    }

    // Helper: lấy base URL + token rồi gọi callback
    private void withUrlAndToken(UrlTokenCallback cb) {
        ApiConfig.getBaseUrl(new ApiConfig.Callback() {
            @Override
            public void onReady(String baseUrl) {
                tokenManager.getValidToken(new TokenManager.TokenCallback() {
                    @Override
                    public void onToken(String token) {
                        cb.onReady(baseUrl, token);
                    }

                    @Override
                    public void onError(String error) {
                        cb.onError("Token error: " + error);
                    }
                });
            }

            @Override
            public void onError(String error) {
                cb.onError("URL error: " + error);
            }
        });
    }

    private interface UrlTokenCallback {
        void onReady(String baseUrl, String token);
        default void onError(String error) {
            Log.e("ApiClient", error);
        }
    }

    // ──────────────────────────────────────────────
    // AUTH: GET /auth/me
    // ──────────────────────────────────────────────

    public void getOrCreateProfile(ApiCallback callback) {
        withUrlAndToken(new UrlTokenCallback() {
            @Override
            public void onReady(String baseUrl, String token) {
                Request request = new Request.Builder()
                        .url(baseUrl + "/auth/me")
                        .addHeader("Authorization", "Bearer " + token)
                        .get()
                        .build();
                executeAsync(client, request, callback);
            }

            @Override
            public void onError(String error) {
                callback.onFailure(error);
            }
        });
    }

    // ──────────────────────────────────────────────
    // VIDEOS: POST /videos/upload
    // ──────────────────────────────────────────────

    public void uploadVideo(File videoFile, ApiCallback callback) {
        withUrlAndToken(new UrlTokenCallback() {
            @Override
            public void onReady(String baseUrl, String token) {
                RequestBody fileBody = RequestBody.create(
                        videoFile, MediaType.parse("video/mp4"));
                RequestBody multipart = new MultipartBody.Builder()
                        .setType(MultipartBody.FORM)
                        .addFormDataPart("file", videoFile.getName(), fileBody)
                        .build();
                Request request = new Request.Builder()
                        .url(baseUrl + "/videos/upload")
                        .addHeader("Authorization", "Bearer " + token)
                        .post(multipart)
                        .build();
                executeAsync(uploadClient, request, callback);
            }

            @Override
            public void onError(String error) {
                callback.onFailure(error);
            }
        });
    }

    // ──────────────────────────────────────────────
    // VIDEOS: GET /videos/analysis/{video_id}
    // ──────────────────────────────────────────────

    public void getVideoAnalysis(String videoId, ApiCallback callback) {
        withUrlAndToken(new UrlTokenCallback() {
            @Override
            public void onReady(String baseUrl, String token) {
                Request request = new Request.Builder()
                        .url(baseUrl + "/videos/analysis/" + videoId)
                        .addHeader("Authorization", "Bearer " + token)
                        .get()
                        .build();
                executeAsync(client, request, callback);
            }

            @Override
            public void onError(String error) {
                callback.onFailure(error);
            }
        });
    }

    // ──────────────────────────────────────────────
    // HISTORY: GET /history/me
    // ──────────────────────────────────────────────

    public void getHistory(int limit, ApiCallback callback) {
        withUrlAndToken(new UrlTokenCallback() {
            @Override
            public void onReady(String baseUrl, String token) {
                Request request = new Request.Builder()
                        .url(baseUrl + "/history/me?limit=" + limit)
                        .addHeader("Authorization", "Bearer " + token)
                        .get()
                        .build();
                executeAsync(client, request, callback);
            }

            @Override
            public void onError(String error) {
                callback.onFailure(error);
            }
        });
    }

    public void getVideoDetail(String videoId, ApiCallback callback) {
        withUrlAndToken(new UrlTokenCallback() {
            @Override
            public void onReady(String baseUrl, String token) {
                Request request = new Request.Builder()
                        .url(baseUrl + "/history/me/" + videoId)
                        .addHeader("Authorization", "Bearer " + token)
                        .get()
                        .build();
                executeAsync(client, request, callback);
            }

            @Override
            public void onError(String error) {
                callback.onFailure(error);
            }
        });
    }

    // ──────────────────────────────────────────────
    // SCHEDULES: CRUD /schedules
    // ──────────────────────────────────────────────

    public void getSchedules(ApiCallback callback) {
        withUrlAndToken(new UrlTokenCallback() {
            @Override
            public void onReady(String baseUrl, String token) {
                Request request = new Request.Builder()
                        .url(baseUrl + "/schedules")
                        .addHeader("Authorization", "Bearer " + token)
                        .get()
                        .build();
                executeAsync(client, request, callback);
            }

            @Override
            public void onError(String error) {
                callback.onFailure(error);
            }
        });
    }

    public void createSchedule(String jsonBody, ApiCallback callback) {
        withUrlAndToken(new UrlTokenCallback() {
            @Override
            public void onReady(String baseUrl, String token) {
                RequestBody body = RequestBody.create(
                        jsonBody, MediaType.parse("application/json"));
                Request request = new Request.Builder()
                        .url(baseUrl + "/schedules")
                        .addHeader("Authorization", "Bearer " + token)
                        .post(body)
                        .build();
                executeAsync(client, request, callback);
            }

            @Override
            public void onError(String error) {
                callback.onFailure(error);
            }
        });
    }

    public void updateSchedule(String id, String jsonBody, ApiCallback callback) {
        withUrlAndToken(new UrlTokenCallback() {
            @Override
            public void onReady(String baseUrl, String token) {
                RequestBody body = RequestBody.create(
                        jsonBody, MediaType.parse("application/json"));
                Request request = new Request.Builder()
                        .url(baseUrl + "/schedules/" + id)
                        .addHeader("Authorization", "Bearer " + token)
                        .patch(body)
                        .build();
                executeAsync(client, request, callback);
            }

            @Override
            public void onError(String error) {
                callback.onFailure(error);
            }
        });
    }

    public void deleteSchedule(String id, ApiCallback callback) {
        withUrlAndToken(new UrlTokenCallback() {
            @Override
            public void onReady(String baseUrl, String token) {
                Request request = new Request.Builder()
                        .url(baseUrl + "/schedules/" + id)
                        .addHeader("Authorization", "Bearer " + token)
                        .delete()
                        .build();
                executeAsync(client, request, callback);
            }

            @Override
            public void onError(String error) {
                callback.onFailure(error);
            }
        });
    }

    public void getUpcomingEvents(int days, ApiCallback callback) {
        withUrlAndToken(new UrlTokenCallback() {
            @Override
            public void onReady(String baseUrl, String token) {
                Request request = new Request.Builder()
                        .url(baseUrl + "/schedules/upcoming?days=" + days)
                        .addHeader("Authorization", "Bearer " + token)
                        .get()
                        .build();
                executeAsync(client, request, callback);
            }

            @Override
            public void onError(String error) {
                callback.onFailure(error);
            }
        });
    }

    // ──────────────────────────────────────────────
    // NOTIFICATIONS: PUT/DELETE /notifications/fcm-token
    // ──────────────────────────────────────────────

    public void registerFcmToken(String fcmToken, int consentLevel, ApiCallback callback) {
        withUrlAndToken(new UrlTokenCallback() {
            @Override
            public void onReady(String baseUrl, String token) {
                String json = "{\"fcm_token\":\"" + fcmToken + "\",\"consent_level\":" + consentLevel + "}";
                RequestBody body = RequestBody.create(
                        json, MediaType.parse("application/json"));
                Request request = new Request.Builder()
                        .url(baseUrl + "/notifications/fcm-token")
                        .addHeader("Authorization", "Bearer " + token)
                        .put(body)
                        .build();
                executeAsync(client, request, callback);
            }

            @Override
            public void onError(String error) {
                callback.onFailure(error);
            }
        });
    }

    public void unregisterFcmToken(ApiCallback callback) {
        withUrlAndToken(new UrlTokenCallback() {
            @Override
            public void onReady(String baseUrl, String token) {
                Request request = new Request.Builder()
                        .url(baseUrl + "/notifications/fcm-token")
                        .addHeader("Authorization", "Bearer " + token)
                        .delete()
                        .build();
                executeAsync(client, request, callback);
            }

            @Override
            public void onError(String error) {
                callback.onFailure(error);
            }
        });
    }

    // ──────────────────────────────────────────────
    // PHQ: POST /phq/submit + GET /phq/history + GET /phq/pending
    //      + POST /phq/reject + POST /phq/defer-phq9
    // ──────────────────────────────────────────────

    /** Kiểm tra pending PHQ (gọi mỗi khi app vào foreground). */
    public void getPhqPending(ApiCallback callback) {
        withUrlAndToken(new UrlTokenCallback() {
            @Override
            public void onReady(String baseUrl, String token) {
                Request request = new Request.Builder()
                        .url(baseUrl + "/phq/pending")
                        .addHeader("Authorization", "Bearer " + token)
                        .get()
                        .build();
                executeAsync(client, request, callback);
            }

            @Override
            public void onError(String error) {
                callback.onFailure(error);
            }
        });
    }

    /** User từ chối trả lời PHQ. Backend tăng rejection_count; sau 3 lần tự tạm dừng. */
    public void rejectPhq(String phqType, ApiCallback callback) {
        withUrlAndToken(new UrlTokenCallback() {
            @Override
            public void onReady(String baseUrl, String token) {
                String json = "{\"phq_type\":\"" + phqType + "\"}";
                RequestBody body = RequestBody.create(
                        json, MediaType.parse("application/json"));
                Request request = new Request.Builder()
                        .url(baseUrl + "/phq/reject")
                        .addHeader("Authorization", "Bearer " + token)
                        .post(body)
                        .build();
                executeAsync(client, request, callback);
            }

            @Override
            public void onError(String error) {
                callback.onFailure(error);
            }
        });
    }

    /** User hoãn PHQ-9 sau khi hoàn thành PHQ-2 escalation. phq2Id có thể null. */
    public void deferPhq9(String phq2Id, ApiCallback callback) {
        withUrlAndToken(new UrlTokenCallback() {
            @Override
            public void onReady(String baseUrl, String token) {
                String json = phq2Id != null
                        ? "{\"phq2_id\":\"" + phq2Id + "\"}"
                        : "{}";
                RequestBody body = RequestBody.create(
                        json, MediaType.parse("application/json"));
                Request request = new Request.Builder()
                        .url(baseUrl + "/phq/defer-phq9")
                        .addHeader("Authorization", "Bearer " + token)
                        .post(body)
                        .build();
                executeAsync(client, request, callback);
            }

            @Override
            public void onError(String error) {
                callback.onFailure(error);
            }
        });
    }

    public void submitPhqResult(String jsonBody, ApiCallback callback) {
        withUrlAndToken(new UrlTokenCallback() {
            @Override
            public void onReady(String baseUrl, String token) {
                RequestBody body = RequestBody.create(
                        jsonBody, MediaType.parse("application/json"));
                Request request = new Request.Builder()
                        .url(baseUrl + "/phq/submit")
                        .addHeader("Authorization", "Bearer " + token)
                        .post(body)
                        .build();
                executeAsync(client, request, callback);
            }

            @Override
            public void onError(String error) {
                callback.onFailure(error);
            }
        });
    }

    public void getPhqHistory(String type, int limit, ApiCallback callback) {
        withUrlAndToken(new UrlTokenCallback() {
            @Override
            public void onReady(String baseUrl, String token) {
                Request request = new Request.Builder()
                        .url(baseUrl + "/phq/history?type=" + type + "&limit=" + limit)
                        .addHeader("Authorization", "Bearer " + token)
                        .get()
                        .build();
                executeAsync(client, request, callback);
            }

            @Override
            public void onError(String error) {
                callback.onFailure(error);
            }
        });
    }

    // ──────────────────────────────────────────────
    // MOOD: POST /mood/checkin
    // ──────────────────────────────────────────────

    public void submitMoodCheckin(String jsonBody, ApiCallback callback) {
        withUrlAndToken(new UrlTokenCallback() {
            @Override
            public void onReady(String baseUrl, String token) {
                RequestBody body = RequestBody.create(
                        jsonBody, MediaType.parse("application/json"));
                Request request = new Request.Builder()
                        .url(baseUrl + "/mood/checkin")
                        .addHeader("Authorization", "Bearer " + token)
                        .post(body)
                        .build();
                executeAsync(client, request, callback);
            }

            @Override
            public void onError(String error) {
                callback.onFailure(error);
            }
        });
    }

    // ──────────────────────────────────────────────
    // SAFETY: POST /safety-event
    // ──────────────────────────────────────────────

    public void submitSafetyEvent(String jsonBody, ApiCallback callback) {
        withUrlAndToken(new UrlTokenCallback() {
            @Override
            public void onReady(String baseUrl, String token) {
                RequestBody body = RequestBody.create(
                        jsonBody, MediaType.parse("application/json"));
                Request request = new Request.Builder()
                        .url(baseUrl + "/safety-event")
                        .addHeader("Authorization", "Bearer " + token)
                        .post(body)
                        .build();
                executeAsync(client, request, callback);
            }

            @Override
            public void onError(String error) {
                callback.onFailure(error);
            }
        });
    }

    // ──────────────────────────────────────────────
    // MONITORING: PUT /monitoring/update + GET /monitoring/status
    // ──────────────────────────────────────────────

    public void updateMonitoringLevel(String jsonBody, ApiCallback callback) {
        withUrlAndToken(new UrlTokenCallback() {
            @Override
            public void onReady(String baseUrl, String token) {
                RequestBody body = RequestBody.create(
                        jsonBody, MediaType.parse("application/json"));
                Request request = new Request.Builder()
                        .url(baseUrl + "/monitoring/update")
                        .addHeader("Authorization", "Bearer " + token)
                        .put(body)
                        .build();
                executeAsync(client, request, callback);
            }

            @Override
            public void onError(String error) {
                callback.onFailure(error);
            }
        });
    }

    /** Cập nhật monitoring level bằng int (convenience) */
    public void updateMonitoringLevel(int level, ApiCallback callback) {
        updateMonitoringLevel("{\"level\":" + level + "}", callback);
    }

    /** Gửi nhật ký Scout mỗi chu kỳ 2 giờ */
    public void submitScoutLog(String jsonBody, ApiCallback callback) {
        withUrlAndToken(new UrlTokenCallback() {
            @Override
            public void onReady(String baseUrl, String token) {
                RequestBody body = RequestBody.create(
                        jsonBody, MediaType.parse("application/json"));
                Request request = new Request.Builder()
                        .url(baseUrl + "/monitoring/scout-log")
                        .addHeader("Authorization", "Bearer " + token)
                        .post(body)
                        .build();
                executeAsync(client, request, callback);
            }

            @Override
            public void onError(String error) {
                callback.onFailure(error);
            }
        });
    }

    public void getMonitoringStatus(ApiCallback callback) {
        withUrlAndToken(new UrlTokenCallback() {
            @Override
            public void onReady(String baseUrl, String token) {
                Request request = new Request.Builder()
                        .url(baseUrl + "/monitoring/status")
                        .addHeader("Authorization", "Bearer " + token)
                        .get()
                        .build();
                executeAsync(client, request, callback);
            }

            @Override
            public void onError(String error) {
                callback.onFailure(error);
            }
        });
    }

    // ──────────────────────────────────────────────
    // USAGE: POST /usage/session
    // ──────────────────────────────────────────────

    public void submitUsageSession(String jsonBody, ApiCallback callback) {
        withUrlAndToken(new UrlTokenCallback() {
            @Override
            public void onReady(String baseUrl, String token) {
                RequestBody body = RequestBody.create(
                        jsonBody, MediaType.parse("application/json"));
                Request request = new Request.Builder()
                        .url(baseUrl + "/usage/session")
                        .addHeader("Authorization", "Bearer " + token)
                        .post(body)
                        .build();
                executeAsync(client, request, callback);
            }

            @Override
            public void onError(String error) {
                callback.onFailure(error);
            }
        });
    }

    // ──────────────────────────────────────────────
    // CHAT: POST /chat/interaction
    // ──────────────────────────────────────────────

    public void submitChatInteraction(String jsonBody, ApiCallback callback) {
        withUrlAndToken(new UrlTokenCallback() {
            @Override
            public void onReady(String baseUrl, String token) {
                RequestBody body = RequestBody.create(
                        jsonBody, MediaType.parse("application/json"));
                Request request = new Request.Builder()
                        .url(baseUrl + "/chat/interaction")
                        .addHeader("Authorization", "Bearer " + token)
                        .post(body)
                        .build();
                executeAsync(client, request, callback);
            }

            @Override
            public void onError(String error) {
                callback.onFailure(error);
            }
        });
    }

    // ──────────────────────────────────────────────
    // JOURNALS: CRUD /journals
    // ──────────────────────────────────────────────

    public void getJournals(int limit, ApiCallback callback) {
        withUrlAndToken(new UrlTokenCallback() {
            @Override
            public void onReady(String baseUrl, String token) {
                Request request = new Request.Builder()
                        .url(baseUrl + "/journals?limit=" + limit)
                        .addHeader("Authorization", "Bearer " + token)
                        .get()
                        .build();
                executeAsync(client, request, callback);
            }

            @Override
            public void onError(String error) {
                callback.onFailure(error);
            }
        });
    }

    public void createJournal(String jsonBody, ApiCallback callback) {
        withUrlAndToken(new UrlTokenCallback() {
            @Override
            public void onReady(String baseUrl, String token) {
                RequestBody body = RequestBody.create(
                        jsonBody, MediaType.parse("application/json"));
                Request request = new Request.Builder()
                        .url(baseUrl + "/journals")
                        .addHeader("Authorization", "Bearer " + token)
                        .post(body)
                        .build();
                executeAsync(client, request, callback);
            }

            @Override
            public void onError(String error) {
                callback.onFailure(error);
            }
        });
    }

    public void deleteJournal(String journalId, ApiCallback callback) {
        withUrlAndToken(new UrlTokenCallback() {
            @Override
            public void onReady(String baseUrl, String token) {
                Request request = new Request.Builder()
                        .url(baseUrl + "/journals/" + journalId)
                        .addHeader("Authorization", "Bearer " + token)
                        .delete()
                        .build();
                executeAsync(client, request, callback);
            }

            @Override
            public void onError(String error) {
                callback.onFailure(error);
            }
        });
    }

    // ──────────────────────────────────────────────
    // HEALTH: GET /health
    // ──────────────────────────────────────────────

    public void healthCheck(ApiCallback callback) {
        ApiConfig.getBaseUrl(new ApiConfig.Callback() {
            @Override
            public void onReady(String baseUrl) {
                Request request = new Request.Builder()
                        .url(baseUrl + "/health")
                        .get()
                        .build();
                executeAsync(client, request, callback);
            }

            @Override
            public void onError(String error) {
                callback.onFailure(error);
            }
        });
    }

    // ──────────────────────────────────────────────
    // INTERNAL
    // ──────────────────────────────────────────────

    private void executeAsync(OkHttpClient httpClient, Request request, ApiCallback callback) {
        httpClient.newCall(request).enqueue(new Callback() {
            @Override
            public void onFailure(Call call, IOException e) {
                callback.onFailure(e.getMessage());
            }

            @Override
            public void onResponse(Call call, Response response) throws IOException {
                String body = response.body() != null ? response.body().string() : "";
                if (response.isSuccessful()) {
                    callback.onSuccess(body);
                } else {
                    // Nếu 401, invalidate URL cache (có thể server restart)
                    if (response.code() == 401) {
                        ApiConfig.invalidateCache();
                    }
                    callback.onFailure("HTTP " + response.code() + ": " + body);
                }
            }
        });
    }

    public interface ApiCallback {
        void onSuccess(String responseBody);
        void onFailure(String error);
    }
}

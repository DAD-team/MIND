package com.example.mind.schedule.ui;

import android.app.AlertDialog;
import android.app.Dialog;
import android.graphics.Color;
import android.graphics.drawable.ColorDrawable;
import android.os.Bundle;
import android.util.Log;
import android.view.View;
import android.view.Window;
import android.view.WindowManager;
import android.widget.ArrayAdapter;
import android.widget.EditText;
import android.widget.Spinner;
import android.widget.TextView;
import android.widget.Toast;

import androidx.appcompat.app.AppCompatActivity;
import androidx.appcompat.app.AppCompatDelegate;
import androidx.recyclerview.widget.LinearLayoutManager;

import com.example.mind.checkin.data.MoodStorage;
import androidx.recyclerview.widget.RecyclerView;

import com.example.mind.R;
import com.example.mind.schedule.ScheduleStorage;
import com.example.mind.schedule.adapter.ScheduleAdapter;
import com.example.mind.schedule.model.ScheduleItem;

import java.util.List;
import java.util.Locale;

public class ScheduleActivity extends AppCompatActivity {

    private static final String TAG = "ScheduleActivity";

    private RecyclerView rvSchedule;
    private View emptyState;
    private ScheduleAdapter adapter;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        AppCompatDelegate.setDefaultNightMode(AppCompatDelegate.MODE_NIGHT_NO);
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_schedule);

        // Cập nhật tương tác (tín hiệu 1 - khoảng im lặng)
        new MoodStorage(this).updateLastInteraction(System.currentTimeMillis());

        rvSchedule = findViewById(R.id.rvSchedule);
        emptyState = findViewById(R.id.emptyState);

        findViewById(R.id.btnBack).setOnClickListener(v -> finish());
        findViewById(R.id.fabAdd).setOnClickListener(v -> {
            Log.d(TAG, "FAB clicked!");
            showScheduleDialog(null);
        });

        adapter = new ScheduleAdapter(this::onClassLongClick, this::onEditClick);
        rvSchedule.setLayoutManager(new LinearLayoutManager(this));
        rvSchedule.setAdapter(adapter);

        loadSchedule();
    }

    // ──────────────────────────────────────────────
    // LOAD (async)
    // ──────────────────────────────────────────────

    private void loadSchedule() {
        ScheduleStorage.load(this, new ScheduleStorage.ScheduleListCallback() {
            @Override
            public void onSuccess(List<ScheduleItem> items) {
                runOnUiThread(() -> {
                    adapter.setWeeklyData(items);
                    boolean empty = items.isEmpty();
                    rvSchedule.setVisibility(empty ? View.GONE : View.VISIBLE);
                    emptyState.setVisibility(empty ? View.VISIBLE : View.GONE);
                });
            }

            @Override
            public void onFailure(String error) {
                Log.e(TAG, "loadSchedule failed: " + error);
                runOnUiThread(() ->
                    Toast.makeText(ScheduleActivity.this,
                            "Không tải được lịch học", Toast.LENGTH_SHORT).show()
                );
            }
        });
    }

    // ──────────────────────────────────────────────
    // ADD / EDIT DIALOG (floating overlay)
    // ──────────────────────────────────────────────

    private void showScheduleDialog(ScheduleItem editItem) {
        try {
            Dialog dialog = new Dialog(this);
            dialog.requestWindowFeature(Window.FEATURE_NO_TITLE);
            dialog.setContentView(R.layout.dialog_add_schedule);

            if (dialog.getWindow() != null) {
                dialog.getWindow().setBackgroundDrawable(new ColorDrawable(Color.TRANSPARENT));
                dialog.getWindow().setLayout(
                        WindowManager.LayoutParams.MATCH_PARENT,
                        WindowManager.LayoutParams.WRAP_CONTENT
                );
                dialog.getWindow().setDimAmount(0.5f);
            }

            TextView tvTitle = dialog.findViewById(R.id.tvDialogTitle);
            EditText edtSubject = dialog.findViewById(R.id.edtSubject);
            EditText edtRoom = dialog.findViewById(R.id.edtRoom);
            Spinner spinnerDay = dialog.findViewById(R.id.spinnerDay);
            Spinner spinnerEventType = dialog.findViewById(R.id.spinnerEventType);
            EditText edtStartHour = dialog.findViewById(R.id.edtStartHour);
            EditText edtStartMin = dialog.findViewById(R.id.edtStartMin);
            EditText edtEndHour = dialog.findViewById(R.id.edtEndHour);
            EditText edtEndMin = dialog.findViewById(R.id.edtEndMin);
            TextView btnSave = dialog.findViewById(R.id.btnSaveSchedule);
            TextView btnCancel = dialog.findViewById(R.id.btnCancelSchedule);

            ArrayAdapter<String> dayAdapter = new ArrayAdapter<>(this,
                    android.R.layout.simple_spinner_dropdown_item, ScheduleItem.DAY_NAMES);
            spinnerDay.setAdapter(dayAdapter);

            ArrayAdapter<String> eventTypeAdapter = new ArrayAdapter<>(this,
                    android.R.layout.simple_spinner_dropdown_item, ScheduleItem.EVENT_TYPE_NAMES);
            spinnerEventType.setAdapter(eventTypeAdapter);

            boolean isEdit = editItem != null;
            if (isEdit) {
                tvTitle.setText(R.string.schedule_edit_title);
                btnSave.setText(R.string.schedule_btn_update);
                edtSubject.setText(editItem.subject);
                edtRoom.setText(editItem.room);
                spinnerDay.setSelection(editItem.dayOfWeek);
                spinnerEventType.setSelection(editItem.eventType);

                String[] startParts = editItem.startTime.split(":");
                if (startParts.length == 2) {
                    edtStartHour.setText(startParts[0]);
                    edtStartMin.setText(startParts[1]);
                }
                String[] endParts = editItem.endTime.split(":");
                if (endParts.length == 2) {
                    edtEndHour.setText(endParts[0]);
                    edtEndMin.setText(endParts[1]);
                }
            }

            btnCancel.setOnClickListener(v -> dialog.dismiss());

            btnSave.setOnClickListener(v -> {
                String subject = edtSubject.getText().toString().trim();
                if (subject.isEmpty()) {
                    edtSubject.setError("Vui lòng nhập tên môn");
                    edtSubject.requestFocus();
                    return;
                }

                int startH = parseNumber(edtStartHour, -1);
                int startM = parseNumber(edtStartMin, -1);
                if (startH < 0 || startH > 23) {
                    edtStartHour.setError("0-23");
                    edtStartHour.requestFocus();
                    return;
                }
                if (startM < 0 || startM > 59) {
                    edtStartMin.setError("0-59");
                    edtStartMin.requestFocus();
                    return;
                }

                int endH = parseNumber(edtEndHour, -1);
                int endM = parseNumber(edtEndMin, -1);
                if (endH < 0 || endH > 23) {
                    edtEndHour.setError("0-23");
                    edtEndHour.requestFocus();
                    return;
                }
                if (endM < 0 || endM > 59) {
                    edtEndMin.setError("0-59");
                    edtEndMin.requestFocus();
                    return;
                }

                int startTotal = startH * 60 + startM;
                int endTotal = endH * 60 + endM;
                if (endTotal <= startTotal) {
                    edtEndHour.setError("Phải sau giờ bắt đầu");
                    edtEndHour.requestFocus();
                    return;
                }

                String startTime = String.format(Locale.getDefault(), "%02d:%02d", startH, startM);
                String endTime = String.format(Locale.getDefault(), "%02d:%02d", endH, endM);

                ScheduleItem newItem = new ScheduleItem(
                        subject,
                        edtRoom.getText().toString().trim(),
                        spinnerDay.getSelectedItemPosition(),
                        startTime,
                        endTime,
                        spinnerEventType.getSelectedItemPosition()
                );

                btnSave.setEnabled(false);

                if (isEdit) {
                    newItem.id = editItem.id;
                    ScheduleStorage.updateItem(this, newItem, new ScheduleStorage.ScheduleItemCallback() {
                        @Override
                        public void onSuccess(ScheduleItem item) {
                            runOnUiThread(() -> {
                                Toast.makeText(ScheduleActivity.this,
                                        "Đã cập nhật " + subject, Toast.LENGTH_SHORT).show();
                                loadSchedule();
                                dialog.dismiss();
                            });
                        }

                        @Override
                        public void onFailure(String error) {
                            Log.e(TAG, "updateItem failed: " + error);
                            runOnUiThread(() -> {
                                btnSave.setEnabled(true);
                                Toast.makeText(ScheduleActivity.this,
                                        "Lỗi cập nhật: " + error, Toast.LENGTH_LONG).show();
                            });
                        }
                    });
                } else {
                    ScheduleStorage.addItem(this, newItem, new ScheduleStorage.ScheduleItemCallback() {
                        @Override
                        public void onSuccess(ScheduleItem item) {
                            runOnUiThread(() -> {
                                Toast.makeText(ScheduleActivity.this,
                                        "Đã thêm " + subject, Toast.LENGTH_SHORT).show();
                                loadSchedule();
                                dialog.dismiss();
                            });
                        }

                        @Override
                        public void onFailure(String error) {
                            Log.e(TAG, "addItem failed: " + error);
                            runOnUiThread(() -> {
                                btnSave.setEnabled(true);
                                Toast.makeText(ScheduleActivity.this,
                                        "Lỗi thêm môn: " + error, Toast.LENGTH_LONG).show();
                            });
                        }
                    });
                }
            });

            dialog.show();

        } catch (Exception e) {
            Log.e(TAG, "showScheduleDialog FAILED", e);
            Toast.makeText(this, "Lỗi: " + e.getMessage(), Toast.LENGTH_LONG).show();
        }
    }

    private int parseNumber(EditText edt, int fallback) {
        String text = edt.getText().toString().trim();
        if (text.isEmpty()) return fallback;
        try {
            return Integer.parseInt(text);
        } catch (NumberFormatException e) {
            return fallback;
        }
    }

    // ──────────────────────────────────────────────
    // EDIT
    // ──────────────────────────────────────────────

    private void onEditClick(ScheduleItem item) {
        showScheduleDialog(item);
    }

    // ──────────────────────────────────────────────
    // DELETE (async)
    // ──────────────────────────────────────────────

    private void onClassLongClick(ScheduleItem item) {
        new AlertDialog.Builder(this)
                .setTitle("Xoá môn học")
                .setMessage("Bạn muốn xoá \"" + item.subject + "\" khỏi lịch?")
                .setPositiveButton("Xoá", (d, w) -> {
                    ScheduleStorage.deleteItem(this, item, new ScheduleStorage.DeleteCallback() {
                        @Override
                        public void onSuccess() {
                            runOnUiThread(() -> {
                                Toast.makeText(ScheduleActivity.this,
                                        "Đã xoá " + item.subject, Toast.LENGTH_SHORT).show();
                                loadSchedule();
                            });
                        }

                        @Override
                        public void onFailure(String error) {
                            Log.e(TAG, "deleteItem failed: " + error);
                            runOnUiThread(() ->
                                Toast.makeText(ScheduleActivity.this,
                                        "Lỗi xoá: " + error, Toast.LENGTH_LONG).show()
                            );
                        }
                    });
                })
                .setNegativeButton("Huỷ", null)
                .show();
    }
}

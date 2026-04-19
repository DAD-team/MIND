package com.example.mind.journal;

import android.os.Bundle;
import android.widget.EditText;
import android.widget.TextView;
import android.widget.Toast;

import androidx.appcompat.app.AppCompatActivity;
import androidx.appcompat.app.AppCompatDelegate;

import com.example.mind.R;
import com.example.mind.auth.ApiClient;
import com.example.mind.checkin.data.MoodStorage;

import org.json.JSONObject;

import java.text.SimpleDateFormat;
import java.util.Date;
import java.util.Locale;

public class JournalWriteActivity extends AppCompatActivity {

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        AppCompatDelegate.setDefaultNightMode(AppCompatDelegate.MODE_NIGHT_NO);
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_journal_write);

        // Show today's date
        TextView tvDate = findViewById(R.id.tvDate);
        SimpleDateFormat sdf = new SimpleDateFormat("dd 'Th'MM, yyyy", new Locale("vi"));
        tvDate.setText(sdf.format(new Date()));

        // Back button
        findViewById(R.id.btnBack).setOnClickListener(v -> finish());

        // Save button
        EditText edtTitle = findViewById(R.id.edtTitle);
        EditText edtContent = findViewById(R.id.edtContent);

        findViewById(R.id.btnSave).setOnClickListener(v -> {
            String title = edtTitle.getText().toString().trim();
            String content = edtContent.getText().toString().trim();

            if (title.isEmpty() && content.isEmpty()) {
                Toast.makeText(this, "Hãy viết gì đó trước khi lưu nhé!", Toast.LENGTH_SHORT).show();
                return;
            }

            // Cập nhật tương tác (tín hiệu 1 - khoảng im lặng)
            new MoodStorage(this).updateLastInteraction(System.currentTimeMillis());

            // Lưu local
            JournalStorage.addEntry(this, title, content);

            // Sync lên server: POST /journals
            syncJournalToServer(title, content);

            Toast.makeText(this, "Đã lưu nhật ký!", Toast.LENGTH_SHORT).show();
            finish();
        });
    }

    private void syncJournalToServer(String title, String content) {
        try {
            JSONObject json = new JSONObject();
            json.put("title", title.isEmpty() ? "Nhật ký" : title);
            json.put("content", content);

            new ApiClient(this).createJournal(json.toString(), new ApiClient.ApiCallback() {
                @Override public void onSuccess(String r) { /* ok */ }
                @Override public void onFailure(String e) { /* local đã lưu */ }
            });
        } catch (Exception ignored) {}
    }
}

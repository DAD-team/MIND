package com.example.mind.journal;

import android.content.Intent;
import android.os.Bundle;
import android.util.TypedValue;
import android.view.Gravity;
import android.widget.ImageView;
import android.widget.LinearLayout;
import android.widget.TextView;
import android.widget.Toast;

import androidx.appcompat.app.AppCompatActivity;
import androidx.appcompat.app.AppCompatDelegate;

import com.example.mind.MainActivity;
import com.example.mind.R;
import com.example.mind.chat.ChatActivity;
import com.example.mind.settings.ui.ProfileActivity;

import java.text.SimpleDateFormat;
import java.util.Date;
import java.util.List;
import java.util.Locale;

public class JournalActivity extends AppCompatActivity {

    // Excluded broken icons: icon_note_5, icon_note_15, icon_note_28, icon_note_32
    static final int[] NOTE_ICONS = {
            R.drawable.icon_note_2, R.drawable.icon_note_3, R.drawable.icon_note_4,
            R.drawable.icon_note_6, R.drawable.icon_note_7,
            R.drawable.icon_note_8, R.drawable.icon_note_9, R.drawable.icon_note_10,
            R.drawable.icon_note_11, R.drawable.icon_note_12, R.drawable.icon_note_13,
            R.drawable.icon_note_14, R.drawable.icon_note_16,
            R.drawable.icon_note_17, R.drawable.icon_note_18, R.drawable.icon_note_19,
            R.drawable.icon_note_20, R.drawable.icon_note_21, R.drawable.icon_note_22,
            R.drawable.icon_note_23, R.drawable.icon_note_24, R.drawable.icon_note_25,
            R.drawable.icon_note_26, R.drawable.icon_note_27,
            R.drawable.icon_note_29, R.drawable.icon_note_30, R.drawable.icon_note_31
    };

    static int getIconForEntry(int iconIndex) {
        return NOTE_ICONS[Math.abs(iconIndex) % NOTE_ICONS.length];
    }

    private LinearLayout journalList;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        AppCompatDelegate.setDefaultNightMode(AppCompatDelegate.MODE_NIGHT_NO);
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_journal);

        journalList = findViewById(R.id.journalList);

        // FAB - add new journal entry
        findViewById(R.id.fabAdd).setOnClickListener(v -> {
            startActivity(new Intent(this, JournalWriteActivity.class));
        });

        setupNavBar();
    }

    @Override
    protected void onResume() {
        super.onResume();
        loadJournalEntries();
    }

    private void loadJournalEntries() {
        journalList.removeAllViews();

        List<JournalStorage.Entry> entries = JournalStorage.loadEntries(this);

        if (entries.isEmpty()) {
            TextView empty = new TextView(this);
            empty.setText(R.string.journal_empty);
            empty.setTextColor(getColor(R.color.text_hint));
            empty.setTextSize(TypedValue.COMPLEX_UNIT_SP, 15);
            empty.setGravity(Gravity.CENTER);
            empty.setFontFeatureSettings("normal");
            LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(
                    LinearLayout.LayoutParams.MATCH_PARENT,
                    LinearLayout.LayoutParams.WRAP_CONTENT);
            lp.topMargin = dp(40);
            empty.setLayoutParams(lp);
            journalList.addView(empty);
            return;
        }

        SimpleDateFormat sdfDate = new SimpleDateFormat("dd 'Th'MM", new Locale("vi"));
        SimpleDateFormat sdfTime = new SimpleDateFormat("dd/MM/yyyy, HH:mm", new Locale("vi"));

        for (JournalStorage.Entry entry : entries) {
            journalList.addView(createEntryCard(entry, sdfDate, sdfTime));
        }
    }

    private LinearLayout createEntryCard(JournalStorage.Entry entry,
                                          SimpleDateFormat sdfDate,
                                          SimpleDateFormat sdfTime) {
        Date date = new Date(entry.timestamp);

        // Card container
        LinearLayout card = new LinearLayout(this);
        card.setOrientation(LinearLayout.HORIZONTAL);
        card.setGravity(Gravity.CENTER_VERTICAL);
        card.setBackgroundResource(R.drawable.bg_card_journal);
        card.setPadding(dp(10), dp(10), dp(10), dp(10));
        LinearLayout.LayoutParams cardLp = new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT,
                LinearLayout.LayoutParams.WRAP_CONTENT);
        cardLp.bottomMargin = dp(12);
        card.setLayoutParams(cardLp);

        // Icon
        ImageView icon = new ImageView(this);
        LinearLayout.LayoutParams iconLp = new LinearLayout.LayoutParams(dp(72), dp(72));
        icon.setLayoutParams(iconLp);
        icon.setAdjustViewBounds(true);
        icon.setScaleType(ImageView.ScaleType.FIT_CENTER);
        icon.setImageResource(getIconForEntry(entry.iconIndex));
        card.addView(icon);

        // Info column
        LinearLayout info = new LinearLayout(this);
        info.setOrientation(LinearLayout.VERTICAL);
        LinearLayout.LayoutParams infoLp = new LinearLayout.LayoutParams(
                0, LinearLayout.LayoutParams.WRAP_CONTENT, 1);
        infoLp.leftMargin = dp(12);
        info.setLayoutParams(infoLp);

        // Date text
        TextView tvDate = new TextView(this);
        tvDate.setText(sdfDate.format(date));
        tvDate.setTextColor(getColor(R.color.text_primary));
        tvDate.setTextSize(TypedValue.COMPLEX_UNIT_SP, 20);
        tvDate.setTypeface(tvDate.getTypeface(), android.graphics.Typeface.BOLD);
        info.addView(tvDate);

        // Preview text
        TextView tvPreview = new TextView(this);
        String preview = entry.title.isEmpty() ? entry.content : entry.title;
        if (preview.length() > 40) preview = preview.substring(0, 40) + "…";
        tvPreview.setText(preview);
        tvPreview.setTextColor(getColor(R.color.text_primary));
        tvPreview.setTextSize(TypedValue.COMPLEX_UNIT_SP, 14);
        tvPreview.setMaxLines(1);
        LinearLayout.LayoutParams previewLp = new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.WRAP_CONTENT,
                LinearLayout.LayoutParams.WRAP_CONTENT);
        previewLp.topMargin = dp(2);
        tvPreview.setLayoutParams(previewLp);
        info.addView(tvPreview);

        // Timestamp
        TextView tvTime = new TextView(this);
        tvTime.setText(sdfTime.format(date));
        tvTime.setTextColor(getColor(R.color.text_hint));
        tvTime.setTextSize(TypedValue.COMPLEX_UNIT_SP, 12);
        LinearLayout.LayoutParams timeLp = new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.WRAP_CONTENT,
                LinearLayout.LayoutParams.WRAP_CONTENT);
        timeLp.topMargin = dp(2);
        tvTime.setLayoutParams(timeLp);
        info.addView(tvTime);

        card.addView(info);
        return card;
    }

    private int dp(int value) {
        return (int) TypedValue.applyDimension(
                TypedValue.COMPLEX_UNIT_DIP, value, getResources().getDisplayMetrics());
    }

    private void setupNavBar() {
        findViewById(R.id.navHome).setOnClickListener(v -> {
            startActivity(new Intent(this, MainActivity.class));
            finish();
        });

        findViewById(R.id.navNote).setOnClickListener(v -> {
            // Already on journal
        });

        findViewById(R.id.navChat).setOnClickListener(v -> {
            startActivity(new Intent(this, ChatActivity.class));
        });

        findViewById(R.id.navHeart).setOnClickListener(v -> {
            startActivity(new Intent(this, ProfileActivity.class));
        });
    }
}

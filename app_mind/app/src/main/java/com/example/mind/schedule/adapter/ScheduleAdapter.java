package com.example.mind.schedule.adapter;

import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;
import android.widget.ImageView;
import android.widget.TextView;

import androidx.annotation.NonNull;
import androidx.recyclerview.widget.RecyclerView;

import com.example.mind.R;
import com.example.mind.schedule.model.ScheduleItem;

import java.util.ArrayList;
import java.util.List;

/**
 * Adapter hiển thị lịch tuần dạng grouped-by-day.
 * Mỗi ngày có header + danh sách môn + empty placeholder nếu trống.
 */
public class ScheduleAdapter extends RecyclerView.Adapter<RecyclerView.ViewHolder> {

    private static final int TYPE_DAY_HEADER = 0;
    private static final int TYPE_CLASS_ITEM = 1;
    private static final int TYPE_EMPTY_DAY = 2;

    // Icon quái vật cho mỗi ngày (T2→CN)
    private static final int[] DAY_ICONS = {
            R.drawable.icon_note_3, R.drawable.icon_note_4,
            R.drawable.icon_note_6, R.drawable.icon_note_7,
            R.drawable.icon_note_8, R.drawable.icon_note_10,
            R.drawable.icon_note_12
    };

    // Icon quái vật xoay vòng cho mỗi class item
    private static final int[] CLASS_ICONS = {
            R.drawable.icon_note_20, R.drawable.icon_note_22,
            R.drawable.icon_note_24, R.drawable.icon_note_26,
            R.drawable.icon_note_3, R.drawable.icon_note_4,
    };

    private final List<Row> rows = new ArrayList<>();
    private OnClassLongClickListener longClickListener;
    private OnEditClickListener editClickListener;

    public interface OnClassLongClickListener {
        void onLongClick(ScheduleItem item);
    }

    public interface OnEditClickListener {
        void onEditClick(ScheduleItem item);
    }

    public ScheduleAdapter(OnClassLongClickListener longListener, OnEditClickListener editListener) {
        this.longClickListener = longListener;
        this.editClickListener = editListener;
    }

    /** Build rows: 7 ngày × (header + classes hoặc empty) */
    public void setWeeklyData(List<ScheduleItem> allItems) {
        rows.clear();

        for (int day = 0; day < 7; day++) {
            // Header cho ngày này
            List<ScheduleItem> dayItems = new ArrayList<>();
            for (ScheduleItem item : allItems) {
                if (item.dayOfWeek == day) dayItems.add(item);
            }

            rows.add(new Row(TYPE_DAY_HEADER, day, null, dayItems.size()));

            if (dayItems.isEmpty()) {
                rows.add(new Row(TYPE_EMPTY_DAY, day, null, 0));
            } else {
                for (ScheduleItem item : dayItems) {
                    rows.add(new Row(TYPE_CLASS_ITEM, day, item, 0));
                }
            }
        }

        notifyDataSetChanged();
    }

    @Override
    public int getItemViewType(int position) {
        return rows.get(position).type;
    }

    @Override
    public int getItemCount() {
        return rows.size();
    }

    @NonNull
    @Override
    public RecyclerView.ViewHolder onCreateViewHolder(@NonNull ViewGroup parent, int viewType) {
        LayoutInflater inflater = LayoutInflater.from(parent.getContext());
        switch (viewType) {
            case TYPE_DAY_HEADER:
                return new DayHeaderVH(inflater.inflate(R.layout.item_schedule_day_header, parent, false));
            case TYPE_CLASS_ITEM:
                return new ClassItemVH(inflater.inflate(R.layout.item_schedule, parent, false));
            default:
                return new EmptyDayVH(inflater.inflate(R.layout.item_schedule_empty_day, parent, false));
        }
    }

    @Override
    public void onBindViewHolder(@NonNull RecyclerView.ViewHolder holder, int position) {
        Row row = rows.get(position);

        if (holder instanceof DayHeaderVH) {
            DayHeaderVH h = (DayHeaderVH) holder;
            h.tvDayName.setText(ScheduleItem.DAY_NAMES[row.dayOfWeek]);
            h.ivDayIcon.setImageResource(DAY_ICONS[row.dayOfWeek % DAY_ICONS.length]);
            h.tvClassCount.setText(row.classCount > 0 ? row.classCount + " môn" : "");

        } else if (holder instanceof ClassItemVH) {
            ClassItemVH h = (ClassItemVH) holder;
            ScheduleItem item = row.item;

            h.tvSubject.setText(item.subject);

            String detail = item.startTime + " - " + item.endTime;
            if (item.room != null && !item.room.isEmpty()) {
                detail += "  •  " + item.room;
            }
            h.tvDetail.setText(detail);

            h.tvTime.setText(item.startTime);
            h.ivIcon.setImageResource(CLASS_ICONS[position % CLASS_ICONS.length]);

            h.itemView.setOnLongClickListener(v -> {
                if (longClickListener != null) longClickListener.onLongClick(item);
                return true;
            });

            h.btnEdit.setOnClickListener(v -> {
                if (editClickListener != null) editClickListener.onEditClick(item);
            });
        }
    }

    // ──────────────────────────────────────────────
    // ViewHolders
    // ──────────────────────────────────────────────

    static class DayHeaderVH extends RecyclerView.ViewHolder {
        ImageView ivDayIcon;
        TextView tvDayName, tvClassCount;

        DayHeaderVH(View v) {
            super(v);
            ivDayIcon = v.findViewById(R.id.ivDayIcon);
            tvDayName = v.findViewById(R.id.tvDayName);
            tvClassCount = v.findViewById(R.id.tvClassCount);
        }
    }

    static class ClassItemVH extends RecyclerView.ViewHolder {
        ImageView ivIcon, btnEdit;
        TextView tvSubject, tvDetail, tvTime;

        ClassItemVH(View v) {
            super(v);
            ivIcon = v.findViewById(R.id.ivIcon);
            tvSubject = v.findViewById(R.id.tvSubject);
            tvDetail = v.findViewById(R.id.tvDetail);
            tvTime = v.findViewById(R.id.tvTime);
            btnEdit = v.findViewById(R.id.btnEdit);
        }
    }

    static class EmptyDayVH extends RecyclerView.ViewHolder {
        EmptyDayVH(View v) { super(v); }
    }

    // ──────────────────────────────────────────────
    // Row model
    // ──────────────────────────────────────────────

    private static class Row {
        int type;
        int dayOfWeek;
        ScheduleItem item;
        int classCount;

        Row(int type, int dayOfWeek, ScheduleItem item, int classCount) {
            this.type = type;
            this.dayOfWeek = dayOfWeek;
            this.item = item;
            this.classCount = classCount;
        }
    }
}

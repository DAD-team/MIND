package com.example.mind.chat;

import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;
import android.widget.LinearLayout;
import android.widget.TextView;

import androidx.annotation.NonNull;
import androidx.recyclerview.widget.RecyclerView;

import com.example.mind.R;

import java.util.List;

public class ChatAdapter extends RecyclerView.Adapter<RecyclerView.ViewHolder> {

    public interface OnMoodSelectedListener {
        void onMoodSelected(int position, int mood);
    }

    private final List<ChatMessage> messages;
    private OnMoodSelectedListener moodListener;

    public ChatAdapter(List<ChatMessage> messages) {
        this.messages = messages;
    }

    public void setMoodListener(OnMoodSelectedListener listener) {
        this.moodListener = listener;
    }

    @Override
    public int getItemViewType(int position) {
        return messages.get(position).type;
    }

    @NonNull
    @Override
    public RecyclerView.ViewHolder onCreateViewHolder(@NonNull ViewGroup parent, int viewType) {
        LayoutInflater inflater = LayoutInflater.from(parent.getContext());
        if (viewType == ChatMessage.TYPE_MOOD_PICKER) {
            View view = inflater.inflate(R.layout.item_msg_mood, parent, false);
            return new MoodViewHolder(view);
        }
        int layout = viewType == ChatMessage.TYPE_MINDY
                ? R.layout.item_msg_mindy
                : R.layout.item_msg_user;
        View view = inflater.inflate(layout, parent, false);
        return new MsgViewHolder(view);
    }

    @Override
    public void onBindViewHolder(@NonNull RecyclerView.ViewHolder holder, int position) {
        ChatMessage msg = messages.get(position);

        if (holder instanceof MoodViewHolder) {
            MoodViewHolder moodHolder = (MoodViewHolder) holder;
            moodHolder.tvMessage.setText(msg.text);

            // Setup mood button clicks
            int[] moodIds = {
                    R.id.chatMoodHappy, R.id.chatMoodSad, R.id.chatMoodStressed,
                    R.id.chatMoodExcited, R.id.chatMoodNeutral, R.id.chatMoodTired
            };
            for (int i = 0; i < moodIds.length; i++) {
                LinearLayout btn = holder.itemView.findViewById(moodIds[i]);
                final int mood = i + 1;
                btn.setSelected(msg.selectedMood == mood);

                if (msg.selectedMood < 0) {
                    // Chưa chọn → cho phép bấm
                    btn.setOnClickListener(v -> {
                        msg.selectedMood = mood;
                        notifyItemChanged(holder.getAdapterPosition());
                        if (moodListener != null) {
                            moodListener.onMoodSelected(holder.getAdapterPosition(), mood);
                        }
                    });
                    btn.setAlpha(1f);
                } else {
                    // Đã chọn → disable các nút khác
                    btn.setOnClickListener(null);
                    btn.setAlpha(msg.selectedMood == mood ? 1f : 0.4f);
                }
            }
        } else {
            ((MsgViewHolder) holder).tvMessage.setText(msg.text);
        }
    }

    @Override
    public int getItemCount() {
        return messages.size();
    }

    static class MsgViewHolder extends RecyclerView.ViewHolder {
        final TextView tvMessage;
        MsgViewHolder(View itemView) {
            super(itemView);
            tvMessage = itemView.findViewById(R.id.tvMessage);
        }
    }

    static class MoodViewHolder extends RecyclerView.ViewHolder {
        final TextView tvMessage;
        MoodViewHolder(View itemView) {
            super(itemView);
            tvMessage = itemView.findViewById(R.id.tvMessage);
        }
    }
}

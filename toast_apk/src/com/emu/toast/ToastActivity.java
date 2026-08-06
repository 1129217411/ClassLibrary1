package com.emu.toast;

import android.app.Activity;
import android.os.Bundle;
import android.widget.Toast;
import android.widget.TextView;
import android.view.Gravity;
import android.view.LayoutInflater;
import android.view.View;
import android.content.Intent;
import android.util.Base64;

public class ToastActivity extends Activity {
    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        Intent intent = getIntent();
        String msg = decode(intent.getStringExtra("msg"));
        if (msg == null) msg = "";

        LayoutInflater inflater = getLayoutInflater();
        View layout = inflater.inflate(R.layout.toast_layout, null);
        TextView text = (TextView) layout.findViewById(R.id.toast_text);
        text.setText(msg);

        Toast toast = new Toast(this);
        toast.setGravity(Gravity.BOTTOM | Gravity.CENTER_HORIZONTAL, 0, 200);
        toast.setDuration(Toast.LENGTH_LONG);
        toast.setView(layout);
        toast.show();
        finish();
    }

    private String decode(String b64) {
        if (b64 == null || b64.isEmpty()) return "";
        try {
            byte[] bytes = Base64.decode(b64, Base64.DEFAULT);
            return new String(bytes, "UTF-8");
        } catch (Exception e) {
            return b64;
        }
    }
}
